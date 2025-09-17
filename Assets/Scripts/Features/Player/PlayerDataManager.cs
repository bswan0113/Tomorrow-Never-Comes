// C:\Workspace\Tomorrow Never Comes\Assets\Scripts\Features\Player\PlayerDataManager.cs (REFACTORED)

using System;
using System.Threading.Tasks;
using Core.Data.Interface;
using Core.Interface;
using Core.Logging;
using VContainer;
using VContainer.Unity;
using Cysharp.Threading.Tasks;

namespace Features.Player
{
    /// <summary>
    /// 플레이어의 스탯 데이터를 메모리에서 관리하고, 로드 및 변경 로직을 처리합니다.
    /// 데이터 저장은 DataManager를 통해 이루어지므로, 저장 책임은 없습니다.
    /// </summary>
    public class PlayerDataManager : IPlayerService
    {
        private readonly IPlayerStatsRepository _playerStatsRepository;

        private PlayerStatsData m_currentPlayerStats;

        public event Action OnPlayerStatsChanged;

        [Inject]
        public PlayerDataManager(IPlayerStatsRepository repository)
        {
            _playerStatsRepository = repository ?? throw new ArgumentNullException(nameof(repository));
            CoreLogger.Log("[PlayerDataManager] Constructed via VContainer constructor injection.");
        }

        // public async UniTask StartAsync(System.Threading.CancellationToken cancellation)
        // {
        //     // 게임 시작 시 자신의 상태를 초기화하기 위해 데이터를 로드합니다.
        //     await LoadPlayerDataAsync();
        // }

        /// <summary>
        /// 현재 메모리에 있는 PlayerStatsData를 반환합니다.
        /// DataManager가 저장을 위해 이 메서드를 호출할 것입니다.
        /// </summary>
        public PlayerStatsData GetCurrentPlayerStats()
        {
            if (m_currentPlayerStats == null)
            {
                // 로드가 실패했거나 데이터가 없는 경우, 기본값으로 새 인스턴스를 생성합니다.
                m_currentPlayerStats = new PlayerStatsData();
                CoreLogger.LogWarning("[PlayerDataManager] PlayerStatsData was null, initialized with default values.");
            }
            return m_currentPlayerStats;
        }

        public void AddIntellect(int intellect)
        {
            if (m_currentPlayerStats == null) GetCurrentPlayerStats();
            m_currentPlayerStats.Intellect += intellect;
            CoreLogger.Log($"[PlayerDataManager] Intellect updated to: {m_currentPlayerStats.Intellect}");
            OnPlayerStatsChanged?.Invoke();
        }

        public void AddCharm(int charm)
        {
            if (m_currentPlayerStats == null) GetCurrentPlayerStats();
            m_currentPlayerStats.Charm += charm;
            CoreLogger.Log($"[PlayerDataManager] Charm updated to: {m_currentPlayerStats.Charm}");
            OnPlayerStatsChanged?.Invoke();
        }

        public void AddMoney(long amount)
        {
            if (m_currentPlayerStats == null) GetCurrentPlayerStats();
            m_currentPlayerStats.Money += amount;
            CoreLogger.Log($"[PlayerDataManager] Money updated to: {m_currentPlayerStats.Money}");
            OnPlayerStatsChanged?.Invoke();
        }

        /// <summary>
        /// Repository를 통해 PlayerStatsData를 비동기적으로 로드합니다.
        /// 이 작업은 PlayerDataManager의 내부 책임입니다.
        /// </summary>
        public async Task LoadPlayerDataAsync()
        {
            CoreLogger.Log("[PlayerDataManager] Attempting to load player data...");
            try
            {
                m_currentPlayerStats = await _playerStatsRepository.LoadPlayerStatsAsync(1);
                if (m_currentPlayerStats != null)
                {
                    CoreLogger.Log($"[PlayerDataManager] Player data loaded for SlotID {m_currentPlayerStats.SaveSlotID}. Intellect: {m_currentPlayerStats.Intellect}, Money: {m_currentPlayerStats.Money}");
                }
                else
                {
                    m_currentPlayerStats = new PlayerStatsData();
                    CoreLogger.Log("[PlayerDataManager] No existing player data found, created new default data.");
                }
            }
            catch (Exception ex)
            {
                CoreLogger.LogError($"[PlayerDataManager] Error loading player data: {ex.Message}. Initializing with default data.");
                m_currentPlayerStats = new PlayerStatsData();
            }
            finally
            {
                OnPlayerStatsChanged?.Invoke();
            }
        }

        // SavePlayerDataAsync 메서드를 완전히 제거합니다.
        // 이 책임은 DataManager로 이전되었습니다.
        // public async Task SavePlayerDataAsync() { ... }
    }
}