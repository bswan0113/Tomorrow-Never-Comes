// 경로: Assets/Scripts/ScriptableObjects/Conditions/StatCheckCondition.cs

using System;
using Core.Interface; // IPlayerService를 사용하기 위해 추가
using Core.Logging;
using Features.Player;
using ScriptableObjects.Abstract;
using UnityEngine;

namespace ScriptableObjects.Conditions
{
    [CreateAssetMenu(fileName = "StatCheckCondition", menuName = "Game Data/Conditions/Stat Check")]
    public class StatCheckCondition : BaseCondition
    {
        public enum Operator { GreaterThan, LessThan, EqualTo, GreaterThanOrEqualTo, LessThanOrEqualTo }

        [Tooltip("PlayerStatus 클래스에 있는 프로퍼티(변수)의 이름과 정확히 일치해야 합니다. (예: Intellect, Charm)")]
        public string targetStatName;
        public Operator comparisonOperator;
        public long value;

        // [SerializeField] IPlayerService playerService; // 불필요한 필드 제거

        /// <summary>
        /// 이 조건이 충족되었는지 여부를 반환합니다.
        /// </summary>
        /// <param name="playerService">플레이어 스탯 및 상태 정보를 제공하는 서비스.</param>
        /// <returns>조건 충족 시 true, 아닐 시 false</returns>
        public override bool IsMet(IPlayerService playerService) // IPlayerService 인자 받도록 시그니처 변경
        {
            if (playerService == null || playerService.GetCurrentPlayerStats() == null)
            {
                CoreLogger.LogError($"[StatCheckCondition:{this.name}] PlayerService 또는 PlayerStatsData가 초기화되지 않았습니다.");
                return false;
            }

            var playerStats = playerService.GetCurrentPlayerStats();
            var propertyInfo = typeof(PlayerStatsData).GetProperty(targetStatName);

            if (propertyInfo == null)
            {
                CoreLogger.LogError($"[StatCheckCondition:{this.name}] PlayerStatsData에 '{targetStatName}'이라는 스탯이 없습니다.");
                return false;
            }

            // GetValue는 object를 반환하므로, long 타입으로 안전하게 변환
            long currentStatValue = Convert.ToInt64(propertyInfo.GetValue(playerStats));
            CoreLogger.Log($"[StatCheckCondition:{this.name}] Checking stat '{targetStatName}' (current: {currentStatValue}) {comparisonOperator} {value}");

            switch (comparisonOperator)
            {
                case Operator.GreaterThan: return currentStatValue > value;
                case Operator.LessThan: return currentStatValue < value;
                case Operator.EqualTo: return currentStatValue == value;
                case Operator.GreaterThanOrEqualTo: return currentStatValue >= value;
                case Operator.LessThanOrEqualTo: return currentStatValue <= value;
                default:
                    CoreLogger.LogError($"[StatCheckCondition:{this.name}] 알 수 없는 비교 연산자: {comparisonOperator}");
                    return false;
            }
        }
    }
}