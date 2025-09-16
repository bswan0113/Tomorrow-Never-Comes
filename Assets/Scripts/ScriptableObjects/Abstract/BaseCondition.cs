// 경로: Assets/Scripts/ScriptableObjects/Conditions/BaseCondition.cs



// ▼▼▼ 상속 클래스 변경 ▼▼▼
namespace ScriptableObjects.Abstract
{
    public abstract class BaseCondition : GameData
    {
        /// <summary>
        /// 이 조건이 충족되었는지 여부를 반환합니다.
        /// </summary>
        /// <returns>조건 충족 시 true, 아닐 시 false</returns>
        public abstract bool IsMet();
    }
}