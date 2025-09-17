// 경로: Assets/Scripts/ScriptableObjects/Conditions/BaseCondition.cs

using Core.Interface; // IPlayerService를 사용하기 위해 추가

namespace ScriptableObjects.Abstract
{
    /// <summary>
    /// 게임 내 조건을 추상화하는 기본 클래스입니다.
    /// ScriptableObject로 관리될 수 있으며, 조건 충족 여부를 판단하는 로직을 포함합니다.
    /// </summary>
    public abstract class BaseCondition : GameData
    {
        /// <summary>
        /// 이 조건이 충족되었는지 여부를 반환합니다.
        /// </summary>
        /// <param name="playerService">플레이어 스탯 및 상태 정보를 제공하는 서비스.</param>
        /// <returns>조건 충족 시 true, 아닐 시 false</returns>
        public abstract bool IsMet(IPlayerService playerService); // IPlayerService 인자 추가
    }
}