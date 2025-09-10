// 파일 경로: Assets/Scripts/ScriptableObjects/Action/AddStatAction.cs

using System.Collections;
using Core.Interface;
using UnityEngine;

[CreateAssetMenu(fileName = "AddStatAction", menuName = "Game Actions/Add Stat or Money")]
public class AddStatAction : BaseAction
{
    [Tooltip("변경할 스탯의 이름 (PlayerStatus의 프로퍼티 이름과 일치. 예: Intellect, Charm, Money)")]
    public string targetStatName;

    [Tooltip("더하거나 뺄 값 (음수 가능)")]
    public int amount;

    [SerializeField] private IPlayerService playerService;

    // [변경] 메서드 시그니처를 BaseAction에 맞게 수정하고, 반환 타입을 IEnumerator로 변경합니다.
    // executor 파라미터는 이 액션에서 직접 사용하지 않지만, 인터페이스를 맞추기 위해 필요합니다.
    public override IEnumerator Execute(IGameActionContext context)
    {
        if (playerService == null)
        {
            Debug.LogError("playerService가 씬에 없습니다!", this);
            yield break; // PlayerDataManager가 없으면 아무것도 하지 않고 즉시 종료
        }

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
                Debug.LogWarning($"[AddStatAction] '{targetStatName}'에 해당하는 스탯 변경 로직이 없습니다.", this);
                break;
        }

        // [추가] 로직 실행 후 'yield break'를 호출하여 이 코루틴이 즉시 완료되었음을 알립니다.
        yield break;
    }
}