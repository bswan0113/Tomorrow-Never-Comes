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

        [SerializeField] private IPlayerService playerService;

        public override bool IsValid(IGameActionContext context, out string reason)
        {
            if (string.IsNullOrEmpty(targetStatName))
            {
                reason = "targetStatName이 설정되지 않았습니다.";
                return false;
            }
            if (context.gameService == null) // 예시: gameService가 필수인 경우
            {
                reason = "GameService가 컨텍스트에 없습니다.";
                return false;
            }

            reason = string.Empty;
            return true;
        }
        // [변경] 메서드 시그니처를 BaseAction에 맞게 수정하고, 반환 타입을 IEnumerator로 변경합니다.
        // executor 파라미터는 이 액션에서 직접 사용하지 않지만, 인터페이스를 맞추기 위해 필요합니다.
        public override IEnumerator Execute(IGameActionContext context)
        {
            if (playerService == null)
            {
                CoreLogger.LogError("playerService가 씬에 없습니다!", this);
                yield break; // PlayerDataManager가 없으면 아무것도 하지 않고 즉시 종료
            }
            try
            {
                // 취소 요청 확인
                ThrowIfCancellationRequested(context.CancellationToken);

                // 기존 로직은 그대로 유지합니다.
                switch (targetStatName)
                {
                    case "Intellect":
                        playerService.AddIntellect(amount);
                        break;
                    case "Charm":
                        playerService.AddCharm(amount);
                        break;
                    // TODO: PlayerDataManager에 AddEndurance, AddMoney 함수가 있다면 여기에 추가
                    // case "Endurance":
                    //     playerService.AddEndurance(amount);
                    //     break;
                    // case "Money":
                    //     playerService.AddMoney(amount);
                    //     break;
                    default:
                        CoreLogger.LogWarning($"[AddStatAction] '{targetStatName}'에 해당하는 스탯 변경 로직이 없습니다.", this);
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
        }
    }
}