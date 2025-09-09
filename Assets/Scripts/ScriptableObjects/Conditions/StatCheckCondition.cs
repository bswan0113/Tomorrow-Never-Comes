// 경로: Assets/Scripts/ScriptableObjects/Conditions/StatCheckCondition.cs
using UnityEngine;
using System;

[CreateAssetMenu(fileName = "StatCheckCondition", menuName = "Game Data/Conditions/Stat Check")]
public class StatCheckCondition : BaseCondition
{
    // 기존 ConditionData에 있던 필드들을 그대로 가져옵니다.
    public enum Operator { GreaterThan, LessThan, EqualTo, GreaterThanOrEqualTo, LessThanOrEqualTo }

    [Tooltip("PlayerStatus 클래스에 있는 프로퍼티(변수)의 이름과 정확히 일치해야 합니다. (예: Intellect, Charm)")]
    public string targetStatName;
    public Operator comparisonOperator;
    public long value;

    public override bool IsMet()
    {
        if (PlayerDataManager.Instance == null || PlayerDataManager.Instance.Status == null)
        {
            Debug.LogError("[StatCheckCondition] PlayerDataManager 또는 PlayerStatus가 초기화되지 않았습니다.");
            return false;
        }

        var playerStatus = PlayerDataManager.Instance.Status;
        var propertyInfo = typeof(PlayerStatus).GetProperty(targetStatName);

        if (propertyInfo == null)
        {
            Debug.LogError($"[StatCheckCondition] PlayerStatus에 '{targetStatName}'이라는 스탯이 없습니다.");
            return false;
        }

        long currentStatValue = Convert.ToInt64(propertyInfo.GetValue(playerStatus));

        switch (comparisonOperator)
        {
            case Operator.GreaterThan: return currentStatValue > value;
            case Operator.LessThan: return currentStatValue < value;
            case Operator.EqualTo: return currentStatValue == value;
            case Operator.GreaterThanOrEqualTo: return currentStatValue >= value;
            case Operator.LessThanOrEqualTo: return currentStatValue <= value;
            default: return false;
        }
    }
}