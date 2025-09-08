// C:\Workspace\Tomorrow Never Comes\Assets\Scripts\Features\World\ActionSequencer.cs

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// UnityEvent들을 순차적으로, 대기 시간을 포함하여 실행할 수 있게 해주는 컴포넌트.
/// 대화가 끝날 때까지 기다리는 등의 고급 로직을 처리합니다.
/// </summary>
public class ActionSequencer : MonoBehaviour
{
    // 액션 목록. 인스펙터에서 설정합니다.
    public List<GameAction> actions;

    // 현재 실행 중인 시퀀스 코루틴
    private Coroutine sequenceCoroutine;
    private bool dialogueIsInProgress = false;

    /// <summary>
    /// 이 시퀀서에 등록된 모든 액션을 처음부터 순서대로 실행합니다.
    /// </summary>
    public void ExecuteSequence()
    {
        // 이미 실행 중인 시퀀스가 있다면 중복 실행을 막기 위해 중지
        if (sequenceCoroutine != null)
        {
            StopCoroutine(sequenceCoroutine);
        }
        sequenceCoroutine = StartCoroutine(SequenceCoroutine());
    }

    private IEnumerator SequenceCoroutine()
    {
        foreach (var action in actions)
        {
            // 1. 대기 시간이 있다면 먼저 기다립니다.
            if (action.delay > 0)
            {
                yield return new WaitForSeconds(action.delay);
            }

            // 2. 대화 액션이라면, 대화가 끝날 때까지 기다립니다.
            if (action.actionType == ActionType.StartDialogue && DialogueManager.Instance != null)
            {
                dialogueIsInProgress = true;
                DialogueManager.Instance.StartDialogue(action.dialogueData);
                // DialogueManager가 isDialogueActive를 false로 만들 때까지 대기
                yield return new WaitUntil(() => dialogueIsInProgress == false);
            }
            else // 3. 일반 UnityEvent 액션이라면 그냥 실행합니다.
            {
                action.unityEvent?.Invoke();
            }
        }
        sequenceCoroutine = null; // 모든 액션이 끝나면 코루틴 참조를 비웁니다.
    }

    private void OnEnable()
    {
        DialogueManager.OnDialogueEnded += HandleDialogueEnded;
        // 만약 OnDialogueStateChanged도 필요하다면 여기서 구독
        // DialogueManager.OnDialogueStateChanged += HandleDialogueStateChanged;
    }

    private void OnDisable()
    {
        DialogueManager.OnDialogueEnded -= HandleDialogueEnded;
        // DialogueManager.OnDialogueStateChanged -= HandleDialogueStateChanged;
    }

    // ▼▼▼ 이벤트 핸들러 함수 추가 ▼▼▼
    private void HandleDialogueEnded()
    {
        // DialogueManager에서 대화가 끝났음을 알리면, 플래그를 false로 변경합니다.
        // WaitUntil 조건이 충족되어 코루틴이 재개될 것입니다.
        dialogueIsInProgress = false;
        Debug.Log("[ActionSequencer] Received OnDialogueEnded event.");
    }
}

// 액션의 종류를 정의하는 enum
public enum ActionType
{
    UnityEvent,     // 일반 이벤트 실행
    StartDialogue   // 대화 시작 (그리고 끝날 때까지 대기)
}

// 하나의 액션을 정의하는 데이터 클래스 (MonoBehaviour가 아님)
[System.Serializable]
public class GameAction
{
    public string description; // 인스펙터에서 알아보기 위한 설명
    public ActionType actionType;
    
    [Tooltip("액션 실행 전 대기 시간 (초)")]
    public float delay;

    // 각 액션 타입에 필요한 데이터
    public UnityEvent unityEvent;
    public DialogueData dialogueData; // StartDialogue 타입일 때만 사용
}