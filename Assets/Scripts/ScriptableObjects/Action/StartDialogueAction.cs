// 파일 경로: Assets/Scripts/ScriptableObjects/Actions/StartDialogueAction.cs

using System.Collections;
using UnityEngine;

[CreateAssetMenu(fileName = "New Dialogue Action", menuName = "Game Actions/Start Dialogue Action")]
public class StartDialogueAction : BaseAction
{
    public DialogueData dialogueData;

    // 이 액션은 대화가 끝날 때까지 시퀀서를 '기다리게' 만들어야 합니다.
    public override IEnumerator Execute(MonoBehaviour executor)
    {
        if (DialogueManager.Instance == null || dialogueData == null)
        {
            Debug.LogWarning("DialogueManager 또는 DialogueData가 없어 대화를 시작할 수 없습니다.");
            yield break;
        }

        // 대화를 시작합니다.
        DialogueManager.Instance.StartDialogue(dialogueData);

        // DialogueManager의 IsDialogueActive() 상태를 확인하여 대화가 끝날 때까지 기다립니다.
        yield return new WaitUntil(() => !DialogueManager.Instance.IsDialogueActive());
    }
}