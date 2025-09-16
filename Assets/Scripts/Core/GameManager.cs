// C:\Workspace\Tomorrow Never Comes\Assets\Scripts\Core\GameManager.cs

using System;
using System.Linq;
using System.Threading.Tasks;
using Core.Data.Interface;
using Core.Interface;
using Core.Logging;
using Features.Data; // GameProgressData 사용을 위해 추가
using UnityEngine;
using VContainer;


// GameManager를 일반 C# 클래스로 변경
namespace Core
{
    public class GameManager : IGameService // : MonoBehaviour (<- 제거)
    {
        // public static GameManager Instance { get; private set; } // <- 제거
        // [Header("게임 상태")] // <- 일반 클래스에서는 [SerializeField]와 함께 작동하지 않습니다.
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
        private readonly IDataService _dataService; // P24: DataManager의 파사드 역할을 위해 IDataService 유지
        private readonly IGameProgressRepository _gameProgressRepository; // P24: GameProgressData 로드를 위해 추가

        // 생성자를 통해 의존성을 주입받도록 변경
        [Inject]
        public GameManager(
            IPlayerService playerService,
            ISceneTransitionService sceneTransitionService,
            IGameResourceService gameResourceService,
            IDataService dataService, // DataManager (IDataService)
            IGameProgressRepository gameProgressRepository) // GameProgressRepository
        {
            _playerService = playerService ?? throw new ArgumentNullException(nameof(playerService));
            _sceneTransitionService = sceneTransitionService ?? throw new ArgumentNullException(nameof(sceneTransitionService));
            _gameResourceService = gameResourceService ?? throw new ArgumentNullException(nameof(gameResourceService));
            _dataService = dataService ?? throw new ArgumentNullException(nameof(dataService));
            _gameProgressRepository = gameProgressRepository ?? throw new ArgumentNullException(nameof(gameProgressRepository));

            currentActionPoint = maxActionPoint; // 초기화 로직은 생성자에서 수행
            CoreLogger.Log("GameManager 초기화 완료 (DI 방식)");
        }

        public void StartGame()
        {
            LoadGameProgress();
        }

        public int DayCount => dayCount;
        public int CurrentActionPoint => currentActionPoint;

        public bool UseActionPoint(int amount)
        {
            if (currentActionPoint >= amount)
            {
                currentActionPoint -= amount;
                CoreLogger.Log($"행동력 {amount} 소모. 남은 행동력: {currentActionPoint}");
                OnActionPointChanged?.Invoke();
                return true;
            }
            else
            {
                CoreLogger.LogWarning("행동력이 부족합니다!");
                return false;
            }
        }

        public async void AdvanceToNextDay() // 비동기 저장을 위해 async로 변경
        {
            if (CheckSurvivalConditions())
            {
                await SaveGameProgress(); // 비동기 저장 대기
                dayCount++;
                CoreLogger.Log($"<color=yellow>========== {dayCount}일차 아침이 밝았습니다. ==========</color>");

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
                CoreLogger.Log($"[{dayCount}일차] 특별 생존 규칙 없음. 통과.");
                return true;
            }

            CoreLogger.Log($"<color=orange>[{dayCount}일차] 생존 규칙 '{currentDayRule.name}' 검사를 시작합니다...</color>");
            foreach (var condition in currentDayRule.survivalConditions)
            {
                if (!EvaluateCondition(condition))
                {
                    CoreLogger.Log($"<color=red>생존 실패: 조건 '{condition.description}'을(를) 만족하지 못했습니다.</color>");
                    return false;
                }
            }
            CoreLogger.Log($"<color=green>생존 성공: 모든 조건을 만족했습니다.</color>");
            return true;
        }

        private void HandleGameOver()
        {
            CoreLogger.LogError("========= GAME OVER ==========");
        }

        public bool EvaluateCondition(ConditionData condition)
        {
            if (condition == null)
            {
                CoreLogger.LogWarning("평가하려는 ConditionData가 null입니다.");
                return false;
            }
            return condition.Evaluate();
        }

        private async void LoadGameProgress() // 비동기 로드를 위해 async로 변경
        {
            // 주입받은 DataService를 통해 SaveData 유무 확인
            // HasSaveData는 DataManager의 HasSaveData를 사용합니다.
            // DataManager의 LoadAllGameData는 시작 시 HasSaveData를 업데이트하므로, 여기서는 그 값을 사용합니다.
            if (_dataService.HasSaveData)
            {
                // P24: GameProgressData 로드를 위해 IGameProgressRepository 사용
                GameProgressData progressData = await _gameProgressRepository.LoadGameProgressAsync(1); // 기본 세이브 슬롯 ID = 1
                if (progressData != null)
                {
                    dayCount = progressData.CurrentDay;
                    CoreLogger.Log($"<color=yellow>저장된 데이터 로드: {dayCount}일차에서 시작합니다. (마지막 씬: {progressData.LastSceneName})</color>");
                    _sceneTransitionService.FadeAndLoadScene(progressData.LastSceneName); // 로드된 씬으로 이동
                }
                else
                {
                    CoreLogger.Log($"<color=cyan>저장된 게임 진행 데이터가 없거나 로드에 실패했습니다. 새 게임으로 시작합니다 (1일차).</color>");
                    dayCount = 1;
                    _sceneTransitionService.FadeAndLoadScene("PlayerRoom"); // 기본 시작 씬
                }
            }
            else
            {
                CoreLogger.Log($"<color=cyan>저장된 게임 데이터가 없습니다. 새 게임으로 시작합니다 (1일차).</color>");
                dayCount = 1;
                _sceneTransitionService.FadeAndLoadScene("PlayerRoom"); // 기본 시작 씬
            }
        }

        private async Task SaveGameProgress() // 비동기 저장을 위해 async Task로 변경
        {
            // 현재 게임 상태를 GameProgressData 객체로 만듦
            var currentProgress = new GameProgressData
            {
                SaveSlotID = 1, // 기본 세이브 슬롯 ID
                CurrentDay = dayCount,
                LastSceneName = _sceneTransitionService.CurrentSceneName // 현재 씬 이름을 가져오는 인터페이스 필요 (가정)
                                                                       // SceneTransitionService에 CurrentSceneName 속성이 없으면 추가해야 함.
                                                                       // 임시로 하드코딩된 "PlayerRoom"을 사용하거나, 실제 씬 매니저에서 가져와야 함.
            };

            // P24: DataManager의 SaveAllGameData 파사드 메서드를 호출하여 전체 게임 데이터 저장
            await _dataService.SaveAllGameData(currentProgress.SaveSlotID);
            CoreLogger.Log($"<color=orange>게임 진행 상황 저장 완료 (DataManager 파사드): {dayCount}일차</color>");
        }
    }
}