// 경로: Assets/Scripts/ScriptableObjects/Conditions/StatCheckCondition.cs
using UnityEngine;
using System;
using Core.Interface;
using Core.Logging;
using Features.Player;
using UnityEngine.Playables;

[CreateAssetMenu(fileName = "StatCheckCondition", menuName = "Game Data/Conditions/Stat Check")]
public class StatCheckCondition : BaseCondition
{
    // 기존 ConditionData에 있던 필드들을 그대로 가져옵니다.
    public enum Operator { GreaterThan, LessThan, EqualTo, GreaterThanOrEqualTo, LessThanOrEqualTo }

    [Tooltip("PlayerStatus 클래스에 있는 프로퍼티(변수)의 이름과 정확히 일치해야 합니다. (예: Intellect, Charm)")]
    public string targetStatName;
    public Operator comparisonOperator;
    public long value;

    [SerializeField] IPlayerService playerService;

    public override bool IsMet()
    {
        if (playerService == null || playerService.GetCurrentPlayerStats() == null)
        {
            CoreLogger.LogError("[StatCheckCondition] PlayerDataManager 또는 PlayerStatus가 초기화되지 않았습니다.");
            return false;
        }

        var playerStatus = playerService.GetCurrentPlayerStats();
        var propertyInfo = typeof(PlayerStatsData).GetProperty(targetStatName);

        if (propertyInfo == null)
        {
            CoreLogger.LogError($"[StatCheckCondition] PlayerStatus에 '{targetStatName}'이라는 스탯이 없습니다.");
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