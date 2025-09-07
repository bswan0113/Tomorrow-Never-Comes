// C:\Workspace\Tomorrow Never Comes\Assets\Scripts\Features\UI\Common\StatusUIController.cs

using UnityEngine;
using TMPro; // TextMeshPro를 사용하기 위해 필수!

public class StatusUIController : MonoBehaviour
{
    [Header("Game State UI")]
    [SerializeField] private TextMeshProUGUI dayText;
    [SerializeField] private TextMeshProUGUI actionPointText;

    // ▼▼▼ 추가 ▼▼▼: 플레이어 스탯 UI
    [Header("Player Stats UI")]
    [SerializeField] private TextMeshProUGUI intellectText;
    [SerializeField] private TextMeshProUGUI charmText;
    // (필요에 따라 다른 스탯 UI들도 여기에 추가)


    // 이 UI 오브젝트가 활성화될 때 자동으로 호출됩니다.
    private void OnEnable()
    {
        // GameManager가 보내는 방송(이벤트)을 구독 시작
        GameManager.OnDayStart += UpdateDayUI;
        GameManager.OnActionPointChanged += UpdateActionPointUI;

        // ▼▼▼ 추가 ▼▼▼: PlayerDataManager의 스탯 변경 방송을 구독 시작
        PlayerDataManager.OnPlayerStatusChanged += UpdatePlayerStatsUI;
    }

    // 비활성화될 때(씬이 바뀌거나 오브젝트가 파괴될 때) 호출됩니다.
    private void OnDisable()
    {
        // 구독을 해제합니다. 이걸 안 하면 메모리 누수나 에러의 원인이 됩니다. 매우 중요!
        GameManager.OnDayStart -= UpdateDayUI;
        GameManager.OnActionPointChanged -= UpdateActionPointUI;

        // ▼▼▼ 추가 ▼▼▼: PlayerDataManager 구독 해제
        PlayerDataManager.OnPlayerStatusChanged -= UpdatePlayerStatsUI;
    }

    // 게임 시작 시, 혹은 이 UI가 처음 나타났을 때 한 번 현재 정보로 초기화
    private void Start()
    {
        Debug.Log("StatusUIController Initializing...");
        // 매니저들이 모두 준비되었는지 확인
        if (GameManager.Instance != null)
        {
            UpdateDayUI();
            UpdateActionPointUI();
        }

        // ▼▼▼ 추가 ▼▼▼: 플레이어 데이터 초기화
        if (PlayerDataManager.Instance != null)
        {
            UpdatePlayerStatsUI();
        }
    }

    // '새 날이 시작됐다'는 방송을 받으면 호출될 메서드
    private void UpdateDayUI()
    {
        if (dayText != null && GameManager.Instance != null)
        {
            dayText.text = $"DAY {GameManager.Instance.DayCount}";
        }
    }

    // '행동력이 변경됐다'는 방송을 받으면 호출될 메서드
    private void UpdateActionPointUI()
    {
        if (actionPointText != null && GameManager.Instance != null)
        {
            actionPointText.text = $"행동력: {GameManager.Instance.CurrentActionPoint}";
        }
    }

    // ▼▼▼ 추가 ▼▼▼: '플레이어 스탯이 변경됐다'는 방송을 받으면 호출될 메서드
    private void UpdatePlayerStatsUI()
    {
        if (PlayerDataManager.Instance == null || PlayerDataManager.Instance.Status == null)
            return;

        if (intellectText != null)
        {
            intellectText.text = $"지능: {PlayerDataManager.Instance.Status.Intellect}";
        }
        if (charmText != null)
        {
            charmText.text = $"매력: {PlayerDataManager.Instance.Status.Charm}";
        }
        // (다른 스탯 UI 업데이트 로직도 여기에 추가)
    }
}