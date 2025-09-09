// C:\Workspace\Tomorrow Never Comes\Assets\Scripts\Core\GameManager.cs

using UnityEngine;
using UnityEngine.SceneManagement;
using System;
using System.Linq;         // ▼▼▼ 추가 ▼▼▼: FirstOrDefault 등 사용
using Manager;            // ▼▼▼ 추가 ▼▼▼: GameResourceManager 사용
using System.Reflection;// Action 이벤트를 사용하기 위함

public class GameManager : MonoBehaviour
{
    // ... (기존 변수들은 그대로) ...
    public static GameManager Instance { get; private set; }
    [Header("게임 상태")]
    [SerializeField] private int dayCount = 1;
    [SerializeField] private int maxActionPoint = 10;
    private int currentActionPoint;
    public static event Action OnDayStart;
    public static event Action OnActionPointChanged;


    void Awake()
    {
        // ... (Awake 함수는 그대로) ...
        if (Instance != null && Instance != this)
        {
            Destroy(this.gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(this.gameObject);
        currentActionPoint = maxActionPoint;
    }

    void Start()
    {
        LoadGameProgress();
        if (PlayerDataManager.Instance != null)
        {
            PlayerDataManager.Instance.Initialize();
        }
    }

    public int DayCount => dayCount;
    public int CurrentActionPoint => currentActionPoint;

    public bool UseActionPoint(int amount)
    {
        // ... (UseActionPoint 함수는 그대로) ...
        if (currentActionPoint >= amount)
        {
            currentActionPoint -= amount;
            Debug.Log($"행동력 {amount} 소모. 남은 행동력: {currentActionPoint}");
            OnActionPointChanged?.Invoke();
            return true;
        }
        else
        {
            Debug.LogWarning("행동력이 부족합니다!");
            return false;
        }
    }

    /// <summary>
    /// 다음 날로 시간을 진행시키는 메서드. 생존 여부를 먼저 체크합니다.
    /// </summary>
    public void AdvanceToNextDay()
    {
        // ▼▼▼ 수정/추가 ▼▼▼: 생존 조건 체크 로직
        if (CheckSurvivalConditions())
        {
            SaveGameProgress();
            // 생존 성공!
            dayCount++;
            Debug.Log($"<color=yellow>========== {dayCount}일차 아침이 밝았습니다. ==========</color>");

            currentActionPoint = maxActionPoint;

            OnDayStart?.Invoke();
            OnActionPointChanged?.Invoke();

            SceneTransitionManager.Instance.FadeAndLoadScene("PlayerRoom");
        }
        else
        {
            // 생존 실패... 게임 오버
            SceneTransitionManager.Instance.FadeAndLoadScene("GameOverScene");
        }
    }

    /// <summary>
    /// 현재 날짜를 기준으로 생존 조건을 만족하는지 검사합니다.
    /// </summary>
    /// <returns>생존했으면 true, 실패했으면 false</returns>
    private bool CheckSurvivalConditions()
    {
        // 1. 오늘 날짜에 맞는 규칙 SO를 GameResourceManager에서 찾는다.
        var allRules = GameResourceManager.Instance.GetAllDataOfType<DailyRuleData>();
        DailyRuleData currentDayRule = allRules.FirstOrDefault(rule => rule.targetDay == dayCount);

        // 2. 오늘 적용할 특별 규칙이 없다면? 기본 생존으로 간주.
        if (currentDayRule == null)
        {
            Debug.Log($"[{dayCount}일차] 특별 생존 규칙 없음. 통과.");
            return true;
        }

        // 3. 규칙이 있다면, 모든 생존 조건을 하나씩 검사한다.
        Debug.Log($"<color=orange>[{dayCount}일차] 생존 규칙 '{currentDayRule.name}' 검사를 시작합니다...</color>");
        foreach (var condition in currentDayRule.survivalConditions)
        {
            if (!EvaluateCondition(condition))
            {
                // 조건 중 하나라도 실패하면 즉시 생존 실패
                Debug.Log($"<color=red>생존 실패: 조건 '{condition.description}'을(를) 만족하지 못했습니다.</color>");
                return false;
            }
        }

        // 4. 모든 조건을 통과했으면 생존 성공
        Debug.Log($"<color=green>생존 성공: 모든 조건을 만족했습니다.</color>");
        return true;
    }

    /// <summary>
    /// 게임 오버를 처리하는 메서드입니다.
    /// </summary>
    private void HandleGameOver()
    {
        Debug.LogError("========= GAME OVER ==========");
    }

    /// <summary>
    /// ConditionSO 데이터를 해석하여 플레이어의 현재 상태와 비교하는 해석기입니다.
    /// </summary>
    public bool EvaluateCondition(ConditionData condition)
    {
        if (condition == null)
        {
            Debug.LogWarning("평가하려는 ConditionData가 null입니다.");
            return false;
        }
        return condition.Evaluate();
    }
    private void LoadGameProgress()
    {
        if (DataManager.Instance.HasSaveData)
        {
            var data = DataManager.Instance.LoadData("GameProgress", "SaveSlotID", 1);
            if (data != null && data.Count > 0)
            {
                // DB의 INTEGER는 long으로 오므로 Convert.ToInt32로 변환
                dayCount = Convert.ToInt32(data[0]["CurrentDay"]);
                Debug.Log($"<color=yellow>저장된 데이터 로드: {dayCount}일차에서 시작합니다.</color>");
            }
        }
        // 저장된 데이터가 없으면 기본값인 1일차로 시작
    }

    // ▼▼▼ 추가 ▼▼▼: 현재 날짜를 DB에 저장하는 함수
    private void SaveGameProgress()
    {
        // DataManager에 GameProgress 테이블이 없으면 생성, 있으면 업데이트하는 기능이 필요하지만,
        // 지금은 간단하게 UpdateData를 사용합니다. 새 게임 시엔 INSERT가 필요합니다.
        // DataManager.Instance.CreateNewGameData()에서 GameProgress 초기 데이터를 넣어주면 완벽해집니다.
        DataManager.Instance.UpdateData(
            "GameProgress",
            new string[] { "CurrentDay" },
            new object[] { dayCount },
            "SaveSlotID",
            1
        );

        // 플레이어 데이터도 여기서 함께 저장하도록 책임을 이전합니다.
        if (PlayerDataManager.Instance != null)
        {
            PlayerDataManager.Instance.SavePlayerData();
        }

        Debug.Log($"<color=orange>게임 진행 상황 저장 완료: {dayCount}일차</color>");
    }
}