// C:\Workspace\Tomorrow Never Comes\Assets\Scripts\Core\GameManager.cs

using System;
using System.Linq;
using System.Threading.Tasks;
using Core.Data.Interface;
using Core.Interface;
using Core.Logging;
using Features.Data;
using Features.Player; // PlayerStatsData를 사용하기 위해 추가
using ScriptableObjects.Data;
using UnityEngine;
using VContainer;

namespace Core
{
    public class GameManager : IGameService
    {
        private int dayCount = 1;
        private int maxActionPoint = 10;
        private int currentActionPoint;

        public event Action OnDayStart;
        public event Action OnActionPointChanged;

        private readonly IPlayerService _playerService;
        private readonly ISceneTransitionService _sceneTransitionService;
        private readonly IGameResourceService _gameResourceService;
        private readonly IDataService _dataService;
        private readonly IGameProgressRepository _gameProgressRepository;

        [Inject]
        public GameManager(
            IPlayerService playerService,
            ISceneTransitionService sceneTransitionService,
            IGameResourceService gameResourceService,
            IDataService dataService,
            IGameProgressRepository gameProgressRepository)
        {
            _playerService = playerService ?? throw new ArgumentNullException(nameof(playerService));
            _sceneTransitionService = sceneTransitionService ?? throw new ArgumentNullException(nameof(sceneTransitionService));
            _gameResourceService = gameResourceService ?? throw new ArgumentNullException(nameof(gameResourceService));
            _dataService = dataService ?? throw new ArgumentNullException(nameof(dataService));
            _gameProgressRepository = gameProgressRepository ?? throw new ArgumentNullException(nameof(gameProgressRepository));

            currentActionPoint = maxActionPoint;
            CoreLogger.Log("GameManager 초기화 완료 (DI 방식)");
        }

        public async Task StartGame()
        {
            CoreLogger.Log("[GameManager] StartGame 호출됨.");
            await LoadGameProgress();
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

        public async Task AdvanceToNextDay()
        {
            CoreLogger.Log("[GameManager] 다음 날로 진행 요청.");
            if (CheckSurvivalConditions())
            {
                await SaveGameProgress();
                dayCount++;
                CoreLogger.Log($"<color=yellow>========== {dayCount}일차 아침이 밝았습니다. ==========</color>");

                currentActionPoint = maxActionPoint;

                OnDayStart?.Invoke();
                OnActionPointChanged?.Invoke();

                await _sceneTransitionService.FadeAndLoadScene("PlayerRoom");
            }
            else
            {
                HandleGameOver();
                await _sceneTransitionService.FadeAndLoadScene("GameOverScene");
            }
        }

        private bool CheckSurvivalConditions()
        {
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
            return condition.Evaluate(_playerService);
        }

        private async Task LoadGameProgress()
        {
            CoreLogger.Log("[GameManager] 게임 진행 상황 로드 시도...");
            if (_dataService.HasSaveData)
            {
                GameProgressData progressData = await _gameProgressRepository.LoadGameProgressAsync(1);
                if (progressData != null)
                {
                    dayCount = progressData.CurrentDay;
                    CoreLogger.Log($"<color=yellow>저장된 데이터 로드 성공: {dayCount}일차에서 시작합니다. (마지막 씬: {progressData.LastSceneName})</color>");
                    await _sceneTransitionService.FadeAndLoadScene(progressData.LastSceneName);
                }
                else
                {
                    CoreLogger.Log($"<color=cyan>저장된 게임 진행 데이터가 없거나 로드에 실패했습니다. 새 게임으로 시작합니다 (1일차).</color>");
                    dayCount = 1;
                    await _sceneTransitionService.FadeAndLoadScene("PlayerRoom");
                }
            }
            else
            {
                CoreLogger.Log($"<color=cyan>저장된 게임 데이터가 없습니다. 새 게임으로 시작합니다 (1일차).</color>");
                dayCount = 1;
                await _sceneTransitionService.FadeAndLoadScene("PlayerRoom");
            }
        }

        /// <summary>
        /// 현재 게임 상태(진행, 스탯)를 수집하여 DataManager에 저장을 요청합니다.
        /// </summary>
        private async Task SaveGameProgress()
        {
            CoreLogger.Log("[GameManager] 게임 데이터 저장을 준비합니다...");
            const int saveSlotId = 1;

            // 1. 저장할 GameProgressData를 생성합니다.
            var gameProgressToSave = new GameProgressData
            {
                SaveSlotID = saveSlotId,
                CurrentDay = this.dayCount,
                LastSceneName = _sceneTransitionService.CurrentSceneName,
                SaveDateTime = DateTime.UtcNow
            };

            // 2. IPlayerService를 통해 현재 PlayerStatsData를 가져옵니다.
            PlayerStatsData playerStatsToSave = _playerService.GetCurrentPlayerStats();

            // 데이터 일관성을 위해 SaveSlotID를 통일합니다.
            playerStatsToSave.SaveSlotID = saveSlotId;

            // 3. 수집한 두 데이터를 DataManager의 SaveAllGameData에 전달합니다.
            await _dataService.SaveAllGameData(gameProgressToSave, playerStatsToSave);

            CoreLogger.Log($"<color=orange>게임 데이터 저장 요청 완료: {dayCount}일차</color>");
        }
    }
}