// C:\Workspace\Tomorrow Never Comes\Assets\Scripts\Core\GameManager.cs

using System;
using System.Linq;
using Core.Data.Interface;
using Core.Interface;
using UnityEngine;
using VContainer;


// GameManager를 일반 C# 클래스로 변경
namespace Core
{
    public class GameManager : IGameService // : MonoBehaviour (<- 제거)
    {
        // public static GameManager Instance { get; private set; } // <- 제거
        [Header("게임 상태")] // <- 일반 클래스에서는 [SerializeField]와 함께 작동하지 않습니다.
        private int dayCount = 1;
        private int maxActionPoint = 10;
        private int currentActionPoint;

        // 이벤트는 그대로 유지 가능
        public event Action OnDayStart;
        public event Action OnActionPointChanged;

        // 의존성들을 저장할 private readonly 필드
        private readonly IPlayerService _playerService;
        private readonly ISceneTransitionService _sceneTransitionService;
        private readonly IGameResourceService _gameResourceService;
        private readonly IDataService _dataService;
        // 생성자를 통해 의존성을 주입받도록 변경
        [Inject]
        public GameManager(
            IPlayerService playerService,
            ISceneTransitionService sceneTransitionService,
            IGameResourceService gameResourceService,
            IDataService dataService)
        {
            _playerService = playerService ?? throw new ArgumentNullException(nameof(playerService));
            _sceneTransitionService = sceneTransitionService ?? throw new ArgumentNullException(nameof(sceneTransitionService));
            _gameResourceService = gameResourceService ?? throw new ArgumentNullException(nameof(gameResourceService));
            _dataService = dataService ?? throw new ArgumentNullException(nameof(dataService));

            currentActionPoint = maxActionPoint; // 초기화 로직은 생성자에서 수행
            Debug.Log("GameManager 초기화 완료 (DI 방식)");
        }

        public void StartGame()
        {
            LoadGameProgress();
            _playerService.Initialize(); // 주입받은 PlayerService 사용
        }

        public int DayCount => dayCount;
        public int CurrentActionPoint => currentActionPoint;

        public bool UseActionPoint(int amount)
        {
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

        public void AdvanceToNextDay()
        {
            if (CheckSurvivalConditions())
            {
                SaveGameProgress();
                dayCount++;
                Debug.Log($"<color=yellow>========== {dayCount}일차 아침이 밝았습니다. ==========</color>");

                currentActionPoint = maxActionPoint;

                OnDayStart?.Invoke();
                OnActionPointChanged?.Invoke();

                // 주입받은 SceneTransitionService 사용
                _sceneTransitionService.FadeAndLoadScene("PlayerRoom");
            }
            else
            {
                // 주입받은 SceneTransitionService 사용
                _sceneTransitionService.FadeAndLoadScene("GameOverScene");
            }
        }

        private bool CheckSurvivalConditions()
        {
            // 주입받은 GameResourceService 사용
            var allRules = _gameResourceService.GetAllDataOfType<DailyRuleData>();
            DailyRuleData currentDayRule = allRules.FirstOrDefault(rule => rule.targetDay == dayCount);

            if (currentDayRule == null)
            {
                Debug.Log($"[{dayCount}일차] 특별 생존 규칙 없음. 통과.");
                return true;
            }

            Debug.Log($"<color=orange>[{dayCount}일차] 생존 규칙 '{currentDayRule.name}' 검사를 시작합니다...</color>");
            foreach (var condition in currentDayRule.survivalConditions)
            {
                if (!EvaluateCondition(condition))
                {
                    Debug.Log($"<color=red>생존 실패: 조건 '{condition.description}'을(를) 만족하지 못했습니다.</color>");
                    return false;
                }
            }
            Debug.Log($"<color=green>생존 성공: 모든 조건을 만족했습니다.</color>");
            return true;
        }

        private void HandleGameOver()
        {
            Debug.LogError("========= GAME OVER ==========");
        }

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
            // 주입받은 DataService 사용
            if (_dataService.HasSaveData)
            {
                var data = _dataService.LoadData("GameProgress", "SaveSlotID", 1);
                if (data != null && data.Count > 0)
                {
                    dayCount = Convert.ToInt32((object)data[0]["CurrentDay"]);
                    Debug.Log($"<color=yellow>저장된 데이터 로드: {dayCount}일차에서 시작합니다.</color>");
                }
            }
        }

        private void SaveGameProgress()
        {
            // 주입받은 DataService 사용
            _dataService.UpdateData(
                "GameProgress",
                new string[] { "CurrentDay" },
                new object[] { dayCount },
                "SaveSlotID",
                1
            );

            // 주입받은 PlayerService 사용
            _playerService.SavePlayerData();

            Debug.Log($"<color=orange>게임 진행 상황 저장 완료: {dayCount}일차</color>");
        }


    }
}