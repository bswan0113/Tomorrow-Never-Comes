// 경로: Assets/Scripts/ScriptableObjects/ChoiceActions/AddStatAction.cs
using UnityEngine;

[CreateAssetMenu(fileName = "AddStatAction", menuName = "Game Data/Choice Actions/Add Stat or Money")]
public class AddStatAction : BaseAction
{
    [Tooltip("변경할 스탯의 이름 (PlayerStatus의 프로퍼티 이름과 일치. 예: Intellect, Charm, Money)")]
    public string targetStatName;

    [Tooltip("더하거나 뺄 값 (음수 가능)")]
    public int amount;

    public override void Execute()
    {
        if (PlayerDataManager.Instance == null) return;

        // 문자열 기반으로 해당 함수를 동적으로 호출하는 대신,
        // 명시적으로 분기하여 안정성을 높입니다.
        switch (targetStatName)
        {
            case "Intellect":
                PlayerDataManager.Instance.AddIntellect(amount);
                break;
            case "Charm":
                PlayerDataManager.Instance.AddCharm(amount);
                break;
            // TODO: PlayerDataManager에 AddEndurance, AddMoney 함수가 있다면 여기에 추가
            // case "Endurance":
            //     PlayerDataManager.Instance.AddEndurance(amount);
            //     break;
            // case "Money":
            //     PlayerDataManager.Instance.AddMoney(amount);
            //     break;
            default:
                Debug.LogWarning($"[AddStatAction] '{targetStatName}'에 해당하는 스탯 변경 로직이 없습니다.", this);
                break;
        }
    }
}