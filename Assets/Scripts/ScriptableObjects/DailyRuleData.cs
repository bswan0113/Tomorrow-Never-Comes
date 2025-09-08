// C:\Workspace\Tomorrow Never Comes\Assets\Scripts\Data\Rules\DailyRuleSO.cs

using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Day X Rule", menuName = "Game Data/Rules/Daily Rule")]
public class DailyRuleData : GameData // GameResourceManager가 관리하도록 GameDataSO 상속
{
    [Tooltip("이 규칙이 적용될 날짜 (예: 1일차 종료 시 체크면 1)")]
    public int targetDay;

    [Header("생존 조건 (모두 만족해야 함 - AND)")]
    public List<ConditionData> survivalConditions;

    // TODO: 나중에 배드엔딩 분기를 위해 확장
    // [Header("배드엔딩 조건 (하나라도 만족하면 즉시 발동 - OR)")]
    // public List<ConditionSO> badEndingConditions;
    // public int badEndingDialogueID;
}