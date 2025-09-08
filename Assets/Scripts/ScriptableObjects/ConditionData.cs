// C:\Workspace\Tomorrow Never Comes\Assets\Scripts\Data\Rules\ConditionSO.cs

using UnityEngine;

// 이 enum들은 나중에 확장될 수 있습니다. (예: 아이템 보유 여부, 이벤트 플래그 체크)
public enum ConditionType { StatCheck }
public enum Operator { GreaterThan, LessThan, EqualTo, GreaterThanOrEqualTo, LessThanOrEqualTo }

[CreateAssetMenu(fileName = "New Condition", menuName = "Game Data/Rules/Condition")]
public class ConditionData : GameData
{
    [Tooltip("이 조건에 대한 설명 (기획자용)")]
    public string description;
    
    public ConditionType type;
    
    [Tooltip("PlayerStatus 클래스에 있는 프로퍼티(변수)의 이름과 정확히 일치해야 합니다. (예: Intellect, Charm, Endurance, Money)")]
    public string targetStatName;
    
    public Operator comparisonOperator;
    public long value; // 돈(long) 같은 큰 숫자도 비교할 수 있도록 long 타입 사용
}