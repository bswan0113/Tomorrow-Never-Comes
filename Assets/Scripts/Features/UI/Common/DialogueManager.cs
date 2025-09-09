// C:\Workspace\Tomorrow Never Comes\Assets\Scripts\Features\UI\Common\DialogueManager.cs

using System;
using System.Collections;
using System.Collections.Generic;
using Manager;
using UnityEngine;
using System.Linq;

/// <summary>
/// 게임의 전체 대화 흐름을 관리하는 싱글톤 매니저입니다.
/// 데이터(SO)와 UI(Handler) 사이의 다리 역할을 합니다.
/// </summary>
public class DialogueManager : MonoBehaviour
{
    public static DialogueManager Instance { get; private set; }

    private DialogueUIHandler m_RegisteredUI;
    private Queue<DialogueLine> dialogueQueue;
    private DialogueData currentDialogueData;

    private bool isDialogueActive = false;
    private bool isDisplayingChoices = false;
    private bool canProcessInput = true; // 입력 제어를 위한 플래그
    private const string noneRegisteredIdentifier = "0";
    public static event Action OnDialogueEnded;

    // 게임 상태 변경을 위한 이벤트
    public static event Action<bool> OnDialogueStateChanged;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        dialogueQueue = new Queue<DialogueLine>();
    }

    void Update()
    {
        if (isDialogueActive && !isDisplayingChoices && canProcessInput && Input.anyKeyDown)
        {
            canProcessInput = false; // 중복 입력 방지

            if (m_RegisteredUI.IsTyping)
            {
                m_RegisteredUI.SkipTypingEffect();
                canProcessInput = true; // 스킵 후에는 즉시 다음 입력 가능
            }
            else
            {
                DisplayNextLine();
            }
        }
    }

    public void RegisterDialogueUI(DialogueUIHandler uiHandler)
    {
        m_RegisteredUI = uiHandler;
    }

    /// <summary>
    /// 지정된 ID의 대화를 시작합니다.
    /// </summary>
    public void StartDialogue(string dialogueID)
    {
        if (string.IsNullOrEmpty(dialogueID) || dialogueID == noneRegisteredIdentifier)
        {
            return;
        }
        DialogueData data = GameResourceManager.Instance.GetDataByID<DialogueData>(dialogueID);
        if (data != null)
        {
            StartDialogue(data);
        }
        else
        {
            Debug.LogError($"Dialogue ID '{dialogueID}'를 찾을 수 없습니다!");
        }
    }

    /// <summary>
    /// DialogueData 에셋을 직접 받아 대화를 시작합니다. (내부 또는 분기용)
    /// </summary>
    public void StartDialogue(DialogueData data)
    {
        if (m_RegisteredUI == null) {
            Debug.LogError("DialogueUI가 등록되지 않아 대화를 시작할 수 없습니다!");
            return;
        }

        OnDialogueStateChanged?.Invoke(true); // "대화 시작!" 방송

        currentDialogueData = data;
        isDialogueActive = true;
        isDisplayingChoices = false;
        canProcessInput = true; // 대화 시작 시 항상 입력 가능 상태로 초기화

        m_RegisteredUI.Show();

        dialogueQueue.Clear();
        foreach (var line in data.dialogueLines)
        {
            dialogueQueue.Enqueue(line);
        }

        DisplayNextLine();
    }

    private void DisplayNextLine()
    {
        if (dialogueQueue.Count > 0)
        {
            DialogueLine currentLine = dialogueQueue.Dequeue();
            string speakerName = GetSpeakerName(currentLine.speakerID);
            m_RegisteredUI.ShowLine(speakerName, currentLine.dialogueText);

            // 새 대사 출력 후, 짧은 지연 뒤에 다시 입력 받도록 함
            StartCoroutine(EnableInputAfterDelay(0.2f));
        }
        else
        {
            if (currentDialogueData != null && currentDialogueData.choices != null && currentDialogueData.choices.Count > 0)
            {
                isDisplayingChoices = true;
                m_RegisteredUI.ShowChoices(currentDialogueData.choices);
            }
            else
            {
                EndDialogue();
            }
        }
    }

    private string GetSpeakerName(string speakerID)
    {
        if (string.IsNullOrEmpty(speakerID) || speakerID == noneRegisteredIdentifier) return "";

        CharacterData speakerData = GameResourceManager.Instance.GetDataByID<CharacterData>(speakerID);
        if (speakerData != null)
        {
            return speakerData.characterName;
        }

        Debug.LogError($"Character ID '{speakerID}'를 찾을 수 없습니다!");
        return $"[ID:{speakerID} 없음]";
    }

    // DialogueManager.cs 파일의 ProcessChoice 함수

    public void ProcessChoice(Choice choice)
    {
        // choice.nextDialogueID가 "0"이나 다른 종료 식별자인지 확인 (기존 로직과 동일하게)
        bool isEffectivelyNoNextDialogue = string.IsNullOrEmpty(choice.nextDialogueID) || choice.nextDialogueID == "0"; // "0"을 종료 식별자로 가정

        // 이제 actionLogic에 직접 접근할 필요 없이, 리스트의 아이템이 AdvanceDayAction 타입인지 바로 확인합니다.
        bool containsAdvanceToNextDay = choice.actions != null &&
                                        choice.actions.Any(action => action is AdvanceDayAction);

        // ExecuteChoiceActions를 먼저 호출하여 선택의 결과를 즉시 반영합니다.
        ExecuteChoiceActions(choice.actions);

        // 대화 종료 조건을 확인합니다.
        if (isEffectivelyNoNextDialogue || containsAdvanceToNextDay)
        {
            EndDialogue();
        }
        else // 종료 조건이 아닐 때만 다음 대화를 시작합니다.
        {
            StartDialogue(choice.nextDialogueID);
        }

        // 모든 로직이 끝난 후 입력 잠금을 해제하는 것이 더 안전할 수 있습니다.
        canProcessInput = true;
    }

    // 메서드의 파라미터 타입도 List<BaseChoiceAction>으로 변경합니다.
    private void ExecuteChoiceActions(List<BaseAction> actions)
    {
        if (actions == null) return;

        foreach (var action in actions)
        {
            // action이 null이 아닌지만 확인하면 됩니다. action 자체가 실행 로직을 가진 SO입니다.
            if (action != null)
            {
                action.Execute();
            }
        }
    }

    private void EndDialogue()
    {
        OnDialogueStateChanged?.Invoke(false); // "대화 끝!" 방송

        isDialogueActive = false;
        isDisplayingChoices = false;
        currentDialogueData = null;
        OnDialogueEnded?.Invoke();
        m_RegisteredUI.Hide();
    }

    /// <summary>
    /// 현재 대화가 활성화 상태인지 여부를 반환합니다.
    /// </summary>
    public bool IsDialogueActive()
    {
        return isDialogueActive;
    }

    /// <summary>
    /// 지정된 시간 후에 canProcessInput 플래그를 true로 바꾸는 코루틴
    /// </summary>
    private IEnumerator EnableInputAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        canProcessInput = true;
    }
}