// 파일 경로: Assets/Scripts/ScriptableObjects/Action/AdvanceDayAction.cs

using System.Collections;
using UnityEngine;

[CreateAssetMenu(fileName = "AdvanceDayAction", menuName = "Game Actions/Advance To Next Day")]
public class AdvanceDayAction : BaseAction
{
    // [변경] 메서드 시그니처를 BaseAction에 맞게 수정합니다.
    public override IEnumerator Execute(MonoBehaviour executor)
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.AdvanceToNextDay();
        }
        else
        {
            Debug.LogError("GameManager.Instance가 씬에 없습니다!", this);
        }

        // [추가] 로직 실행 후 'yield break'를 호출하여 즉시 코루틴을 종료합니다.
        yield break;
    }
}