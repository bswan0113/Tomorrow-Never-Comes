using System.Collections;
using System.Collections.Generic;
using Core;
using Core.Interface;
using UnityEngine;
using VContainer;

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
        Debug.Log($"{gameObject.name}: 게임 서비스 주입 완료 (Construct 호출됨). GameService is null: {_gameService == null}");
    }

    public void Interact()
    {
        if (_gameService == null)
        {
            Debug.LogWarning("GameService가 아직 주입되지 않았습니다. 잠시 후 다시 시도합니다.");
            return;
        }

        // 1. 행동력 조건 검사
        if (_gameService.CurrentActionPoint < actionPointCost)
        {
            Debug.LogWarning("행동력 부족! 상호작용을 거부합니다.");
            if (onInteractionFailure != null)
            {
                onInteractionFailure.ExecuteSequence();
            }
            return;
        }

        // 2. 조건부 이벤트 목록 순차 검사
        foreach (var conditionalEvent in conditionalEvents)
        {
            if (conditionalEvent.conditions == null) continue;

            bool allConditionsMet = true;
            foreach (var condition in conditionalEvent.conditions)
            {
                if (condition == null || !_gameService.EvaluateCondition(condition))
                {
                    allConditionsMet = false;
                    break;
                }
            }

            if (allConditionsMet)
            {
                Debug.Log($"조건 '{conditionalEvent.description}' 충족. 해당 이벤트를 실행합니다.");

                // 행동력 소모
                _gameService.UseActionPoint(actionPointCost);

                if (conditionalEvent.onConditionsMet != null)
                {
                    conditionalEvent.onConditionsMet.ExecuteSequence();
                }
                return;
            }
        }

        // 3. 만족하는 조건부 이벤트가 하나도 없었을 경우
        Debug.Log("만족하는 특별 조건이 없어 기본 이벤트를 실행합니다.");

        // 행동력 소모
        _gameService.UseActionPoint(actionPointCost);

        if (defaultEvent != null)
        {
            defaultEvent.ExecuteSequence();
        }
    }
}