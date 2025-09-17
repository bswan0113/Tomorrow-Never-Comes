// 파일 경로: Assets/Scripts/ScriptableObjects/Action/AddStatAction.cs

using System;
using System.Collections;
using Core.Interface;
using Core.Logging;
using ScriptableObjects.Abstract;
using UnityEngine;

namespace ScriptableObjects.Action
{
    [CreateAssetMenu(fileName = "AddStatAction", menuName = "Game Actions/Add Stat or Money")]
    public class AddStatAction : BaseAction
    {
        [Tooltip("변경할 스탯의 이름 (PlayerStatus의 프로퍼티 이름과 일치. 예: Intellect, Charm, Money)")]
        public string targetStatName;

        [Tooltip("더하거나 뺄 값 (음수 가능)")]
        public int amount;

        // [SerializeField] private IPlayerService playerService; // 불필요한 필드 제거

        public override bool IsValid(IGameActionContext context, out string reason)
        {
            if (string.IsNullOrEmpty(targetStatName))
            {
                reason = "targetStatName이 설정되지 않았습니다.";
                return false;
            }
            // context.playerService의 유효성 검사를 IsValid에서 미리 하는 것이 좋습니다.
            if (context.playerService == null)
            {
                reason = "PlayerService가 컨텍스트에 없습니다.";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        public override IEnumerator Execute(IGameActionContext context)
        {
            // 이제 IGameActionContext를 통해 playerService를 가져옵니다.
            IPlayerService playerService = context.playerService;

            if (playerService == null)
            {
                // IsValid에서 이미 체크했겠지만, 런타임에 null이 될 경우를 대비하여 방어 코드 유지
                CoreLogger.LogError("[AddStatAction] PlayerService가 IGameActionContext에서 제공되지 않았습니다.");
                context.ReportError(new InvalidOperationException("PlayerService is not available in the GameActionContext."));
                yield break;
            }

            try
            {
                // 취소 요청 확인
                ThrowIfCancellationRequested(context.CancellationToken);

                switch (targetStatName)
                {
                    case "Intellect":
                        playerService.AddIntellect(amount);
                        CoreLogger.Log($"[AddStatAction] Intellect changed by {amount}. New value: {playerService.GetCurrentPlayerStats().Intellect}");
                        break;
                    case "Charm":
                        playerService.AddCharm(amount);
                        CoreLogger.Log($"[AddStatAction] Charm changed by {amount}. New value: {playerService.GetCurrentPlayerStats().Charm}");
                        break;
                    case "Money": // Money 스탯 변경 로직 추가 (PlayerDataManager에 AddMoney 함수가 있다면)
                        playerService.AddMoney(amount);
                        CoreLogger.Log($"[AddStatAction] Money changed by {amount}. New value: {playerService.GetCurrentPlayerStats().Money}");
                        break;
                    // TODO: PlayerDataManager에 AddEndurance 함수가 있다면 여기에 추가
                    // case "Endurance":
                    //     playerService.AddEndurance(amount);
                    //     break;
                    default:
                        CoreLogger.LogWarning($"[AddStatAction] '{targetStatName}'에 해당하는 스탯 변경 로직이 없습니다. 아무 작업도 수행하지 않습니다.");
                        break;
                }
                // 취소 요청 확인 (장기 실행 로직 중간에도 확인할 수 있음)
                ThrowIfCancellationRequested(context.CancellationToken);
            }
            catch (OperationCanceledException)
            {
                HandleActionExecutionError(context, new OperationCanceledException($"Action '{this.name}' was cancelled."));
            }
            catch (Exception ex)
            {
                // 다른 모든 예외는 여기서 처리하고 ReportError를 통해 알림
                HandleActionExecutionError(context, ex);
            }
            yield return null; // 모든 Action 코루틴은 최소한 한 번은 yield해야 합니다.
        }
    }
}