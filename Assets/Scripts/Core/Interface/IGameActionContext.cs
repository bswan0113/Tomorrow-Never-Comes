// C:\Workspace\Tomorrow Never Comes\Assets\Scripts\Core\Interface\IGameActionContext.cs

using System;
using System.Threading;
using UnityEngine;

namespace Core.Interface
{
    /// <summary>
    /// 게임 액션 실행에 필요한 서비스와 유틸리티에 대한 접근을 제공하는 컨텍스트 인터페이스입니다.
    /// </summary>
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
        /// 플레이어 서비스에 접근하기 위한 인터페이스입니다.
        /// </summary>
        IPlayerService playerService { get; } // IPlayerService 속성 추가

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