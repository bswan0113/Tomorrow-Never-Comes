// C:\Workspace\Tomorrow Never Comes\Assets\Scripts\Features\World\InteractionObject.cs

using System.Collections.Generic;
using Core.Interface;
using Core.Logging;
using UnityEngine;
using VContainer;

namespace Features.World
{
    [System.Serializable]
    public class ConditionalEvent
    {
        public string description;
        [Tooltip("여기에 있는 모든 조건을 만족해야 이벤트가 실행됩니다.")]
        public List<ConditionData> conditions;
        [Tooltip("조건이 충족되었을 때 실행될 ActionSequencer입니다.")]
        public ActionSequencer onConditionsMet;
    }

    public class InteractionObject : MonoBehaviour, IInteractable
    {
        [Header("행동력 비용")]
        [Tooltip("이 상호작용에 필요한 행동력. 0이면 비용 없음.")]
        public int actionPointCost = 0;

        [Header("이벤트 연결")]
        [Tooltip("위에서부터 순서대로 조건을 검사하여, 가장 먼저 모든 조건을 만족하는 이벤트 하나만 실행됩니다.")]
        public List<ConditionalEvent> conditionalEvents;

        [Tooltip("만족하는 조건이 하나도 없을 경우 실행될 기본 시퀀서입니다.")]
        public ActionSequencer defaultEvent;

        [Tooltip("행동력이 부족할 때 실행될 시퀀서입니다.")]
        public ActionSequencer onInteractionFailure;

        private IGameService _gameService;

        [Inject]
        public void Construct(IGameService gameService)
        {
            _gameService = gameService ?? throw new System.ArgumentNullException(nameof(gameService));
            CoreLogger.Log($"{gameObject.name}: 게임 서비스 주입 완료 (Construct 호출됨). GameService is null: {_gameService == null}", CoreLogger.LogLevel.Info,this);
        }

        public void Interact()
        {
            if (_gameService == null)
            {
                CoreLogger.LogWarning("GameService가 아직 주입되지 않았습니다. 상호작용을 처리할 수 없습니다.", this);
                return;
            }

            // 1. 행동력 조건 검사
            if (_gameService.CurrentActionPoint < actionPointCost)
            {
                CoreLogger.LogWarning("행동력 부족! 상호작용을 거부합니다.", this);
                if (onInteractionFailure != null)
                {
                    onInteractionFailure.ExecuteSequence();
                }
                else
                {
                    CoreLogger.LogWarning("행동력 부족 시 실행할 'onInteractionFailure' ActionSequencer가 설정되지 않았습니다.", this);
                }
                return;
            }

            // 2. 조건부 이벤트 목록 순차 검사
            foreach (var conditionalEvent in conditionalEvents)
            {
                if (conditionalEvent == null)
                {
                    CoreLogger.LogWarning("InteractionObject의 conditionalEvents 목록에 null 항목이 있습니다. 건너뜜.", this);
                    continue;
                }

                // 조건이 설정되지 않은 conditionalEvent는 이 단계에서 실행되지 않도록 처리 (defaultEvent로 넘어가도록)
                if (conditionalEvent.conditions == null || conditionalEvent.conditions.Count == 0)
                {
                    CoreLogger.Log($"조건부 이벤트 '{conditionalEvent.description}'에 조건이 없습니다. 다음 이벤트를 검사합니다.",  CoreLogger.LogLevel.Info,this);
                    continue;
                }

                bool allConditionsMet = true;
                foreach (var condition in conditionalEvent.conditions)
                {
                    if (condition == null)
                    {
                        CoreLogger.LogWarning($"조건부 이벤트 '{conditionalEvent.description}'에 null 조건이 포함되어 있습니다. 이를 만족하지 못하는 것으로 간주합니다.", this);
                        allConditionsMet = false;
                        break;
                    }
                    if (!_gameService.EvaluateCondition(condition))
                    {
                        allConditionsMet = false;
                        break;
                    }
                }

                if (allConditionsMet)
                {
                    CoreLogger.Log($"조건 '{conditionalEvent.description}' 충족. 해당 이벤트를 실행합니다.",  CoreLogger.LogLevel.Info,this);

                    // 행동력 소모
                    _gameService.UseActionPoint(actionPointCost);

                    if (conditionalEvent.onConditionsMet != null)
                    {
                        conditionalEvent.onConditionsMet.ExecuteSequence();
                    }
                    else
                    {
                        CoreLogger.LogWarning($"조건부 이벤트 '{conditionalEvent.description}'의 'onConditionsMet' ActionSequencer가 설정되지 않았습니다. 아무 일도 일어나지 않습니다.", this);
                    }
                    return; // 첫 번째 만족하는 이벤트 실행 후 종료
                }
            }

            // 3. 만족하는 조건부 이벤트가 하나도 없었을 경우
            CoreLogger.Log("만족하는 특별 조건이 없어 기본 이벤트를 실행합니다.",  CoreLogger.LogLevel.Info,this);

            // 행동력 소모
            _gameService.UseActionPoint(actionPointCost);

            if (defaultEvent != null)
            {
                    defaultEvent.ExecuteSequence();
            }
            else
            {
                CoreLogger.LogWarning("만족하는 조건부 이벤트가 없었으나, 'defaultEvent' ActionSequencer도 설정되지 않았습니다. 아무 일도 일어나지 않습니다.", this);
            }
        }
    }
}