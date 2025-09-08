using UnityEngine;

/// <summary>
/// 캐릭터의 고유 정보(이름, 초상화 등)를 담는 ScriptableObject.
/// GameDataSO를 상속받아 고유 ID를 가집니다.
/// </summary>
[CreateAssetMenu(fileName = "New Character", menuName = "Game Data/Character Data")]
public class CharacterData : GameData
{
    [Header("캐릭터 정보")]
    [Tooltip("게임 내에서 표시될 캐릭터의 이름입니다.")]
    public string characterName;

    // [미래 확장 영역]
    // public Sprite portrait;
    // public Color nameColor = Color.white;
}