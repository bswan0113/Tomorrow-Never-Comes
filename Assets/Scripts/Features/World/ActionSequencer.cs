// 파일 경로: C:\Workspace\Tomorrow Never Comes\Assets\Scripts\Features\World\ActionSequencer.cs

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// BaseAction 에셋들을 순차적으로 실행하는 범용 시퀀서입니다.
/// 각 Action이 비동기(코루틴) 실행을 지원하므로, 복잡한 연출을 만들 수 있습니다.
/// </summary>
public class ActionSequencer : MonoBehaviour
{
    // [변경] 기존 GameAction 클래스 대신 BaseAction SO 리스트를 직접 사용합니다.
    // 이제 인스펙터에서 UnityEvent를 설정하는 대신, 만들어 둔 Action SO 에셋들을 끌어다 놓으면 됩니다.
    public List<BaseAction> actions;

    // 현재 실행 중인 시퀀스 코루틴
    private Coroutine sequenceCoroutine;

    /// <summary>
    /// 이 시퀀서에 등록된 모든 액션을 처음부터 순서대로 실행합니다.
    /// </summary>
    public void ExecuteSequence()
    {
        if (sequenceCoroutine != null)
        {
            StopCoroutine(sequenceCoroutine);
        }
        sequenceCoroutine = StartCoroutine(SequenceCoroutine());
    }

    /// <summary>
    /// [변경] 시퀀스 실행 코루틴이 매우 단순해졌습니다.
    /// 이제 각 액션이 동기인지 비동기인지 신경 쓸 필요 없이, 그저 실행하고 끝날 때까지 기다려주기만 하면 됩니다.
    /// </summary>
    private IEnumerator SequenceCoroutine()
    {
        foreach (var action in actions)
        {
            if (action == null)
            {
                Debug.LogWarning("ActionSequencer에 비어있는(null) Action이 있습니다.", this);
                continue;
            }

            // 각 Action SO에 구현된 Execute 코루틴을 실행하고, 끝날 때까지 기다립니다.
            // - 만약 Action이 즉시 끝나는 동기 작업이라면(yield break), 바로 다음 액션으로 넘어갑니다.
            // - 만약 Action이 시간이 걸리는 비동기 작업이라면(yield return ...), 해당 작업이 끝날 때까지 여기서 대기합니다.
            yield return StartCoroutine(action.Execute(this));
        }

        sequenceCoroutine = null; // 모든 액션이 끝나면 코루틴 참조를 비웁니다.
    }

    // [삭제] 아래의 코드는 더 이상 ActionSequencer의 책임이 아닙니다.
    // 대화가 끝났는지 여부는 'StartDialogueAction' SO가 스스로 처리하게 됩니다.
    // 따라서 dialogueIsInProgress 플래그, 이벤트 핸들러(OnEnable, OnDisable, HandleDialogueEnded)가 모두 필요 없어집니다.
    /*
    private bool dialogueIsInProgress = false;
    private void OnEnable() { ... }
    private void OnDisable() { ... }
    private void HandleDialogueEnded() { ... }
    */
}


// [삭제] 아래의 enum과 클래스는 더 이상 사용되지 않으므로 파일에서 완전히 삭제합니다.
/*
public enum ActionType { ... }

[System.Serializable]
public class GameAction { ... }
*/