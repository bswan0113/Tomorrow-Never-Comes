// 경로: Assets/Scripts/ScriptableObjects/ChoiceActions/AdvanceDayAction.cs
using UnityEngine;

[CreateAssetMenu(fileName = "AdvanceDayAction", menuName = "Game Data/Choice Actions/Advance To Next Day")]
public class AdvanceDayAction : BaseAction
{
    public override void Execute()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.AdvanceToNextDay();
        }
    }
}