// C:\Workspace\Tomorrow Never Comes\Assets\Scripts\Features\World\InteractionObject.cs

using System.Collections.Generic; // List 사용을 위해 추가
using UnityEngine;
using UnityEngine.Events;

// ▼▼▼ [클래스 추가] 조건과 그에 따른 이벤트를 하나로 묶는 데이터 클래스 ▼▼▼
[System.Serializable]
public class ConditionalEvent
{
    public string description; // 인스펙터에서 알아보기 위한 설명
    [Tooltip("여기에 있는 모든 조건을 만족해야 이벤트가 실행됩니다.")]
    public List<ConditionData> conditions;
    public UnityEvent onConditionsMet;
}


public class InteractionObject : MonoBehaviour, IInteractable
{
    [Header("행동력 비용")]
    [Tooltip("이 상호작용에 필요한 행동력. 0이면 비용 없음.")]
    public int actionPointCost = 0;

    // ▼▼▼ [필드 변경] 기존 onInteractionSuccess/Failure 삭제 후 아래 내용으로 교체 ▼▼▼
    [Header("이벤트 연결")]
    [Tooltip("위에서부터 순서대로 조건을 검사하여, 가장 먼저 모든 조건을 만족하는 이벤트 하나만 실행됩니다.")]
    public List<ConditionalEvent> conditionalEvents;

    [Tooltip("만족하는 조건이 하나도 없을 경우 실행될 기본 이벤트입니다.")]
    public UnityEvent defaultEvent;

    [Tooltip("행동력이 부족할 때 실행될 이벤트입니다.")]
    public UnityEvent onInteractionFailure;
    // ▲▲▲ [필드 변경] 여기까지 ▲▲▲


    /// <summary>
    /// IInteractable 인터페이스의 구현부입니다.
    /// 행동력 체크 후, 조건 목록을 순차적으로 검사하여 해당하는 이벤트를 실행합니다.
    /// </summary>
    public void Interact()
    {
        if (GameManager.Instance == null)
        {
            Debug.LogError("GameManager를 찾을 수 없어 상호작용을 처리할 수 없습니다!");
            return;
        }

        // 1. 행동력 조건 검사
        if (GameManager.Instance.CurrentActionPoint < actionPointCost)
        {
            Debug.LogWarning("행동력 부족! 상호작용을 거부합니다.");
            onInteractionFailure?.Invoke();
            return; // 행동력이 없으면 여기서 즉시 종료
        }

        // 2. 조건부 이벤트 목록 순차 검사
        foreach (var conditionalEvent in conditionalEvents)
        {
            bool allConditionsMet = true;
            // 해당 이벤트에 연결된 모든 조건을 검사
            foreach (var condition in conditionalEvent.conditions)
            {
                // 조건 중 하나라도 만족하지 못하면, 이 이벤트는 실행 불가
                if (!GameManager.Instance.EvaluateCondition(condition))
                {
                    allConditionsMet = false;
                    break;
                }
            }

            // 모든 조건을 만족했다면?
            if (allConditionsMet)
            {
                Debug.Log($"조건 '{conditionalEvent.description}' 충족. 해당 이벤트를 실행합니다.");
                conditionalEvent.onConditionsMet?.Invoke();
                // ★★★★★ 가장 먼저 만족한 이벤트 하나만 실행하고 즉시 종료 ★★★★★
                return;
            }
        }

        // 3. 만족하는 조건부 이벤트가 하나도 없었을 경우
        Debug.Log("만족하는 특별 조건이 없어 기본 이벤트를 실행합니다.");
        defaultEvent?.Invoke();
    }
}