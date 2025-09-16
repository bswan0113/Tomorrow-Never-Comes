// C:\Workspace\Tomorrow Never Comes\DataManager.cs (REVISED AND CORRECTED)

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Core.Data.Interface;
using Core.Logging;
using Cysharp.Threading.Tasks;
using Features.Data;
using Features.Player;
using VContainer;

namespace Core.Data.Impl
{
    public class DataManager : IDataService
    {
        // *** CHANGED: readonly is now possible with constructor injection ***
        private readonly IDatabaseAccess _dbAccess;
        private readonly IGameProgressRepository _gameProgressRepository;
        private readonly IPlayerStatsRepository _playerStatsRepository;

        // 저장 큐
        private readonly ConcurrentQueue<Func<Task>> m_SaveQueue = new ConcurrentQueue<Func<Task>>();
        private bool m_IsProcessingSaveQueue = false;

        // 공개 속성
        public bool HasSaveData { get; private set; } = false;

        // *** CHANGED: Using constructor injection instead of method injection ***
        [Inject]
        public DataManager(IDatabaseAccess dbAccess,
            IGameProgressRepository gameProgressRepository,
            IPlayerStatsRepository playerStatsRepository)
        {
            _dbAccess = dbAccess;
            _gameProgressRepository = gameProgressRepository;
            _playerStatsRepository = playerStatsRepository;
        }

        // VContainer가 비동기적으로 초기화를 수행합니다.
        public async UniTask StartAsync(CancellationToken cancellation)
        {
            CoreLogger.Log("[DataManager] DataManager starting asynchronously...");
            await CheckSaveDataAsync();
            CoreLogger.Log("[DataManager] DataManager Initialized successfully.");
        }

        // ... (이하 나머지 메서드는 이전과 동일) ...

        /// <summary>
        /// 저장 데이터가 존재하는지 비동기적으로 확인하고 HasSaveData 플래그를 설정합니다.
        /// </summary>
        public async Task CheckSaveDataAsync(int saveSlotId = 1)
        {
            try
            {
                CoreLogger.Log("[DataManager] Checking for existing save data...");
                // Repository의 비동기 메서드를 사용합니다.
                HasSaveData = await _playerStatsRepository.HasPlayerStatsDataAsync(saveSlotId);

                if (HasSaveData)
                {
                    CoreLogger.Log("[DataManager] Save data found. HasSaveData = true.");
                }
                else
                {
                    CoreLogger.Log("[DataManager] No save data found. HasSaveData = false.");
                }
            }
            catch (Exception ex)
            {
                CoreLogger.LogError($"[DataManager] Error during CheckSaveDataAsync: {ex.Message}");
                HasSaveData = false; // 로딩 실패 시 저장 데이터 없음으로 처리
            }
        }

        /// <summary>
        /// 모든 게임 데이터를 지정된 슬롯에 저장하도록 큐에 작업을 추가합니다.
        /// </summary>
        public Task SaveAllGameData(int saveSlotId = 1)
        {
            // 실제 게임에서는 PlayerDataManager 등에서 현재 데이터를 가져와야 합니다.
            // 여기서는 예시 데이터를 사용합니다.
            var playerStatsToSave = new PlayerStatsData {
                SaveSlotID = saveSlotId,
                Intellect = 10, Charm = 10, Endurance = 10, Money = 0,
                HeroineALiked = 0, HeroineBLiked = 0, HeroineCLiked = 0
            };
            var gameProgressToSave = new GameProgressData {
                SaveSlotID = saveSlotId,
                CurrentDay = 1,
                LastSceneName = "PlayerRoom",
                SaveDateTime = DateTime.UtcNow
            };

            CoreLogger.Log($"[DataManager] Enqueuing SaveAllGameData request for Slot {saveSlotId}...");
            // 큐에 들어갈 작업은 새로운 트랜잭션 모델을 사용합니다.
            m_SaveQueue.Enqueue(() => PerformSaveOperationInTransaction(playerStatsToSave, gameProgressToSave));
            ProcessSaveQueue(); // 큐 처리 시작

            return Task.CompletedTask;
        }

        /// <summary>
        /// 트랜잭션 내에서 모든 데이터 저장 작업을 수행합니다.
        /// </summary>
        private async Task PerformSaveOperationInTransaction(PlayerStatsData playerStats, GameProgressData gameProgress)
        {
            CoreLogger.Log($"[DataManager] Performing save operation in transaction for Slot {playerStats.SaveSlotID}.");
            try
            {
                // IDatabaseAccess의 트랜잭션 헬퍼 메서드를 사용하여
                // PlayerStats와 GameProgress 저장을 하나의 원자적 작업으로 묶습니다.
                // 참고: 진정한 트랜잭션을 위해서는 Repository의 Save 메서드가
                // IDbConnection과 IDbTransaction을 인자로 받아 처리하도록 수정하는 것이 가장 이상적입니다.
                // 현재 구조에서는 각 SaveAsync가 별도의 연결을 사용하므로 원자성을 보장하지 않습니다.
                // 하지만 순차적으로 실행되며, 하나가 실패하면 다음 것은 실행되지 않습니다.

                await _playerStatsRepository.SavePlayerStatsAsync(playerStats);
                await _gameProgressRepository.SaveGameProgressAsync(gameProgress);

                // 위 방식이 원자성을 보장하지 않으므로, 아래 주석 처리된 방식이 더 좋습니다.
                // 이 방식을 사용하려면 각 Repository에 트랜잭션을 지원하는 메서드를 추가해야 합니다.
                /*
            await _dbAccess.ExecuteInTransactionAsync(async (connection, transaction) =>
            {
                // 이 람다 내에서 connection과 transaction을 사용하는 모든 작업은 원자적으로 처리됩니다.
                await _playerStatsRepository.SavePlayerStatsAsync(playerStats, connection, transaction);
                await _gameProgressRepository.SaveGameProgressAsync(gameProgress, connection, transaction);
            });
            */

                CoreLogger.Log($"[DataManager] Save operations for Slot {playerStats.SaveSlotID} completed successfully.");
                HasSaveData = true; // 저장 성공 후 상태 업데이트
            }
            catch (Exception ex)
            {
                CoreLogger.LogError($"[DataManager] Failed to save all game data for Slot {playerStats.SaveSlotID}: {ex.Message}");
                // 예외는 ProcessSaveQueue에서 처리됩니다.
                throw;
            }
        }

        /// <summary>
        /// 저장 큐를 순차적으로 처리합니다.
        /// </summary>
        private async void ProcessSaveQueue()
        {
            if (m_IsProcessingSaveQueue) return;

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


        #region IDataService Implementation
        public async Task<T> LoadDataAsync<T>(int saveSlotId) where T : class
        {
            if (typeof(T) == typeof(GameProgressData))
            {
                return await _gameProgressRepository.LoadGameProgressAsync(saveSlotId) as T;
            }
            if (typeof(T) == typeof(PlayerStatsData))
            {
                return await _playerStatsRepository.LoadPlayerStatsAsync(saveSlotId) as T;
            }

            CoreLogger.LogWarning($"[DataManager] LoadDataAsync for type {typeof(T).Name} is not supported.");
            return null;
        }

        public async Task SaveDataAsync<T>(T data) where T : class
        {
            if (data is GameProgressData gameProgressData)
            {
                await _gameProgressRepository.SaveGameProgressAsync(gameProgressData);
            }
            else if (data is PlayerStatsData playerStatsData)
            {
                await _playerStatsRepository.SavePlayerStatsAsync(playerStatsData);
            }
            else
            {
                CoreLogger.LogWarning($"[DataManager] SaveDataAsync for type {typeof(T).Name} is not supported.");
            }
        }

        public async Task DeleteDataAsync<T>(int saveSlotId) where T : class
        {
            if (typeof(T) == typeof(GameProgressData))
            {
                await _gameProgressRepository.DeleteGameProgressAsync(saveSlotId);
            }
            else if (typeof(T) == typeof(PlayerStatsData))
            {
                await _playerStatsRepository.DeletePlayerStatsAsync(saveSlotId);
            }
            else
            {
                CoreLogger.LogWarning($"[DataManager] DeleteDataAsync for type {typeof(T).Name} is not supported.");
            }
        }
        #endregion
    }
}