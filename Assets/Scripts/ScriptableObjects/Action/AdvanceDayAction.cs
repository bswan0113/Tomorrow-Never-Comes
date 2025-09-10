// 파일 경로: Assets/Scripts/ScriptableObjects/Action/AdvanceDayAction.cs

using System.Collections;
using Core.Interface;
using UnityEngine;

[CreateAssetMenu(fileName = "AdvanceDayAction", menuName = "Game Actions/Advance To Next Day")]
public class AdvanceDayAction : BaseAction
{
    // [변경] 메서드 시그니처를 BaseAction에 맞게 수정합니다.
    public override IEnumerator Execute(IGameActionContext context)
    {
        if (context == null || context.gameService == null)
        {
            Debug.LogError("AdvanceDayAction: IGameActionContext 또는 IGameService가 유효하지 않습니다!", this);
            yield break;
        }

        // Context를 통해 IGameService에 접근
        context.gameService.AdvanceToNextDay();

        yield break;
    }
}