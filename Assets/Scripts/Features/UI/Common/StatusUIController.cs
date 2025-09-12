using Core.Interface;
using Core.Logging;
using TMPro;
using UnityEngine;
using VContainer;

namespace Features.UI.Common
{
    public class StatusUIController : MonoBehaviour
    {
        [Header("Game State UI")]
        [SerializeField] private TextMeshProUGUI dayText;
        [SerializeField] private TextMeshProUGUI actionPointText;

        [Header("Player Stats UI")]
        [SerializeField] private TextMeshProUGUI intellectText;
        [SerializeField] private TextMeshProUGUI charmText;

        private IPlayerService _playerService;
        private IGameService _gameService;

        [Inject]
        public void Construct(IPlayerService playerService, IGameService gameService)
        {
            _playerService = playerService ?? throw new System.ArgumentNullException(nameof(playerService));
            _gameService = gameService ?? throw new System.ArgumentNullException(nameof(gameService));

            CoreLogger.LogDebug($"{gameObject.name}: 서비스 주입 완료."); // Debug 레벨로 변경
        }

        // OnEnable에서 Start로 이벤트 구독 시점을 변경합니다.
        // OnEnable 시점에는 [Inject]가 완료되지 않았을 수 있기 때문입니다.
        // OnDisable은 여전히 구독 해지 역할을 합니다.
        private void OnDisable()
        {
            if (_gameService != null)
            {
                _gameService.OnDayStart -= UpdateDayUI;
                _gameService.OnActionPointChanged -= UpdateActionPointUI;
                CoreLogger.LogDebug("[StatusUIController] GameService 이벤트 구독 해제.");
            }

            if (_playerService != null)
            {
                _playerService.OnPlayerStatusChanged -= UpdatePlayerStatsUI;
                CoreLogger.LogDebug("[StatusUIController] PlayerService 이벤트 구독 해제.");
            }
        }

        private void Start()
        {
            CoreLogger.LogDebug("[StatusUIController] Start - 초기화.");

            // Start 시점에 서비스 주입이 완료되었으므로, 이제 안전하게 이벤트를 구독합니다.
            if (_gameService != null)
            {
                _gameService.OnDayStart += UpdateDayUI;
                _gameService.OnActionPointChanged += UpdateActionPointUI;
                CoreLogger.LogDebug("[StatusUIController] GameService 이벤트 구독.");
            }
            else
            {
                CoreLogger.LogWarning("[StatusUIController] _gameService가 null입니다. GameService 이벤트 구독을 건너뜜.");
            }

            if (_playerService != null)
            {
                _playerService.OnPlayerStatusChanged += UpdatePlayerStatsUI;
                CoreLogger.LogDebug("[StatusUIController] PlayerService 이벤트 구독.");
            }
            else
            {
                CoreLogger.LogWarning("[StatusUIController] _playerService가 null입니다. PlayerService 이벤트 구독을 건너뜜.");
            }

            // Start에서 모든 UI 업데이트를 호출하여 초기 상태 반영
            UpdateAllUI();
        }

        /// <summary>
        /// 모든 UI 요소를 업데이트하는 통합 메서드.
        /// </summary>
        private void UpdateAllUI()
        {
            UpdateDayUI();
            UpdateActionPointUI();
            UpdatePlayerStatsUI();
        }

        private void UpdateDayUI()
        {
            if (dayText != null && _gameService != null)
            {
                dayText.text = $"DAY {_gameService.DayCount}";
            }
        }

        private void UpdateActionPointUI()
        {
            if (actionPointText != null && _gameService != null)
            {
                actionPointText.text = $"행동력: {_gameService.CurrentActionPoint}";
            }
        }

        private void UpdatePlayerStatsUI()
        {
            if (_playerService == null || _playerService.StatsData == null)
            {
                return;
            }

            if (intellectText != null)
            {
                intellectText.text = $"지능: {_playerService.StatsData.Intellect}";
            }

            if (charmText != null)
            {
                charmText.text = $"매력: {_playerService.StatsData.Charm}";
            }
        }
    }
}