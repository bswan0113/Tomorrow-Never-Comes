// C:\Workspace\Tomorrow Never Comes\Assets\Scripts\Features\Player\PlayerDataManager.cs (REFACTORED)

using System;
using System.Threading.Tasks;
using Core.Data.Interface;
using Core.Interface;
using Core.Logging;
using VContainer; // VContainer의 Inject 속성
using VContainer.Unity; // IAsyncStartable
using Cysharp.Threading.Tasks; // UniTask

namespace Features.Player
{
    // MonoBehaviour 제거, IAsyncStartable 구현 (비동기 초기화를 위해)
    public class PlayerDataManager : IPlayerService, IAsyncStartable
    {
        private readonly IPlayerStatsRepository _playerStatsRepository;

        private PlayerStatsData m_currentPlayerStats;

        public event Action OnPlayerStatsChanged;

        // 생성자 주입 방식으로 변경
        [Inject]
        public PlayerDataManager(IPlayerStatsRepository repository)
        {
            _playerStatsRepository = repository ?? throw new ArgumentNullException(nameof(repository));
            CoreLogger.Log("[PlayerDataManager] Constructed via VContainer constructor injection.");
        }

        // VContainer가 모든 주입이 끝난 후 호출하는 비동기 초기화 메서드
        public async UniTask StartAsync(System.Threading.CancellationToken cancellation)
        {
            // Construct 메서드에 있던 초기 로딩 로직을 이곳으로 이동
            await LoadPlayerDataAsync();
        }

        public PlayerStatsData GetCurrentPlayerStats()
        {
            if (m_currentPlayerStats == null)
            {
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

        public async Task LoadPlayerDataAsync()
        {
            CoreLogger.Log("[PlayerDataManager] Attempting to load player data...");
            try
            {
                // CancellationToken을 사용하는 UniTask 버전의 API가 있다면 더 좋습니다.
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
                // 로드가 성공하든 실패하든, 상태 변경을 알립니다.
                OnPlayerStatsChanged?.Invoke();
            }
        }

        public async Task SavePlayerDataAsync()
        {
            if (m_currentPlayerStats == null)
            {
                CoreLogger.LogWarning("[PlayerDataManager] Attempted to save null player data. Initializing with default.");
                m_currentPlayerStats = new PlayerStatsData();
            }

            CoreLogger.Log($"[PlayerDataManager] Saving player data for SlotID {m_currentPlayerStats.SaveSlotID}...");
            try
            {
                await _playerStatsRepository.SavePlayerStatsAsync(m_currentPlayerStats);
                CoreLogger.Log("[PlayerDataManager] Player data saved successfully.");
            }
            catch (Exception ex)
            {
                CoreLogger.LogError($"[PlayerDataManager] Failed to save player data: {ex.Message}");
            }
        }
    }
}