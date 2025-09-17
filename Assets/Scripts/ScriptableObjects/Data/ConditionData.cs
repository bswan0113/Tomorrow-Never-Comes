// 경로: C:\Workspace\Tomorrow Never Comes\Assets\Scripts\ScriptableObjects\Data\ConditionData.cs

using Core.Interface; // IPlayerService를 사용하기 위해 추가
using Core.Logging;
using ScriptableObjects.Abstract;
using UnityEngine;

namespace ScriptableObjects.Data
{
    [CreateAssetMenu(fileName = "New Condition", menuName = "Game Data/Rules/Condition Container")]
    public class ConditionData : GameData
    {
        [Tooltip("이 조건에 대한 설명 (기획자용)")]
        public string description;

        [Tooltip("실제 조건 판별 로직을 담고 있는 SO를 여기에 연결하세요.")]
        public BaseCondition conditionLogic;

        /// <summary>
        /// 연결된 조건 로직을 평가하여 결과를 반환합니다.
        /// </summary>
        /// <param name="playerService">플레이어 스탯 및 상태 정보를 제공하는 서비스.</param>
        /// <returns>조건 충족 시 true, 아닐 시 false</returns>
        public bool Evaluate(IPlayerService playerService) // IPlayerService 인자 추가
        {
            if (conditionLogic == null)
            {
                CoreLogger.LogWarning($"[ConditionData:{name}] conditionLogic이 할당되지 않았습니다. 항상 false를 반환합니다.", this);
                return false;
            }
            if (playerService == null)
            {
                CoreLogger.LogError($"[ConditionData:{name}] Evaluate 호출 시 playerService가 null입니다. 조건 평가를 할 수 없습니다.", this);
                return false;
            }
            // conditionLogic.IsMet() 메서드에 playerService 인자를 전달합니다.
            return conditionLogic.IsMet(playerService);
        }
    }
}