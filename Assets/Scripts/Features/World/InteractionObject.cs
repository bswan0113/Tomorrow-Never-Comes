// C:\Workspace\Tomorrow Never Comes\Assets\Scripts\Features\World\InteractionObject.cs

using System.Collections.Generic;
using UnityEngine;
// using UnityEngine.Events; // UnityEvent를 더 이상 사용하지 않으므로 주석 처리하거나 삭제 가능

// [변경] ConditionalEvent 클래스 내부의 UnityEvent를 ActionSequencer로 교체합니다.
[System.Serializable]
public class ConditionalEvent
{
    public string description;
    [Tooltip("여기에 있는 모든 조건을 만족해야 이벤트가 실행됩니다.")]
    public List<ConditionData> conditions;
    // public UnityEvent onConditionsMet; // 기존
    [Tooltip("조건이 충족되었을 때 실행될 ActionSequencer입니다.")]
    public ActionSequencer onConditionsMet; // 변경
}


public class InteractionObject : MonoBehaviour, IInteractable
{
    [Header("행동력 비용")]
    [Tooltip("이 상호작용에 필요한 행동력. 0이면 비용 없음.")]
    public int actionPointCost = 0;

    [Header("이벤트 연결")]
    [Tooltip("위에서부터 순서대로 조건을 검사하여, 가장 먼저 모든 조건을 만족하는 이벤트 하나만 실행됩니다.")]
    public List<ConditionalEvent> conditionalEvents;

    // [변경] defaultEvent와 onInteractionFailure도 ActionSequencer로 교체합니다.
    [Tooltip("만족하는 조건이 하나도 없을 경우 실행될 기본 시퀀서입니다.")]
    public ActionSequencer defaultEvent;

    [Tooltip("행동력이 부족할 때 실행될 시퀀서입니다.")]
    public ActionSequencer onInteractionFailure;


    public void Interact()
    {
        // [수정] GameManager.Instance.UseActionPoint 호출을 추가합니다.
        // 행동력 사용은 상호작용이 "성공"했을 때 이루어져야 합니다.
        if (GameManager.Instance == null)
        {
            Debug.LogError("GameManager를 찾을 수 없어 상호작용을 처리할 수 없습니다!");
            return;
        }

        // 1. 행동력 조건 검사
        if (GameManager.Instance.CurrentActionPoint < actionPointCost)
        {
            Debug.LogWarning("행동력 부족! 상호작용을 거부합니다.");
            // [변경] Invoke() 대신 ExecuteSequence()를 호출합니다. null 체크도 함께 합니다.
            if (onInteractionFailure != null)
            {
                onInteractionFailure.ExecuteSequence();
            }
            return;
        }

        // 2. 조건부 이벤트 목록 순차 검사
        foreach (var conditionalEvent in conditionalEvents)
        {
            if (conditionalEvent.conditions == null) continue; // 조건 리스트가 null일 경우 건너뛰기

            bool allConditionsMet = true;
            foreach (var condition in conditionalEvent.conditions)
            {
                if (condition == null || !GameManager.Instance.EvaluateCondition(condition))
                {
                    allConditionsMet = false;
                    break;
                }
            }

            if (allConditionsMet)
            {
                Debug.Log($"조건 '{conditionalEvent.description}' 충족. 해당 이벤트를 실행합니다.");

                // [추가] 행동력을 실제로 소모하는 시점
                GameManager.Instance.UseActionPoint(actionPointCost);

                // [변경] Invoke() 대신 ExecuteSequence()를 호출합니다.
                if (conditionalEvent.onConditionsMet != null)
                {
                    conditionalEvent.onConditionsMet.ExecuteSequence();
                }
                return;
            }
        }

        // 3. 만족하는 조건부 이벤트가 하나도 없었을 경우
        Debug.Log("만족하는 특별 조건이 없어 기본 이벤트를 실행합니다.");

        // [추가] 행동력을 실제로 소모하는 시점
        GameManager.Instance.UseActionPoint(actionPointCost);

        // [변경] Invoke() 대신 ExecuteSequence()를 호출합니다.
        if (defaultEvent != null)
        {
            defaultEvent.ExecuteSequence();
        }
    }
}