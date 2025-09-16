// 파일 경로: Assets/Scripts/ScriptableObjects/Actions/InvokeUnityEventAction.cs (새 폴더 추천)

using System.Collections;
using Core.Interface;
using ScriptableObjects.Abstract;
using UnityEngine;
using UnityEngine.Events;

namespace ScriptableObjects.Action
{
    [CreateAssetMenu(fileName = "New UnityEvent Action", menuName = "Game Actions/UnityEvent Action")]
    public class InvokeUnityEventAction : BaseAction
    {
        public UnityEvent unityEvent;
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
        // 이 액션은 즉시 실행되고 끝나므로, 코루틴은 바로 종료됩니다.
        public override IEnumerator Execute(IGameActionContext context)
        {
            unityEvent?.Invoke();
            yield break; // 즉시 코루틴 종료
        }


    }
}