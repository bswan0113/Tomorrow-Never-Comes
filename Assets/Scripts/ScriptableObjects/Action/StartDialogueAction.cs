// 파일 경로: Assets/Scripts/ScriptableObjects/Actions/StartDialogueAction.cs

using System.Collections;
using Core.Interface;
using Core.Logging;
using UnityEngine;

[CreateAssetMenu(fileName = "New Dialogue Action", menuName = "Game Actions/Start Dialogue Action")]
public class StartDialogueAction : BaseAction
{
    public DialogueData dialogueData;
    public override bool IsValid(IGameActionContext context, out string reason)
    {
        if (context.gameService == null) // 예시: gameService가 필수인 경우
        {
            reason = "GameService가 컨텍스트에 없습니다.";
            return false;
        }

        reason = string.Empty;
        return true;
    }
    // 이 액션은 대화가 끝날 때까지 시퀀서를 '기다리게' 만들어야 합니다.
    public override IEnumerator Execute(IGameActionContext context)
    {

        if (context == null || context.dialogueService == null)
        {
            CoreLogger.LogError("AdvanceDayAction: IGameActionContext 또는 IGameService가 유효하지 않습니다!", this);
            yield break;
        }

        context.dialogueService.StartDialogue(dialogueData);
        yield return new WaitUntil(() => !context.dialogueService.IsDialogueActive());
    }
}