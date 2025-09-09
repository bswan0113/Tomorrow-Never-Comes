// 경로: C:\Workspace\Tomorrow Never Comes\Assets\Scripts\ScriptableObjects\ConditionData.cs
using UnityEngine;

// 기존에 있던 enum 선언은 이제 필요 없으므로 삭제합니다.
// public enum ConditionType { StatCheck }
// public enum Operator { ... }

[CreateAssetMenu(fileName = "New Condition", menuName = "Game Data/Rules/Condition Container")]
public class ConditionData : GameData
{
    [Tooltip("이 조건에 대한 설명 (기획자용)")]
    public string description;

    // ▼▼▼ 이 부분이 변경됩니다. ▼▼▼
    // 기존의 type, targetStatName, comparisonOperator, value 필드를 모두 삭제하고,
    // 아래의 BaseCondition 참조 필드 하나로 대체합니다.
    [Tooltip("실제 조건 판별 로직을 담고 있는 SO를 여기에 연결하세요.")]
    public BaseCondition conditionLogic;
    // ▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲

    /// <summary>
    /// 연결된 조건 로직을 평가하여 결과를 반환합니다.
    /// </summary>
    public bool Evaluate()
    {
        if (conditionLogic == null)
        {
            Debug.LogWarning($"ConditionData '{name}'에 conditionLogic이 할당되지 않았습니다.", this);
            return false;
        }
        return conditionLogic.IsMet();
    }
}