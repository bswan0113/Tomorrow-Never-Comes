// --- START OF FILE dataManager.txt ---

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
using Core.Util;
using Features.Data;
using Features.Player;
using VContainer; // 비동기 큐를 위해 추가

public class DataManager : MonoBehaviour, IDataService
{
    private Dictionary<string, string> m_SQLQueries;
    private IDatabaseAccess _dbAccess;
    public bool HasSaveData { get; private set; } = false;

    // P14: 세이브 직렬화 큐(SaveQueue) 부재 -> 도입
    // P15: 중복 호출 위험 (SaveQueue 부재) -> SaveQueue로 해결
    // P16: 부분 저장 위험 (SaveQueue 부재) -> SaveQueue와 트랜잭션으로 해결
    private readonly ConcurrentQueue<Func<Task>> m_SaveQueue = new ConcurrentQueue<Func<Task>>();
    private bool m_IsProcessingSaveQueue = false;

    // P27: 로깅 부족 (세이브), P28: 로깅 부족 (로딩)
    // P29: 로깅 부족 (임포트 성능/실패) -> 로깅 강화

    // IDataSerializer 인스턴스들을 관리 (P7: DataManager 역할 혼합 (스키마) - 부분적으로 해결)
    // DataManager는 Serializer들을 알고 있어야 하지만, Serializer는 각 데이터 타입에 대한 책임만 가집니다.
    private Dictionary<Type, IBaseDataSerializer> m_DataSerializers = new Dictionary<Type, IBaseDataSerializer>();
    private SchemaManager _schemaManager;

    [Inject]
    public void Initialize(IDatabaseAccess dbAccess, SchemaManager schemaManager)
    {
        // NullReferenceException 방지를 위한 방어 코드 추가
        if (m_DataSerializers == null)
        {
            m_DataSerializers = new Dictionary<Type, IBaseDataSerializer>();
            Debug.LogWarning("[DataManager] m_DataSerializers was null upon Initialize, forced re-initialization. Check MonoBehaviour lifecycle and VContainer setup.");
        }

        _dbAccess = dbAccess ?? throw new ArgumentNullException(nameof(dbAccess));
        _schemaManager = schemaManager ?? throw new ArgumentNullException(nameof(schemaManager));

        try
        {
            _dbAccess.OpenConnection();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[DataManager] FATAL ERROR: Failed to open database connection during initialization: {ex.Message}");
            throw;
        }

        InitializeDatabaseTables();

        RegisterSerializer<PlayerStatsData>(new PlayerStatsSerializer());
        RegisterSerializer<GameProgressData>(new GameProgressSerializer());
        LoadAllGameData();

        Debug.Log("[DataManager] DataManager Initialized successfully.");
    }

    private void RegisterSerializer<T>(IDataSerializer<T> serializer) where T : class
    {
        if (serializer == null) throw new ArgumentNullException(nameof(serializer));
        Type dataType = typeof(T);
        if (m_DataSerializers.ContainsKey(dataType))
        {
            Debug.LogWarning($"[DataManager] Serializer for type {dataType.Name} already registered. Overwriting.");
            m_DataSerializers[dataType] = serializer;
        }
        else
        {
            m_DataSerializers.Add(dataType, serializer);
        }
    }

    private IDataSerializer<T> GetSerializer<T>() where T : class
    {
        if (m_DataSerializers.TryGetValue(typeof(T), out IBaseDataSerializer baseSerializer)) // IBaseDataSerializer로 받음
        {
            return (IDataSerializer<T>)baseSerializer; // 캐스팅
        }
        throw new InvalidOperationException($"[DataManager] No data serializer registered for type {typeof(T).Name}.");
    }


    // P12: OnApplicationPause/Focus 처리 미흡 - MonoBehaviour 생명주기 메서드 활용
    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
        {
            Debug.Log("[DataManager] OnApplicationPause: App going to background. Closing DB connection.");
            // 앱이 백그라운드로 갈 때 데이터베이스 연결을 닫습니다.
            // 트랜잭션이 남아있을 경우 롤백 처리됩니다.
            _dbAccess?.CloseConnection();
        }
        else
        {
            Debug.Log("[DataManager] OnApplicationPause: App coming to foreground. Opening DB connection.");
            // 앱이 포그라운드로 돌아올 때 데이터베이스 연결을 다시 엽니다.
            try
            {
                _dbAccess?.OpenConnection();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DataManager] Failed to re-open database connection on app resume: {ex.Message}");
                // 연결 재개 실패는 심각하므로, 사용자에게 알리거나 게임을 다시 시작하게 해야 합니다.
                // TODO: UI에 오류 메시지 표시 또는 강제 종료 로직 추가
            }
        }
    }

    private void OnDestroy()
    {
        // P12: OnApplicationPause/Focus 처리 미흡 - 오브젝트 파괴 시 연결 닫기
        // 게임 종료 시 확실하게 연결을 닫고 자원을 해제합니다.
        Debug.Log("[DataManager] OnDestroy: Closing DB connection.");
        _dbAccess?.CloseConnection();
    }


    // P17: SQL 식별자 문자열 삽입 취약 - 이 부분은 InitializeDatabaseTables에서 사용되므로,
    // 스키마 쿼리 자체는 동적이지만, 외부 입력에 의한 조작이 아니므로 일단 허용.
    // 하지만, 이 부분도 SchemaManager 같은 별도 클래스로 분리하여 관리하는 것이 좋습니다.
    private void LoadSQLQueries()
    {
        TextAsset sqlJson = Resources.Load<TextAsset>("SQLSchemas");
        if (sqlJson == null)
        {
            Debug.LogError("[DataManager] Resources/SQLSchemas.json file not found! Database tables might not be initialized correctly.");
            m_SQLQueries = new Dictionary<string, string>();
            return;
        }
        m_SQLQueries = JsonConvert.DeserializeObject<Dictionary<string, string>>(sqlJson.text);
        Debug.Log($"[DataManager] Loaded {m_SQLQueries.Count} SQL schema queries.");
    }

   // --- START OF FILE DataManager.cs (부분 수정) ---
// ... (DataManager 클래스 상단 및 Initialize 메서드는 동일) ...

    private void InitializeDatabaseTables()
    {
        if (_dbAccess == null)
        {
            Debug.LogError("[DataManager] DatabaseAccess is not initialized. Call Initialize() first.");
            return;
        }
        if (_schemaManager == null)
        {
            Debug.LogError("[DataManager] SchemaManager is not initialized. Call Initialize() first.");
            return;
        }

        try
        {
            foreach (var query in _schemaManager.GetAllTableCreateQueries())
            {
                if (string.IsNullOrWhiteSpace(query))
                {
                    Debug.LogWarning("[DataManager] Skipping null or empty schema query from SchemaManager.");
                    continue;
                }

                _dbAccess.ExecuteNonQuery(query);

                // 테이블 이름 추출 로직을 SchemaManager의 정규식을 활용하거나,
                // 좀 더 견고하게 변경합니다. 여기서는 SchemaManager의 Regex를 활용하는 대신,
                // 단순화를 위해 다시 Regex.Match를 사용합니다.
                string tableNameForLog = "Unknown Table";
                Regex TableNameRegex = new Regex(@"CREATE TABLE (IF NOT EXISTS )?(?<TableName>\w+)", RegexOptions.IgnoreCase);
                Match match = TableNameRegex.Match(query);
                if (match.Success)
                {
                    tableNameForLog = match.Groups["TableName"].Value;
                }
                Debug.Log($"[DataManager] Executed table creation query for: {tableNameForLog}");
            }
            Debug.Log("[DataManager] Database tables are verified using SchemaManager schemas.");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[DataManager] Error initializing database tables: {ex.Message}");
            throw new InvalidOperationException("Failed to initialize database tables.", ex);
        }
    }


    public void LoadAllGameData()
    {
        if (_dbAccess == null)
        {
            Debug.LogError("[DataManager] DatabaseAccess is not initialized.");
            return;
        }

        try
        {
            // P28: 로깅 부족 (로딩) - 로깅 강화
            Debug.Log("[DataManager] Attempting to load all game data to check save state...");

            // 플레이어 스탯 데이터를 로드하여 저장 데이터 존재 여부 확인
            var playerStatsSerializer = GetSerializer<PlayerStatsData>();
            var loadedPlayerStatsMap = _dbAccess.SelectWhere(
                playerStatsSerializer.GetTableName(),
                new string[] { playerStatsSerializer.GetPrimaryKeyColumnName() },
                new string[] { "=" },
                new object[] { playerStatsSerializer.GetPrimaryKeyDefaultValue() }
            );

            HasSaveData = (loadedPlayerStatsMap != null && loadedPlayerStatsMap.Count > 0);

            if (HasSaveData)
            {
                Debug.Log("[DataManager] Save data found. HasSaveData = true.");
                // 실제 게임 데이터 객체 로딩 및 상태 업데이트
                var playerStats = playerStatsSerializer.Deserialize(loadedPlayerStatsMap.FirstOrDefault());
                if (playerStats != null)
                {
                    Debug.Log($"[DataManager] Loaded PlayerStats: SaveSlotId={playerStats.SaveSlotID}");
                    // TODO: GameManager/PlayerManager 등에 로드된 데이터 전달
                }

                var gameProgressSerializer = GetSerializer<GameProgressData>();
                var loadedGameProgressMap = _dbAccess.SelectWhere(
                    gameProgressSerializer.GetTableName(),
                    new string[] { gameProgressSerializer.GetPrimaryKeyColumnName() },
                    new string[] { "=" },
                    new object[] { gameProgressSerializer.GetPrimaryKeyDefaultValue() }
                );
                var gameProgress = gameProgressSerializer.Deserialize(loadedGameProgressMap.FirstOrDefault());
                if (gameProgress != null)
                {
                    Debug.Log($"[DataManager] Loaded GameProgress: CurrentDay={gameProgress.CurrentDay}, LastScene={gameProgress.LastSceneName}");
                    // TODO: GameManager/GameFlowManager 등에 로드된 데이터 전달
                }
            }
            else
            {
                Debug.Log("[DataManager] No save data found. HasSaveData = false.");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[DataManager] Error during LoadAllGameData: {ex.Message}");
            HasSaveData = false; // 로딩 실패 시 저장 데이터 없음으로 처리
        }
    }

    // P14, P15, P16, P27: 세이브 큐 도입 및 로깅 강화
    // IDataService 인터페이스에 SaveAllGameData는 현재 정의되어 있지 않으므로,
    // 필요하다면 인터페이스에 추가하거나 다른 방식으로 처리해야 합니다.
    public void SaveAllGameData()
    {
        // TODO: 저장할 실제 게임 데이터를 매개변수로 받거나, 다른 Manager들로부터 데이터를 조회하여 Serialize해야 합니다.
        // 현재는 예시 데이터로 진행
        var playerStatsToSave = new PlayerStatsData();
        var gameProgressToSave = new GameProgressData();

        Debug.Log("[DataManager] Enqueuing SaveAllGameData request...");
        m_SaveQueue.Enqueue(async () => await PerformSaveOperation(playerStatsToSave, gameProgressToSave));
        ProcessSaveQueue(); // 큐 처리 시작 (이미 처리 중이면 아무것도 안함)
    }

   private async Task PerformSaveOperation(PlayerStatsData playerStats, GameProgressData gameProgress)
{
    if (_dbAccess == null)
    {
        Debug.LogError("[DataManager] FATAL ERROR: DatabaseAccess is null during save operation.");
        return;
    }

    try
    {
        // P20: await Task.Run()을 사용하여 모든 데이터베이스 작업을 백그라운드 스레드에서 실행
        await Task.Run(() =>
        {
            _dbAccess.BeginTransaction(); // P10, P13: 트랜잭션 시작

            // 플레이어 스탯 저장
            var playerStatsSerializer = GetSerializer<PlayerStatsData>();
            var playerStatsMap = playerStatsSerializer.Serialize(playerStats);
            string psTableName = playerStatsSerializer.GetTableName();
            string psPrimaryKeyCol = playerStatsSerializer.GetPrimaryKeyColumnName();
            object psPrimaryKeyValue = playerStatsSerializer.GetPrimaryKeyDefaultValue();

            var existingPlayerStats = _dbAccess.SelectWhere(psTableName, new string[] { psPrimaryKeyCol }, new string[] { "=" }, new object[] { psPrimaryKeyValue });
            if (existingPlayerStats != null && existingPlayerStats.Count > 0)
            {
                _dbAccess.UpdateSet(psTableName, playerStatsMap.Keys.ToArray(), playerStatsMap.Values.ToArray(), psPrimaryKeyCol, psPrimaryKeyValue);
                Debug.Log($"[DataManager] Updated PlayerStats for SaveSlotID {psPrimaryKeyValue}.");
            }
            else
            {
                _dbAccess.InsertInto(psTableName, playerStatsMap.Keys.ToArray(), playerStatsMap.Values.ToArray());
                Debug.Log($"[DataManager] Inserted new PlayerStats for SaveSlotID {psPrimaryKeyValue}.");
            }

            // 게임 진행 상황 저장
            var gameProgressSerializer = GetSerializer<GameProgressData>();
            var gameProgressMap = gameProgressSerializer.Serialize(gameProgress);
            string gpTableName = gameProgressSerializer.GetTableName();
            string gpPrimaryKeyCol = gameProgressSerializer.GetPrimaryKeyColumnName();
            object gpPrimaryKeyValue = gameProgressSerializer.GetPrimaryKeyDefaultValue();

            var existingGameProgress = _dbAccess.SelectWhere(gpTableName, new string[] { gpPrimaryKeyCol }, new string[] { "=" }, new object[] { gpPrimaryKeyValue });
            if (existingGameProgress != null && existingGameProgress.Count > 0)
            {
                _dbAccess.UpdateSet(gpTableName, gameProgressMap.Keys.ToArray(), gameProgressMap.Values.ToArray(), gpPrimaryKeyCol, gpPrimaryKeyValue);
                Debug.Log($"[DataManager] Updated GameProgress for SaveSlotID {gpPrimaryKeyValue}.");
            }
            else
            {
                _dbAccess.InsertInto(gpTableName, gameProgressMap.Keys.ToArray(), gameProgressMap.Values.ToArray());
                Debug.Log($"[DataManager] Inserted new GameProgress for SaveSlotID {gpPrimaryKeyValue}.");
            }

            _dbAccess.CommitTransaction(); // P10, P13: 트랜잭션 커밋
            // P21: Unity의 Debug.Log는 메인 스레드에서만 안전하게 호출되므로,
            //       Task.Run() 블록 내부에서는 로깅을 피하는 것이 좋습니다.
            //       하지만 여기서는 디버그 목적으로 포함했습니다.
        });

        // P22: Task.Run()이 완료된 후 메인 스레드에서 상태를 업데이트하고 로그를 남깁니다.
        HasSaveData = true;
        Debug.Log("[DataManager] All game data saved successfully via transaction.");
    }
    catch (Exception ex)
    {
        Debug.LogError($"[DataManager] Failed to save all game data: {ex.Message}. Rolling back transaction.");
        // P23: Task.Run() 내부에서 발생하는 예외는 자동으로 바깥 try-catch 블록으로 전달됩니다.
        _dbAccess.RollbackTransaction(); // 오류 발생 시 롤백
        throw; // 예외를 다시 던져 상위 호출자가 알 수 있도록 합니다.
    }
}

    private async void ProcessSaveQueue()
    {
        if (m_IsProcessingSaveQueue)
        {
            return; // 이미 처리 중이면 다시 시작하지 않음
        }

        m_IsProcessingSaveQueue = true;
        Debug.Log("[DataManager] Starting to process save queue...");

        while (m_SaveQueue.TryDequeue(out Func<Task> saveOperation))
        {
            try
            {
                await saveOperation(); // 비동기 저장 작업 실행
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DataManager] Error processing save queue item: {ex.Message}. Remaining items in queue: {m_SaveQueue.Count}");
                // TODO: 오류 발생 시 사용자에게 알리거나, 재시도 로직 구현 (P11: 재시도 로직 부재)
            }
        }

        m_IsProcessingSaveQueue = false;
        Debug.Log("[DataManager] Save queue processing finished.");
    }


    // 새 게임 시작 시 데이터를 초기화하는 메서드 (IDataService에 포함)


    // --- 범용 CRUD 메서드 (IDataService 인터페이스 메서드 구현) ---
    // 이 메서드들은 DataManager가 직접 사용하는 것보다,
    // IDataSerializer를 통해 특정 데이터 타입을 다루는 상위 계층에서 사용하는 것이 일반적입니다.
    // DataManager는 주로 IDataSerializer가 호출하는 하위 레이어의 역할을 합니다.

    public List<Dictionary<string, object>> LoadData(string tableName, string whereCol, object whereValue)
    {
        if (_dbAccess == null)
        {
            Debug.LogError("[DataManager] DatabaseAccess is not initialized for LoadData.");
            return null;
        }
        Debug.Log($"[DataManager] Loading data from {tableName} where {whereCol} = {whereValue}.");
        return _dbAccess.SelectWhere(tableName, new string[] { whereCol }, new string[] { "=" }, new object[] { whereValue });
    }

    public void InsertData(string tableName, string[] columns, object[] values)
    {
        if (_dbAccess == null)
        {
            Debug.LogError("[DataManager] DatabaseAccess is not initialized for SaveData.");
            return;
        }
        Debug.Log($"[DataManager] Saving data to {tableName}.");
        // 이 SaveData는 IDataService의 일부이지만, 실제로는 PerformSaveOperation과 같은
        // 트랜잭션 기반 메서드를 사용하는 것이 더 안전합니다.
        // 단일 Insert 작업만 수행합니다.
        _dbAccess.InsertInto(tableName, columns, values);
    }

    public void UpdateData(string tableName, string[] updateCols, object[] updateValues, string whereCol, object whereValue)
    {
        if (_dbAccess == null)
        {
            Debug.LogError("[DataManager] DatabaseAccess is not initialized for UpdateData.");
            return;
        }
        Debug.Log($"[DataManager] Updating data in {tableName} where {whereCol} = {whereValue}.");
        // 단일 Update 작업만 수행합니다.
        _dbAccess.UpdateSet(tableName, updateCols, updateValues, whereCol, whereValue);
    }

    public void DeleteData(string tableName, string whereCol, object whereValue)
    {
        if (_dbAccess == null)
        {
            Debug.LogError("[DataManager] DatabaseAccess is not initialized for DeleteData.");
            return;
        }
        Debug.Log($"[DataManager] Deleting data from {tableName} where {whereCol} = {whereValue}.");
        _dbAccess.DeleteWhere(tableName, whereCol, whereValue);
    }
}
// --- END OF FILE dataManager.txt ---