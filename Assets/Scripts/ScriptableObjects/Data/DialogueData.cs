using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 선택지 하나에 대한 정보를 담는 순수 데이터 클래스.
/// </summary>
[System.Serializable]
public class Choice
{
    [TextArea(2, 5)]
    public string choiceText;
    public string nextDialogueID;

    // ChoiceAction 래퍼를 없애고 BaseChoiceAction 리스트를 직접 사용합니다.
    public List<BaseAction> actions;
}
/// <summary>
/// 대사 한 줄에 대한 정보를 담는 순수 데이터 클래스.
/// </summary>
[System.Serializable]
public class DialogueLine
{
    [Tooltip("이 대사를 말하는 CharacterData의 ID. 0 이면 시스템 독백(나레이션)입니다.")]
    public string speakerID;

    [Tooltip("화면에 표시될 실제 대사 내용입니다.")]
    [TextArea(3, 10)]
    public string dialogueText;
}

/// <summary>
/// 하나의 대화 단위를 구성하는 ScriptableObject.
/// GameDataSO를 상속받아 고유 ID를 가집니다.
/// </summary>
[CreateAssetMenu(fileName = "New Dialogue", menuName = "Game Data/Dialogue Data")]
public class DialogueData : GameData
{
    [Header("대화 내용")]
    [Tooltip("이 대화에서 순차적으로 보여줄 대사들의 목록입니다.")]
    public List<DialogueLine> dialogueLines;

    [Header("선택지")]
    [Tooltip("모든 대사가 끝난 후 플레이어에게 제공될 선택지 목록입니다.")]
    public List<Choice> choices;

}

public enum ChoiceActionType
{
    None,
    // --- PlayerDataManager 관련 ---
    AddIntellect,
    AddCharm,
    AddEndurance,
    AddMoney,
    // --- GameManager 관련 ---
    AdvanceToNextDay
    // 여기에 필요한 모든 종류의 액션을 미리 정의해둡니다.
    // 예: UseActionPoint, GoToScene_PlayerRoom 등
}

