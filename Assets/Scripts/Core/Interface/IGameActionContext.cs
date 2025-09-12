// C:\Workspace\Tomorrow Never Comes\Assets\Scripts\Core\Interface\IGameActionContext.cs

using System; // Action 델리게이트를 사용하기 위해 추가
using System.Threading;
using Core.Interface.Core.Interface; // CancellationToken을 사용하기 위해 추가
using UnityEngine;

namespace Core.Interface
{
    public interface IGameActionContext
    {
        /// <summary>
        /// 게임 서비스에 접근하기 위한 인터페이스입니다.
        /// </summary>
        IGameService gameService { get; }

        /// <summary>
        /// 대화 서비스에 접근하기 위한 인터페이스입니다.
        /// </summary>
        IDialogueService dialogueService { get; }

        /// <summary>
        /// 코루틴을 실행할 수 있는 MonoBehaviour 인스턴스입니다.
        /// </summary>
        MonoBehaviour coroutineRunner { get; }

        /// <summary>
        /// 현재 액션 시퀀스의 취소 토큰입니다.
        /// 액션은 이 토큰을 통해 취소 요청을 받을 수 있습니다.
        /// </summary>
        CancellationToken CancellationToken { get; }

        /// <summary>
        /// 액션 실행 중 발생한 오류를 보고하기 위한 콜백입니다.
        /// </summary>
        /// <param name="ex">발생한 예외 객체입니다.</param>
        void ReportError(Exception ex);
    }
}