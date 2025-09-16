// 파일 경로: Assets/Scripts/ScriptableObjects/Action/AdvanceDayAction.cs

using System;
using System.Collections;
using Core.Interface;
using Core.Logging;
using ScriptableObjects.Abstract;
using UnityEngine;

namespace ScriptableObjects.Action
{
    [CreateAssetMenu(fileName = "AdvanceDayAction", menuName = "Game Actions/Advance To Next Day")]
    public class AdvanceDayAction : BaseAction
    {

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
        // [변경] 메서드 시그니처를 BaseAction에 맞게 수정합니다.
        public override IEnumerator Execute(IGameActionContext context)
        {
            if (context == null || context.gameService == null)
            {
                CoreLogger.LogError("AdvanceDayAction: IGameActionContext 또는 IGameService가 유효하지 않습니다!", this);
                yield break;
            }

            // Context를 통해 IGameService에 접근
            try
            {
                // 취소 요청 확인
                ThrowIfCancellationRequested(context.CancellationToken);

                context.gameService.AdvanceToNextDay();

                // 취소 요청 확인 (장기 실행 로직 중간에도 확인할 수 있음)
                ThrowIfCancellationRequested(context.CancellationToken);

                yield break;
            }
            catch (OperationCanceledException)
            {
                // 취소 요청은 여기서 처리하고, ReportError를 통해 알림
                HandleActionExecutionError(context, new OperationCanceledException($"Action '{this.name}' was cancelled."));
                yield break; // 코루틴 종료
            }
            catch (Exception ex)
            {
                // 다른 모든 예외는 여기서 처리하고 ReportError를 통해 알림
                HandleActionExecutionError(context, ex);
                yield break; // 코루틴 종료
            }
        }
    }
}