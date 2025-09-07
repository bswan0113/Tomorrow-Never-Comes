// C:\Workspace\Tomorrow Never Comes\Assets\Scripts\Features\World\InteractionObject.cs

using UnityEngine;
using UnityEngine.Events; // ▼▼▼ 다시 사용합니다 ▼▼▼

// ActionSequencer가 필수는 아니게 되므로 RequireComponent 제거
public class InteractionObject : MonoBehaviour, IInteractable
{
    [Header("행동력 비용")]
    [Tooltip("이 상호작용에 필요한 행동력. 0이면 비용 없음.")]
    public int actionPointCost = 0;

    [Header("이벤트 연결")]
    [Tooltip("행동력이 충분할 때 실행될 이벤트")]
    public UnityEvent onInteractionSuccess;

    [Tooltip("행동력이 부족할 때 실행될 이벤트")]
    public UnityEvent onInteractionFailure;


    /// <summary>
    /// IInteractable 인터페이스의 구현부입니다.
    /// 행동력 조건을 먼저 검사하고, 결과에 따라 다른 이벤트를 호출합니다.
    /// </summary>
    public void Interact()
    {
        Debug.Log($"{gameObject.name}(와)과 상호작용 시도... 필요한 행동력: {actionPointCost}");

        // GameManager가 없으면 상호작용 자체를 막는 것이 안전
        if (GameManager.Instance == null)
        {
            Debug.LogError("GameManager를 찾을 수 없어 행동력 검사를 할 수 없습니다!");
            return;
        }

        // 1. 행동력 조건 검사
        if (GameManager.Instance.CurrentActionPoint >= actionPointCost)
        {
            // 2. 성공: onInteractionSuccess 이벤트를 실행
            Debug.Log("행동력 충분. 상호작용을 실행합니다.");
            onInteractionSuccess?.Invoke();
        }
        else
        {
            // 3. 실패: onInteractionFailure 이벤트를 실행
            Debug.LogWarning("행동력 부족! 상호작용을 거부합니다.");
            onInteractionFailure?.Invoke();
        }
    }
}