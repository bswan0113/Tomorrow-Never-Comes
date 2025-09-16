// C:\Workspace\Tomorrow Never Comes\Assets\Scripts\Features\World\GameActionHandler.cs
// (파일 이름과 클래스 이름을 GameActionHandler로 변경)

using UnityEngine;

namespace Deprecated
{
    /// <summary>
    /// 씬 내에서 발생하는 다양한 행동(Action) 요청을 받아,
    /// 해당하는 싱글톤 매니저에게 전달하는 중계소 역할을 합니다.
    /// UnityEvent에서 싱글톤의 함수를 직접 호출할 수 없는 문제를 해결합니다.
    /// </summary>
    public class GameActionHandler : MonoBehaviour
    {
        // --- GameManager 관련 행동 ---

        // public void UseActionPoint(int amount)
        // {
        //     if (GameManager.Instance != null)
        //     {
        //         GameManager.Instance.UseActionPoint(amount);
        //     }
        // }
        //
        // public void AdvanceToNextDay()
        // {
        //     // 날을 마치기 전에는 반드시 저장을 먼저 호출
        //     if (PlayerDataManager.Instance != null)
        //     {
        //         PlayerDataManager.Instance.SavePlayerData();
        //     }
        //
        //     if (GameManager.Instance != null)
        //     {
        //         GameManager.Instance.AdvanceToNextDay();
        //     }
        // }
        //
        // // --- PlayerDataManager 관련 행동 ---
        //
        // public void AddIntellect(int amount)
        // {
        //     if (PlayerDataManager.Instance != null)
        //     {
        //         PlayerDataManager.Instance.AddIntellect(amount);
        //     }
        // }
        //
        // public void AddCharm(int amount)
        // {
        //     if (PlayerDataManager.Instance != null)
        //     {
        //         PlayerDataManager.Instance.AddCharm(amount);
        //     }
        // }
        //
        // // --- DialogueManager 관련 행동 ---
        //
        // public void StartDialogue(string dialogueID)
        // {
        //     if (DialogueManager.Instance != null)
        //     {
        //         DialogueManager.Instance.StartDialogue(dialogueID);
        //     }
        // }
        //
        // public void StartDialogueFromSO(DialogueData data)
        // {
        //     if (DialogueManager.Instance != null && data != null)
        //     {
        //         DialogueManager.Instance.StartDialogue(data);
        //     }
        // }
    }
}