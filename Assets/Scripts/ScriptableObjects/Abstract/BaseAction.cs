// 경로: Assets/Scripts/ScriptableObjects/ChoiceActions/BaseChoiceAction.cs (새 폴더 생성 추천)
using UnityEngine;

public abstract class BaseAction : GameData
{
    /// <summary>
    /// 이 액션을 실행합니다.
    /// </summary>
    public abstract void Execute();
}