// C:\Workspace\Tomorrow Never Comes\Assets\Scripts\Features\UI\Common\DialogueManager.cs

using System;
using System.Collections;
using System.Collections.Generic;
// using Manager; // GameResourceManager는 이제 인터페이스를 통해 접근하므로 필요 없음.
using UnityEngine;
using System.Linq;
using Core.Interface;
using Core.Interface.Core.Interface; // 우리가 정의한 인터페이스들을 사용하기 위해 필요


/// <summary>
/// 게임의 전체 대화 흐름을 관리하는 매니저입니다.
/// 데이터(SO)와 UI(Handler) 사이의 다리 역할을 합니다.
/// </summary>
public class DialogueManager : MonoBehaviour, IDialogueService, IGameActionContext  // 인터페이스 구현 추가
{
    // public static DialogueManager Instance { get; private set; } // <- 제거

    // 의존성 주입을 위한 필드
    private IGameResourceService _gameResourceService;
    private IDialogueUIHandler _uiHandler; // DialogueUIHandler 대신 인터페이스 사용
    private IGameService _gameService;
    public IGameService gameService => _gameService; // 주입받은 _gameService를 반환
    public IDialogueService dialogueService => this; // DialogueManager 자신이 IDialogueService이므로 자신을 반환
    public MonoBehaviour coroutineRunner => this;

    private Queue<DialogueLine> dialogueQueue;
    private DialogueData currentDialogueData;

    private bool isDialogueActive = false;
    private bool isDisplayingChoices = false;
    private bool canProcessInput = true;
    private const string noneRegisteredIdentifier = "0";

    // static 이벤트 대신 인스턴스 이벤트로 변경
    public event Action OnDialogueEnded; // IDialogueService에 추가
    public event Action<bool> OnDialogueStateChanged; // IDialogueService에 추가

    public void Initialize(IGameResourceService gameResourceService,  IGameService gameService)
    {
        _gameResourceService = gameResourceService ?? throw new ArgumentNullException(nameof(gameResourceService));
        _gameService = gameService ?? throw new ArgumentNullException(nameof(gameService)); // IGameService 주입

        Debug.Log("[DialogueManager] 초기화 완료.");
    }

    void Awake()
    {

        dialogueQueue = new Queue<DialogueLine>();
        Debug.Log("[DialogueManager] Awake - Singleton 로직 제거됨.");
    }

    // 컴포지션 루트에서 호출될 초기화 메서드
    public void Initialize(IGameResourceService gameResourceService)
    {
        _gameResourceService = gameResourceService ?? throw new ArgumentNullException(nameof(gameResourceService));

        Debug.Log("[DialogueManager] 초기화 완료.");
    }

    void Update()
    {
        if (isDialogueActive && !isDisplayingChoices && canProcessInput && Input.anyKeyDown)
        {
            canProcessInput = false; // 중복 입력 방지

            if (_uiHandler.IsTyping) // 주입받은 _uiHandler 사용
            {
                _uiHandler.SkipTypingEffect(); // 주입받은 _uiHandler 사용
                canProcessInput = true;
            }
            else
            {
                DisplayNextLine();
            }
        }
    }

    // IDialogueService 인터페이스 메서드 구현
    public void RegisterDialogueUI(IDialogueUIHandler uiHandler)
    {
        // Initialize에서 이미 주입받았으므로, 이 메서드는 필요 없을 수 있습니다.
        // 아니면 UI 교체가 필요할 경우를 대비하여 유지할 수 있습니다.
        // 여기서는 Initialize에서 주입받는 것을 주력으로 하고, 이 메서드는 선택적으로 둡니다.
        // 만약 DialogueUIHandler가 동적으로 바뀔 수 있다면 유지. 아니면 Initialize에서만 주입받도록 합니다.
        // 현재 코드에서는 m_RegisteredUI = uiHandler; 이 부분만 필요합니다.
        _uiHandler = uiHandler; // 주입받는 필드에 직접 할당
    }

    // IDialogueService 인터페이스 메서드 구현
    public void StartDialogue(string dialogueID)
    {
        if (_gameResourceService == null) { Debug.LogError("DialogueManager: IGameResourceService가 초기화되지 않았습니다."); return; }

        if (string.IsNullOrEmpty(dialogueID) || dialogueID == noneRegisteredIdentifier)
        {
            return;
        }
        // 주입받은 _gameResourceService 사용
        DialogueData data = _gameResourceService.GetDataByID<DialogueData>(dialogueID);
        if (data != null)
        {
            StartDialogue(data);
        }
        else
        {
            Debug.LogError($"Dialogue ID '{dialogueID}'를 찾을 수 없습니다!");
        }
    }

    // IDialogueService 인터페이스 메서드 구현
    public void StartDialogue(DialogueData data)
    {
        if (_uiHandler == null) {
            Debug.LogError("DialogueUI가 등록되지 않아 대화를 시작할 수 없습니다! Initialize()를 통해 주입되었는지 확인해주세요.");
            return;
        }

        OnDialogueStateChanged?.Invoke(true); // 인스턴스 이벤트 호출

        currentDialogueData = data;
        isDialogueActive = true;
        isDisplayingChoices = false;
        canProcessInput = true;

        _uiHandler.Show(); // 주입받은 _uiHandler 사용

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
            _uiHandler.ShowLine(speakerName, currentLine.dialogueText); // 주입받은 _uiHandler 사용

            StartCoroutine(EnableInputAfterDelay(0.2f));
        }
        else
        {
            if (currentDialogueData != null && currentDialogueData.choices != null && currentDialogueData.choices.Count > 0)
            {
                isDisplayingChoices = true;
                _uiHandler.ShowChoices(currentDialogueData.choices); // 주입받은 _uiHandler 사용
            }
            else
            {
                EndDialogue();
            }
        }
    }

    private string GetSpeakerName(string speakerID)
    {
        if (_gameResourceService == null) { Debug.LogError("DialogueManager: IGameResourceService가 초기화되지 않았습니다."); return "[ERR]"; }

        if (string.IsNullOrEmpty(speakerID) || speakerID == noneRegisteredIdentifier) return "";

        // 주입받은 _gameResourceService 사용
        CharacterData speakerData = _gameResourceService.GetDataByID<CharacterData>(speakerID);
        if (speakerData != null)
        {
            return speakerData.characterName;
        }

        Debug.LogError($"Character ID '{speakerID}'를 찾을 수 없습니다!");
        return $"[ID:{speakerID} 없음]";
    }

    // IDialogueService 인터페이스 메서드 구현
    public void ProcessChoice(ChoiceData choice)
    {
        bool isEffectivelyNoNextDialogue = string.IsNullOrEmpty(choice.nextDialogueID) || choice.nextDialogueID == noneRegisteredIdentifier;
        // AdvanceDayAction이 어떤 네임스페이스에 있는지 확인 필요
        // using GameAction; 과 같이 네임스페이스가 필요할 수 있습니다.
        bool containsAdvanceToNextDay = choice.actions != null &&
                                        choice.actions.Any(action => action is AdvanceDayAction);

        StartCoroutine(ExecuteChoiceActionsCoroutine(choice.actions, () =>
        {
            if (isEffectivelyNoNextDialogue || containsAdvanceToNextDay)
            {
                EndDialogue();
            }
            else
            {
                StartDialogue(choice.nextDialogueID);
            }
            canProcessInput = true;
        }));
    }

    private IEnumerator ExecuteChoiceActionsCoroutine(List<BaseAction> actions, Action onCompleted)
    {
        if (actions != null)
        {
            // DialogueManager 자신이 IGameActionContext이므로, 자신을 Context로 전달합니다.
            IGameActionContext context = this;

            foreach (var action in actions)
            {
                if (action != null)
                {
                    // BaseAction.Execute(this) 대신 BaseAction.Execute(context) 호출
                    yield return StartCoroutine(action.Execute(context));
                }
            }
        }
        onCompleted?.Invoke();
    }

    private void EndDialogue()
    {
        OnDialogueStateChanged?.Invoke(false); // 인스턴스 이벤트 호출

        isDialogueActive = false;
        isDisplayingChoices = false;
        currentDialogueData = null;
        OnDialogueEnded?.Invoke(); // 인스턴스 이벤트 호출
        _uiHandler.Hide(); // 주입받은 _uiHandler 사용
    }

    // IDialogueService 인터페이스 메서드 구현
    public bool IsDialogueActive()
    {
        return isDialogueActive;
    }

    private IEnumerator EnableInputAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        canProcessInput = true;
    }
}