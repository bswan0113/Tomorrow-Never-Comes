// 파일 경로: Assets/Scripts/ScriptableObjects/Abstract/BaseAction.cs

using System.Collections;
using System.Threading;
using System;
using Core.Interface;
using UnityEngine;

/// <summary>
/// 게임에서 실행될 수 있는 모든 행동의 기반이 되는 추상 클래스입니다.
/// ScriptableObject로 만들어 데이터 에셋으로 관리합니다.
/// </summary>
public abstract class BaseAction : GameData
{
    /// <summary>
    /// (P2 확장 가드) 이 액션이 실행되기 위한 전제 조건이 유효한지 검사합니다.
    /// 예를 들어, 필요한 데이터가 할당되었는지, 컨텍스트가 필수 서비스를 제공하는지 등을 검사할 수 있습니다.
    /// </summary>
    /// <param name="context">실행될 컨텍스트.</param>
    /// <param name="reason">유효하지 않을 경우의 사유.</param>
    /// <returns>실행 가능하면 true, 아니면 false.</returns>
    public abstract bool IsValid(IGameActionContext context, out string reason);

    /// <summary>
    /// 이 액션을 실행합니다.
    /// 실행 전에 IsValid(context)가 true인지 확인해야 합니다.
    /// </summary>
    /// <param name="context">액션 실행에 필요한 서비스들과 코루틴 실행기, 취소 토큰 등을 포함하는 컨텍스트입니다.</param>
    public abstract IEnumerator Execute(IGameActionContext context);

    /// <summary>
    /// 코루틴 내부에서 취소 요청이 있었는지 주기적으로 확인하고,
    /// 요청이 있다면 CancellationException을 발생시킵니다.
    /// </summary>
    /// <param name="cancellationToken">현재 액션 시퀀스의 취소 토큰.</param>
    protected void ThrowIfCancellationRequested(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
    }

    /// <summary>
    /// 액션 코루틴 내부에서 발생한 예외를 컨텍스트에 보고합니다.
    /// (참고: 각 상속받는 액션의 Execute 내부에서 try-catch로 감싸고 이 메서드를 호출해야 함)
    /// </summary>
    /// <param name="context">IGameActionContext 인스턴스.</param>
    /// <param name="ex">발생한 예외.</param>
    protected void HandleActionExecutionError(IGameActionContext context, Exception ex)
    {
        context.ReportError(ex);
    }
}