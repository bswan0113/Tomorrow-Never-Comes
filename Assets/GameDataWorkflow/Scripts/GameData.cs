// Scripts/Database/Common/GameDataSO.cs
using UnityEngine;

/// <summary>
/// 게임 내 모든 ScriptableObject 데이터의 기반이 되는 추상 클래스입니다.
/// 모든 데이터는 고유한 정수 ID를 가집니다.
/// </summary>
public abstract class GameData : ScriptableObject
{
    [Tooltip("데이터를 구분하는 고유 ID")]
    public string id;
}