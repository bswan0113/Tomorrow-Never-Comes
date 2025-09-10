using Core.Interface;
using UnityEngine;
using TMPro;
using VContainer;

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
        _playerService = playerService;
        _gameService = gameService;

        Debug.Log($"{gameObject.name}: 서비스 주입 완료");
    }

    private void OnEnable()
    {
        if (_gameService != null)
        {
            _gameService.OnDayStart += UpdateDayUI;
            _gameService.OnActionPointChanged += UpdateActionPointUI;
        }

        if (_playerService != null)
        {
            _playerService.OnPlayerStatusChanged += UpdatePlayerStatsUI;
        }
    }

    private void OnDisable()
    {
        if (_gameService != null)
        {
            _gameService.OnDayStart -= UpdateDayUI;
            _gameService.OnActionPointChanged -= UpdateActionPointUI;
        }

        if (_playerService != null)
        {
            _playerService.OnPlayerStatusChanged -= UpdatePlayerStatsUI;
        }
    }

    private void Start()
    {
        Debug.Log("StatusUIController Initializing...");

        if (_gameService != null)
        {
            UpdateDayUI();
            UpdateActionPointUI();
        }

        if (_playerService != null)
        {
            UpdatePlayerStatsUI();
        }
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
        if (_playerService == null || _playerService.Status == null)
            return;

        if (intellectText != null)
        {
            intellectText.text = $"지능: {_playerService.Status.Intellect}";
        }

        if (charmText != null)
        {
            charmText.text = $"매력: {_playerService.Status.Charm}";
        }
    }
}