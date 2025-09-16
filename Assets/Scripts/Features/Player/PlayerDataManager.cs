// C:\Workspace\Tomorrow Never Comes\Assets\Scripts\Features\Player\PlayerDataManager.cs (최종 수정 권장)

using System;
using System.Threading.Tasks;
using Core.Data.Interface;
using Core.Interface;
using Core.Logging;
using UnityEngine;
using VContainer; // VContainer의 Inject 속성을 사용하기 위해 추가

namespace Features.Player
{
    public class PlayerDataManager : MonoBehaviour, IPlayerService
    {
        private IPlayerStatsRepository _playerStatsRepository; // readonly 제거 (Construct 메서드에서 할당)

        private PlayerStatsData m_currentPlayerStats;

        public event Action OnPlayerStatsChanged;

        // MonoBehaviour는 기본적으로 매개변수 없는 생성자를 요구합니다.
        // VContainer를 통해 인스턴스화되거나 씬에 미리 배치된 경우,
        // 아래의 [Inject] Construct 메서드를 통해 의존성이 주입됩니다.
        public PlayerDataManager() { } // Unity가 호출할 기본 생성자 (필요시)

        // VContainer를 통해 의존성을 주입받는 메서드입니다.
        // Construct라는 이름은 관례이며, 다른 이름을 사용해도 됩니다.
        [Inject] // VContainer가 이 메서드를 호출하여 의존성을 주입합니다.
        public void Construct(IPlayerStatsRepository repository)
        {
            _playerStatsRepository = repository ?? throw new ArgumentNullException(nameof(repository));
            CoreLogger.Log("[PlayerDataManager] Initialized via VContainer [Inject] Construct method.");

            // 초기 플레이어 스탯 로드 시작. Construct는 비동기가 아니므로 await 없이 Task를 시작합니다.
            _ = LoadPlayerDataAsync();
        }

        // 기존 Initialize 메서드는 Construct로 통합되었으므로 제거합니다.
        // public void Initialize(IPlayerStatsRepository repository) { ... }

        public PlayerStatsData GetCurrentPlayerStats()
        {
            if (m_currentPlayerStats == null)
            {
                m_currentPlayerStats = new PlayerStatsData();
                CoreLogger.LogWarning("[PlayerDataManager] PlayerStatsData was null, initialized with default values.");
            }
            return m_currentPlayerStats;
        }

        public void AddIntellect(int Intellect)
        {
            if (m_currentPlayerStats == null) GetCurrentPlayerStats();
            m_currentPlayerStats.Intellect += Intellect;
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
            if (_playerStatsRepository == null) // Construct가 호출되기 전에 호출될 경우 대비
            {
                CoreLogger.LogError("[PlayerDataManager] Repository is not initialized. Cannot load player data.");
                m_currentPlayerStats = new PlayerStatsData(); // 기본값으로 초기화
                OnPlayerStatsChanged?.Invoke();
                return;
            }

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
                OnPlayerStatsChanged?.Invoke();
            }
            catch (Exception ex)
            {
                CoreLogger.LogError($"[PlayerDataManager] Error loading player data: {ex.Message}. Initializing with default data.");
                m_currentPlayerStats = new PlayerStatsData();
                OnPlayerStatsChanged?.Invoke();
            }
        }

        public async Task SavePlayerDataAsync()
        {
            if (_playerStatsRepository == null) // Construct가 호출되기 전에 호출될 경우 대비
            {
                CoreLogger.LogError("[PlayerDataManager] Repository is not initialized. Cannot save player data.");
                return;
            }

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
                OnPlayerStatsChanged?.Invoke();
            }
            catch (Exception ex)
            {
                CoreLogger.LogError($"[PlayerDataManager] Failed to save player data: {ex.Message}");
            }
        }
    }
}