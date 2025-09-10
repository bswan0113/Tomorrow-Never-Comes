// 파일 경로: Assets/Scripts/ScriptableObjects/Abstract/BaseAction.cs

using System.Collections;
using Core.Interface;
using UnityEngine;

/// <summary>
/// 게임에서 실행될 수 있는 모든 행동의 기반이 되는 추상 클래스입니다.
/// ScriptableObject로 만들어 데이터 에셋으로 관리합니다.
/// </summary>
public abstract class BaseAction : GameData
{
    /// <summary>
    /// 이 액션을 실행합니다.
    /// 액션이 끝날 때까지 시퀀서가 기다려야 한다면, 코루틴 내에서 yield return을 사용하세요.
    /// 즉시 완료되는 액션이라면, 로직 실행 후 'yield break;'를 호출하면 됩니다.
    /// </summary>
    /// <param name="executor">이 액션의 코루틴을 실행시켜주는 MonoBehaviour (예: ActionSequencer, DialogueManager)</param>
    public abstract IEnumerator Execute(IGameActionContext context);
}