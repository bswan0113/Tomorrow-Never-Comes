// --- START OF FILE DataManager.txt ---

using System;
using UnityEngine;
using System.Collections.Generic;
using System.Linq; // LINQ 확장 메서드를 위해 추가
using Newtonsoft.Json;
using System.Threading.Tasks; // 비동기 처리를 위해 추가
using System.Collections.Concurrent;
using System.IO;
using System.Text.RegularExpressions;
using Core.Data;
using Core.Data.Interface;
using Core.Logging;
using Features.Data;
using Features.Player;
using VContainer; // 비동기 큐를 위해 추가

public class DataManager : IDataService
{
    private IDatabaseAccess _dbAccess;
    public bool HasSaveData { get; private set; } = false;

    // P14: 세이브 직렬화 큐(SaveQueue) 도입
    private readonly ConcurrentQueue<Func<Task>> m_SaveQueue = new ConcurrentQueue<Func<Task>>();
    private bool m_IsProcessingSaveQueue = false;

    private SchemaManager _schemaManager;

    // P24: DataManager 역할 혼합 - Repository 패턴 도입으로 DataManager는 파사드 역할 수행
    // DataManager가 트랜잭션 관리를 위해 시리얼라이저를 직접 사용하므로 주입받습니다.
    private IGameProgressRepository _gameProgressRepository;
    private IPlayerStatsRepository _playerStatsRepository;
    private IDataSerializer<GameProgressData> _gameProgressSerializer; // DataManager에서 직접 사용하기 위해 주입
    private IDataSerializer<PlayerStatsData> _playerStatsSerializer; // DataManager에서 직접 사용하기 위해 주입

    [Inject]
    public void Construct(IDatabaseAccess dbAccess, SchemaManager schemaManager,
                           IGameProgressRepository gameProgressRepository,
                           IPlayerStatsRepository playerStatsRepository,
                           IDataSerializer<GameProgressData> gameProgressSerializer, // VContainer를 통해 주입받음
                           IDataSerializer<PlayerStatsData> playerStatsSerializer) // VContainer를 통해 주입받음
    {
        _dbAccess = dbAccess ?? throw new ArgumentNullException(nameof(dbAccess));
        _schemaManager = schemaManager ?? throw new ArgumentNullException(nameof(schemaManager));
        _gameProgressRepository = gameProgressRepository ?? throw new ArgumentNullException(nameof(gameProgressRepository));
        _playerStatsRepository = playerStatsRepository ?? throw new ArgumentNullException(nameof(playerStatsRepository));
        _gameProgressSerializer = gameProgressSerializer ?? throw new ArgumentNullException(nameof(gameProgressSerializer));
        _playerStatsSerializer = playerStatsSerializer ?? throw new ArgumentNullException(nameof(playerStatsSerializer));

        try
        {
            // DB 연결은 DataManager 초기화 시점에 여기서 수행
            _dbAccess.OpenConnection();
        }
        catch (Exception ex)
        {
            CoreLogger.LogError($"[DataManager] FATAL ERROR: Failed to open database connection during initialization: {ex.Message}");
            throw;
        }

        InitializeDatabaseTables();
        LoadAllGameData(); // Play Mode 진입 시 초기 저장 데이터 유무 확인

        CoreLogger.Log("[DataManager] DataManager Initialized successfully.");
    }

    // private void OnApplicationPause(bool pauseStatus)
    // {
    //     if (pauseStatus)
    //     {
    //         CoreLogger.Log("[DataManager] OnApplicationPause: App going to background. Closing DB connection.");
    //         _dbAccess?.CloseConnection();
    //     }
    //     else
    //     {
    //         CoreLogger.Log("[DataManager] OnApplicationPause: App coming to foreground. Opening DB connection.");
    //         try
    //         {
    //             _dbAccess?.OpenConnection();
    //         }
    //         catch (Exception ex)
    //         {
    //             CoreLogger.LogError($"[DataManager] Failed to re-open database connection on app resume: {ex.Message}");
    //         }
    //     }
    // }

    private void OnDestroy()
    {
        CoreLogger.Log("[DataManager] OnDestroy: Closing DB connection.");
        _dbAccess?.CloseConnection();
    }

    private void InitializeDatabaseTables()
    {
        if (_dbAccess == null)
        {
            CoreLogger.LogError("[DataManager] DatabaseAccess is not initialized for table initialization.");
            return;
        }
        if (_schemaManager == null)
        {
            CoreLogger.LogError("[DataManager] SchemaManager is not initialized for table initialization.");
            return;
        }

        try
        {
            foreach (var query in _schemaManager.GetAllTableCreateQueries())
            {
                if (string.IsNullOrWhiteSpace(query))
                {
                    CoreLogger.LogWarning("[DataManager] Skipping null or empty schema query from SchemaManager.");
                    continue;
                }

                _dbAccess.ExecuteNonQuery(query);

                string tableNameForLog = "Unknown Table";
                Regex TableNameRegex = new Regex(@"CREATE TABLE (IF NOT EXISTS )?(?<TableName>\w+)", RegexOptions.IgnoreCase);
                Match match = TableNameRegex.Match(query);
                if (match.Success)
                {
                    tableNameForLog = match.Groups["TableName"].Value;
                }
                CoreLogger.Log($"[DataManager] Executed table creation query for: {tableNameForLog}");
            }
            CoreLogger.Log("[DataManager] Database tables are verified using SchemaManager schemas.");
        }
        catch (Exception ex)
        {
            CoreLogger.LogError($"[DataManager] Error initializing database tables: {ex.Message}");
            throw new InvalidOperationException("Failed to initialize database tables.", ex);
        }
    }


    // P25: DataManager.LoadAllGameData()에서 GetAwaiter().GetResult() 사용 시 데드락 방지
    // 이제 이 메서드는 async 키워드를 제거하여 완전한 동기 메서드로 작동합니다.
    public void LoadAllGameData() // async 키워드 제거
    {
        if (_dbAccess == null)
        {
            CoreLogger.LogError("[DataManager] DatabaseAccess is not initialized for LoadAllGameData.");
            return;
        }

        try
        {
            CoreLogger.Log("[DataManager] Attempting to load all game data to check save state...");

            HasSaveData = _playerStatsRepository.HasPlayerStatsData(1); // 기본 슬롯 ID 1 사용 가정

            if (HasSaveData)
            {
                CoreLogger.Log("[DataManager] Save data found. HasSaveData = true.");
                // 실제 데이터를 로드하는 부분은 필요한 시점(예: 게임 시작)에 GameManager 등에서
                // _gameProgressRepository.LoadGameProgressAsync(1) 등을 호출하여 처리합니다.
                // 여기서는 HasSaveData 플래그만 업데이트합니다.
            }
            else
            {
                CoreLogger.Log("[DataManager] No save data found. HasSaveData = false.");
            }
        }
        catch (Exception ex)
        {
            CoreLogger.LogError($"[DataManager] Error during LoadAllGameData: {ex.Message}");
            HasSaveData = false; // 로딩 실패 시 저장 데이터 없음으로 처리
        }
    }

    public Task SaveAllGameData(int saveSlotId = 1) // async 키워드 제거
    {
        var playerStatsToSave = new PlayerStatsData {
            SaveSlotID = saveSlotId,
            Intellect = 10, Charm = 10, Endurance = 10, Money = 0,
            HeroineALiked = 0, HeroineBLiked = 0, HeroineCLiked = 0 // 실제 데이터로 대체
        };
        var gameProgressToSave = new GameProgressData {
            SaveSlotID = saveSlotId,
            CurrentDay = 1, // 실제 데이터로 대체
            LastSceneName = "PlayerRoom", // 실제 데이터로 대체
            SaveDateTime = DateTime.UtcNow
        };

        CoreLogger.Log($"[DataManager] Enqueuing SaveAllGameData request for Slot {saveSlotId}...");
        m_SaveQueue.Enqueue(async () => await PerformSaveOperation(playerStatsToSave, gameProgressToSave));
        ProcessSaveQueue(); // 큐 처리 시작 (이미 처리 중이면 아무것도 안함)

        return Task.CompletedTask; // async 키워드 제거 후 Task 반환을 위해 추가
    }

    private async Task PerformSaveOperation(PlayerStatsData playerStats, GameProgressData gameProgress)
    {
        if (_dbAccess == null)
        {
            CoreLogger.LogError("[DataManager] FATAL ERROR: DatabaseAccess is null during save operation.");
            return;
        }
        if (_gameProgressSerializer == null || _playerStatsSerializer == null)
        {
            CoreLogger.LogError("[DataManager] FATAL ERROR: Serializers are null during save operation. Check VContainer setup.");
            return;
        }

        try
        {
            await Task.Run(() =>
            {
                _dbAccess.BeginTransaction();
                CoreLogger.Log("[DataManager] Transaction started for save operation on worker thread.");

                try
                {
                    var playerStatsMap = _playerStatsSerializer.Serialize(playerStats);
                    string psTableName = _playerStatsSerializer.GetTableName();
                    string psPrimaryKeyCol = _playerStatsSerializer.GetPrimaryKeyColumnName();
                    object psPrimaryKeyValue = playerStats.SaveSlotID;

                    var existingPlayerStats = _dbAccess.SelectWhere(psTableName, new string[] { psPrimaryKeyCol }, new string[] { "=" }, new object[] { psPrimaryKeyValue });
                    if (existingPlayerStats != null && existingPlayerStats.Count > 0)
                    {
                        _dbAccess.UpdateSet(psTableName, playerStatsMap.Keys.ToArray(), playerStatsMap.Values.ToArray(), psPrimaryKeyCol, psPrimaryKeyValue);
                        CoreLogger.Log($"[DataManager] Updated PlayerStats for SaveSlotID {psPrimaryKeyValue} on worker thread.");
                    }
                    else
                    {
                        _dbAccess.InsertInto(psTableName, playerStatsMap.Keys.ToArray(), playerStatsMap.Values.ToArray());
                        CoreLogger.Log($"[DataManager] Inserted new PlayerStats for SaveSlotID {psPrimaryKeyValue} on worker thread.");
                    }

                    var gameProgressMap = _gameProgressSerializer.Serialize(gameProgress);
                    string gpTableName = _gameProgressSerializer.GetTableName();
                    string gpPrimaryKeyCol = _gameProgressSerializer.GetPrimaryKeyColumnName();
                    object gpPrimaryKeyValue = gameProgress.SaveSlotID;

                    var existingGameProgress = _dbAccess.SelectWhere(gpTableName, new string[] { gpPrimaryKeyCol }, new string[] { "=" }, new object[] { gpPrimaryKeyValue });
                    if (existingGameProgress != null && existingGameProgress.Count > 0)
                    {
                        _dbAccess.UpdateSet(gpTableName, gameProgressMap.Keys.ToArray(), gameProgressMap.Values.ToArray(), gpPrimaryKeyCol, gpPrimaryKeyValue);
                        CoreLogger.Log($"[DataManager] Updated GameProgress for SaveSlotID {gpPrimaryKeyValue} on worker thread.");
                    }
                    else
                    {
                        _dbAccess.InsertInto(gpTableName, gameProgressMap.Keys.ToArray(), gameProgressMap.Values.ToArray());
                        CoreLogger.Log($"[DataManager] Inserted new GameProgress for SaveSlotID {gpPrimaryKeyValue} on worker thread.");
                    }

                    _dbAccess.CommitTransaction();
                    CoreLogger.Log("[DataManager] Transaction committed successfully on worker thread.");

                    HasSaveData = true;
                }
                catch (Exception innerEx)
                {
                    CoreLogger.LogError($"[DataManager] Failed to save all game data in worker thread: {innerEx.Message}. Rolling back transaction.");
                    _dbAccess.RollbackTransaction();
                    throw;
                }
            });

            CoreLogger.Log("[DataManager] All game data saved successfully via transaction (main thread notification).");
        }
        catch (Exception ex)
        {
            CoreLogger.LogError($"[DataManager] Exception propagated from save operation queue: {ex.Message}");
            throw;
        }
    }

    private async void ProcessSaveQueue()
    {
        if (m_IsProcessingSaveQueue)
        {
            return;
        }

        m_IsProcessingSaveQueue = true;
        CoreLogger.Log("[DataManager] Starting to process save queue...");

        while (m_SaveQueue.TryDequeue(out Func<Task> saveOperation))
        {
            try
            {
                await saveOperation();
            }
            catch (Exception ex)
            {
                CoreLogger.LogError($"[DataManager] Error processing save queue item: {ex.Message}. Remaining items in queue: {m_SaveQueue.Count}");
            }
        }

        m_IsProcessingSaveQueue = false;
        CoreLogger.Log("[DataManager] Save queue processing finished.");
    }

    public List<Dictionary<string, object>> LoadData(string tableName, string whereCol, object whereValue)
    {
        if (_dbAccess == null)
        {
            CoreLogger.LogError("[DataManager] DatabaseAccess is not initialized for LoadData.");
            return null;
        }
        CoreLogger.Log($"[DataManager] Loading data from {tableName} where {whereCol} = {whereValue}.");
        return _dbAccess.SelectWhere(tableName, new string[] { whereCol }, new string[] { "=" }, new object[] { whereValue });
    }

    public void InsertData(string tableName, string[] columns, object[] values)
    {
        if (_dbAccess == null)
        {
            CoreLogger.LogError("[DataManager] DatabaseAccess is not initialized for InsertData.");
            return;
        }
        CoreLogger.Log($"[DataManager] Inserting data to {tableName}.");
        _dbAccess.InsertInto(tableName, columns, values);
    }

    public void UpdateData(string tableName, string[] updateCols, object[] updateValues, string whereCol, object whereValue)
    {
        if (_dbAccess == null)
        {
            CoreLogger.LogError("[DataManager] DatabaseAccess is not initialized for UpdateData.");
            return;
        }
        CoreLogger.Log($"[DataManager] Updating data in {tableName} where {whereCol} = {whereValue}.");
        _dbAccess.UpdateSet(tableName, updateCols, updateValues, whereCol, whereValue);
    }

    public void DeleteData(string tableName, string whereCol, object whereValue)
    {
        if (_dbAccess == null)
        {
            CoreLogger.LogError("[DataManager] DatabaseAccess is not initialized for DeleteData.");
            return;
        }
        CoreLogger.Log($"[DataManager] Deleting data from {tableName} where {whereCol} = {whereValue}.");
        _dbAccess.DeleteWhere(tableName, whereCol, whereValue);
    }
}