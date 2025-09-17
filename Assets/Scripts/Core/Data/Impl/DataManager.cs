// C:\Workspace\Tomorrow Never Comes\DataManager.cs (REVISED AND CORRECTED)

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Core.Data.Interface;
using Core.Logging;
using Cysharp.Threading.Tasks; // UniTask
using Features.Data;
using Features.Player;
using VContainer;
using VContainer.Unity;

namespace Core.Data.Impl
{
    public class DataManager : IDataService
    {
        private readonly IDatabaseAccess _dbAccess;
        private readonly IGameProgressRepository _gameProgressRepository;
        private readonly IPlayerStatsRepository _playerStatsRepository;

        private readonly ConcurrentQueue<Func<Task>> m_SaveQueue = new ConcurrentQueue<Func<Task>>();
        private bool m_IsProcessingSaveQueue = false;

        public bool HasSaveData { get; private set; } = false;

        // 순환 참조를 유발하는 IPlayerService, IGameService 의존성 제거
        [Inject]
        public DataManager(IDatabaseAccess dbAccess,
            IGameProgressRepository gameProgressRepository,
            IPlayerStatsRepository playerStatsRepository)
        {
            _dbAccess = dbAccess ?? throw new ArgumentNullException(nameof(dbAccess));
            _gameProgressRepository = gameProgressRepository ?? throw new ArgumentNullException(nameof(gameProgressRepository));
            _playerStatsRepository = playerStatsRepository ?? throw new ArgumentNullException(nameof(playerStatsRepository));
            CoreLogger.Log("[DataManager] Constructed via VContainer constructor injection.");
        }

        // public async UniTask StartAsync(CancellationToken cancellation)
        // {
        //     CoreLogger.Log("[DataManager] DataManager starting asynchronously...");
        //     await CheckSaveDataAsync();
        //     CoreLogger.Log("[DataManager] DataManager Initialized successfully.");
        // }

        public async Task CheckSaveDataAsync(int saveSlotId = 1)
        {
            try
            {
                CoreLogger.Log("[DataManager] Checking for existing save data...");
                HasSaveData = await _playerStatsRepository.HasPlayerStatsDataAsync(saveSlotId);
                CoreLogger.Log($"[DataManager] Save data check complete. HasSaveData = {HasSaveData}.");
            }
            catch (Exception ex)
            {
                CoreLogger.LogError($"[DataManager] Error during CheckSaveDataAsync: {ex.Message}");
                HasSaveData = false;
            }
        }

        /// <summary>
        /// 여러 게임 데이터를 하나의 원자적 트랜잭션으로 묶어 저장하도록 큐에 작업을 추가합니다.
        /// </summary>
        public Task SaveAllGameData(GameProgressData gameProgress, PlayerStatsData playerStats)
        {
            if (gameProgress == null) throw new ArgumentNullException(nameof(gameProgress));
            if (playerStats == null) throw new ArgumentNullException(nameof(playerStats));

            CoreLogger.Log($"[DataManager] Enqueuing SaveAllGameData request for Slot {gameProgress.SaveSlotID}...");
            m_SaveQueue.Enqueue(() => PerformSaveOperationInTransaction(gameProgress, playerStats));
            ProcessSaveQueue();
            return Task.CompletedTask;
        }

        /// <summary>
        /// 트랜잭션 내에서 모든 데이터 저장 작업을 수행합니다.
        /// </summary>
        private async Task PerformSaveOperationInTransaction(GameProgressData gameProgress, PlayerStatsData playerStats)
        {
            CoreLogger.Log($"[DataManager] Performing atomic save for Slot {gameProgress.SaveSlotID}.");
            ITransaction transaction = null;
            try
            {
                transaction = await _dbAccess.BeginTransactionAsync();

                // 전달받은 데이터를 사용하여 각 리포지토리에 저장 요청
                await _playerStatsRepository.SavePlayerStatsAsync(playerStats, transaction);
                await _gameProgressRepository.SaveGameProgressAsync(gameProgress, transaction);

                await transaction.CommitAsync();
                CoreLogger.Log($"[DataManager] Save operations for Slot {gameProgress.SaveSlotID} committed successfully.");
                HasSaveData = true;
            }
            catch (Exception ex)
            {
                CoreLogger.LogError($"[DataManager] Error during transactional save for Slot {gameProgress.SaveSlotID}. Attempting rollback: {ex.Message}");
                if (transaction != null)
                {
                    await transaction.RollbackAsync();
                }
                throw;
            }
            finally
            {
                transaction?.Dispose();
            }
        }

        private async void ProcessSaveQueue()
        {
            if (m_IsProcessingSaveQueue) return;

            m_IsProcessingSaveQueue = true;
            CoreLogger.Log("[DataManager] Starting to process save queue...");

            while (m_SaveQueue.TryDequeue(out var saveOperation))
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

        /// <summary>
        /// 단일 데이터 엔티티를 원자적으로 저장합니다.
        /// </summary>
        public async Task SaveDataAsync<T>(T data) where T : class
        {
            ITransaction transaction = null;
            try
            {
                transaction = await _dbAccess.BeginTransactionAsync();

                if (data is GameProgressData gameProgressData)
                {
                    await _gameProgressRepository.SaveGameProgressAsync(gameProgressData, transaction);
                }
                else if (data is PlayerStatsData playerStatsData)
                {
                    await _playerStatsRepository.SavePlayerStatsAsync(playerStatsData, transaction);
                }
                else
                {
                    throw new NotSupportedException($"SaveDataAsync for type {typeof(T).Name} is not supported.");
                }

                await transaction.CommitAsync();
                CoreLogger.Log($"[DataManager] Successfully saved single data of type {typeof(T).Name}.");
            }
            catch(Exception ex)
            {
                CoreLogger.LogError($"[DataManager] Failed to save single data of type {typeof(T).Name}: {ex.Message}");
                if (transaction != null)
                {
                    await transaction.RollbackAsync();
                }
                throw;
            }
            finally
            {
                transaction?.Dispose();
            }
        }

        public async Task DeleteDataAsync<T>(int saveSlotId) where T : class
        {
            // 삭제 작업도 트랜잭션으로 묶는 것이 더 안전할 수 있지만, 현재 요구사항에서는 일단 유지합니다.
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