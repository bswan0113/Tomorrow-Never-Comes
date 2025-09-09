// 파일 경로: Assets/Scripts/ScriptableObjects/Actions/InvokeUnityEventAction.cs (새 폴더 추천)

using System.Collections;
using UnityEngine;
using UnityEngine.Events;

[CreateAssetMenu(fileName = "New UnityEvent Action", menuName = "Game Actions/UnityEvent Action")]
public class InvokeUnityEventAction : BaseAction
{
    public UnityEvent unityEvent;

    // 이 액션은 즉시 실행되고 끝나므로, 코루틴은 바로 종료됩니다.
    public override IEnumerator Execute(MonoBehaviour executor)
    {
        unityEvent?.Invoke();
        yield break; // 즉시 코루틴 종료
    }
}