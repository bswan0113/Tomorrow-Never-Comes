// C:\Workspace\Tomorrow Never Comes\Assets\Scripts\Features\UI\Common\DialogueManager.cs

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading; // CancellationTokenSource를 사용하기 위해 추가
using Core.Interface;
using Core.Interface.Core.Interface;
using Core.Logging;
using UnityEngine;

namespace Features.UI.Common
{
    /// <summary>
    /// 게임의 전체 대화 흐름을 관리하는 매니저입니다.
    /// 데이터(SO)와 UI(Handler) 사이의 다리 역할을 합니다.
    /// </summary>
    public class DialogueManager : MonoBehaviour, IDialogueService, IGameActionContext
    {
        // 의존성 주입을 위한 필드
        private IGameResourceService _gameResourceService;
        private IDialogueUIHandler _uiHandler;
        private IGameService _gameService;

        // IGameActionContext 인터페이스 구현
        public IGameService gameService => _gameService;
        public IDialogueService dialogueService => this;
        public MonoBehaviour coroutineRunner => this;

        // 선택지 액션 실행을 위한 CancellationTokenSource 및 플래그
        private CancellationTokenSource _choiceActionCts;
        private bool _isExecutingChoiceActions = false;

        // IGameActionContext 인터페이스 구현: 취소 토큰
        public CancellationToken CancellationToken => _choiceActionCts?.Token ?? CancellationToken.None;

        // IGameActionContext 인터페이스 구현: 오류 보고
        public void ReportError(Exception ex)
        {
            CoreLogger.LogError($"[DialogueManager] 선택지 액션 실행 중 오류 발생: {ex.Message}\n{ex.StackTrace}", this);
            // 대화 시스템의 오류는 치명적일 수 있으므로, 대화를 즉시 종료하고 복구 시도
            StopChoiceActions(true); // 오류 발생 시 액션 강제 중단
            EndDialogue(); // 오류 발생 시 대화 종료
        }

        private Queue<DialogueLine> dialogueQueue;
        private DialogueData currentDialogueData;

        private bool isDialogueActive = false;
        private bool isDisplayingChoices = false;
        private bool canProcessInput = true;
        private const string noneRegisteredIdentifier = "0";

        // 인스턴스 이벤트로 변경
        public event Action OnDialogueEnded;
        public event Action<bool> OnDialogueStateChanged;

        public void Initialize(IGameResourceService gameResourceService, IGameService gameService)
        {
            _gameResourceService = gameResourceService ?? throw new ArgumentNullException(nameof(gameResourceService));
            _gameService = gameService ?? throw new ArgumentNullException(nameof(gameService));

            CoreLogger.Log("[DialogueManager] 초기화 완료.");
        }

        void Awake()
        {
            dialogueQueue = new Queue<DialogueLine>();
            CoreLogger.Log("[DialogueManager] Awake - Singleton 로직 제거됨.");
        }

        // GameObject 파괴 시 리소스 클린업
        private void OnDestroy()
        {
            StopChoiceActions(); // 선택지 액션 실행 중이었다면 중단
        }

        void Update()
        {
            if (isDialogueActive && !isDisplayingChoices && canProcessInput && Input.anyKeyDown)
            {
                canProcessInput = false; // 중복 입력 방지

                if (_uiHandler != null && _uiHandler.IsTyping)
                {
                    _uiHandler.SkipTypingEffect();
                    canProcessInput = true;
                }
                else
                {
                    DisplayNextLine();
                }
            }
        }

        public void RegisterDialogueUI(IDialogueUIHandler uiHandler)
        {
            _uiHandler = uiHandler;
        }

        public void StartDialogue(string dialogueID)
        {
            if (_gameResourceService == null) { CoreLogger.LogError("DialogueManager: IGameResourceService가 초기화되지 않았습니다.", this); return; }

            if (string.IsNullOrEmpty(dialogueID) || dialogueID == noneRegisteredIdentifier)
            {
                CoreLogger.LogWarning($"DialogueManager: 유효하지 않은 Dialogue ID '{dialogueID}'로 대화 시작 요청.", this);
                return;
            }

            DialogueData data = _gameResourceService.GetDataByID<DialogueData>(dialogueID);
            if (data != null)
            {
                StartDialogue(data);
            }
            else
            {
                CoreLogger.LogError($"Dialogue ID '{dialogueID}'를 찾을 수 없습니다!", this);
            }
        }

        public void StartDialogue(DialogueData data)
        {
            if (_uiHandler == null)
            {
                CoreLogger.LogError("DialogueUI가 등록되지 않아 대화를 시작할 수 없습니다! Initialize()를 통해 주입되었는지 확인해주세요.", this);
                return;
            }
            if (isDialogueActive)
            {
                CoreLogger.LogWarning("DialogueManager: 이미 대화가 활성 상태입니다. 새로운 대화 요청을 무시합니다.", this);
                return;
            }

            OnDialogueStateChanged?.Invoke(true);

            currentDialogueData = data;
            isDialogueActive = true;
            isDisplayingChoices = false;
            canProcessInput = true;

            _uiHandler.Show();

            dialogueQueue.Clear();
            if (data.dialogueLines != null)
            {
                foreach (var line in data.dialogueLines)
                {
                    dialogueQueue.Enqueue(line);
                }
            }
            else
            {
                CoreLogger.LogWarning($"DialogueData '{data.id}'에 대화 라인이 없습니다. 선택지로 바로 넘어갑니다.", this);
            }

            DisplayNextLine();
        }

        private void DisplayNextLine()
        {
            if (dialogueQueue.Count > 0)
            {
                DialogueLine currentLine = dialogueQueue.Dequeue();
                string speakerName = GetSpeakerName(currentLine.speakerID);
                _uiHandler.ShowLine(speakerName, currentLine.dialogueText);

                StartCoroutine(EnableInputAfterDelay(0.2f));
            }
            else
            {
                if (currentDialogueData != null && currentDialogueData.choices != null && currentDialogueData.choices.Count > 0)
                {
                    isDisplayingChoices = true;
                    _uiHandler.ShowChoices(currentDialogueData.choices);
                }
                else
                {
                    EndDialogue();
                }
            }
        }

        private string GetSpeakerName(string speakerID)
        {
            if (_gameResourceService == null) { CoreLogger.LogError("DialogueManager: IGameResourceService가 초기화되지 않았습니다.", this); return "[ERR]"; }

            if (string.IsNullOrEmpty(speakerID) || speakerID == noneRegisteredIdentifier) return "";

            CharacterData speakerData = _gameResourceService.GetDataByID<CharacterData>(speakerID);
            if (speakerData != null)
            {
                return speakerData.characterName;
            }

            CoreLogger.LogError($"Character ID '{speakerID}'를 찾을 수 없습니다!", this);
            return $"[ID:{speakerID} 없음]";
        }

        public void ProcessChoice(ChoiceData choice)
        {
            if (_isExecutingChoiceActions)
            {
                CoreLogger.LogWarning("DialogueManager: 이미 선택지 액션이 실행 중입니다. 새로운 선택지 처리를 무시합니다.", this);
                return;
            }
            if (choice == null)
            {
                CoreLogger.LogError("DialogueManager: 처리할 ChoiceData가 null입니다.", this);
                EndDialogue();
                return;
            }

            bool isEffectivelyNoNextDialogue = string.IsNullOrEmpty(choice.nextDialogueID) || choice.nextDialogueID == noneRegisteredIdentifier;
            bool containsAdvanceToNextDay = choice.actions != null &&
                                            choice.actions.Any(action => action is AdvanceDayAction);

            StartCoroutine(ExecuteChoiceActionsCoroutine(choice.actions, () =>
            {
                canProcessInput = true; // 액션 실행 완료 후 입력 재활성화
                if (isEffectivelyNoNextDialogue || containsAdvanceToNextDay)
                {
                    EndDialogue();
                }
                else
                {
                    StartDialogue(choice.nextDialogueID);
                }
            }));
        }

        /// <summary>
        /// 현재 실행 중인 선택지 액션을 중단하고 관련 리소스를 해제합니다.
        /// </summary>
        /// <param name="reportCancellationError">취소 시 OperationCanceledException을 ReportError로 보고할지 여부.</param>
        private void StopChoiceActions(bool reportCancellationError = false)
        {
            if (!_isExecutingChoiceActions) return;

            CoreLogger.Log("[DialogueManager] 선택지 액션 실행 중단 요청.",CoreLogger.LogLevel.Info, this);

            _choiceActionCts?.Cancel(); // 모든 액션에 취소 요청

            if (reportCancellationError)
            {
                ReportError(new OperationCanceledException("Dialogue choice actions were explicitly stopped."));
            }

            _choiceActionCts?.Dispose();
            _choiceActionCts = null;
            _isExecutingChoiceActions = false;
            CoreLogger.Log("[DialogueManager] 선택지 액션 실행 중단 및 리소스 해제 완료.",CoreLogger.LogLevel.Info, this);
        }


        private IEnumerator ExecuteChoiceActionsCoroutine(List<BaseAction> actions, Action onCompleted)
        {
            _isExecutingChoiceActions = true;
            _choiceActionCts?.Dispose(); // 이전 CTS가 남아있을 경우 해제
            _choiceActionCts = new CancellationTokenSource();
            CancellationToken token = _choiceActionCts.Token;

            IGameActionContext context = this; // DialogueManager 자신이 Context 역할

            try
            {
                if (actions != null)
                {
                    foreach (var action in actions)
                    {
                        if (token.IsCancellationRequested)
                        {
                            CoreLogger.Log("[DialogueManager] 선택지 액션 시퀀스 중간에 취소 요청 감지. 종료합니다.",CoreLogger.LogLevel.Info,this);
                            break;
                        }

                        if (action == null)
                        {
                            CoreLogger.LogWarning("DialogueManager: 선택지 액션 목록에 null 항목이 있습니다. 건너뜜.", this);
                            continue;
                        }

                        IEnumerator actionExecution = null;
                        bool executionSuccess = false;

                        try
                        {
                            actionExecution = action.Execute(context);
                            executionSuccess = true;
                        }
                        catch (OperationCanceledException)
                        {
                            CoreLogger.Log($"[DialogueManager] 액션 '{action.name}' 실행 중 취소 요청 감지. 시퀀스를 중단합니다.", CoreLogger.LogLevel.Info,this);
                            break;
                        }
                        catch (Exception ex)
                        {
                            ReportError(ex);
                            break;
                        }

                        if (executionSuccess && actionExecution != null)
                        {
                            yield return StartCoroutine(actionExecution);
                        }
                        else if (executionSuccess) // actionExecution이 null인 경우
                        {
                            CoreLogger.LogWarning($"[DialogueManager] 액션 '{action.name}'이 유효한 코루틴을 반환하지 않았습니다. 다음 액션으로 넘어갑니다.", this);
                        }
                    }
                }
            }
            finally // 코루틴이 어떻게 끝나든 리소스 정리 및 상태 초기화
            {
                _isExecutingChoiceActions = false;
                _choiceActionCts?.Dispose();
                _choiceActionCts = null;
                onCompleted?.Invoke(); // 완료 콜백 호출
                CoreLogger.Log("[DialogueManager] 선택지 액션 실행 완료 또는 중단 후 정리.", CoreLogger.LogLevel.Info,this);
            }
        }

        private void EndDialogue()
        {
            if (!isDialogueActive) return; // 이미 비활성 상태라면 중복 호출 방지

            OnDialogueStateChanged?.Invoke(false);

            isDialogueActive = false;
            isDisplayingChoices = false;
            currentDialogueData = null;
            OnDialogueEnded?.Invoke();
            _uiHandler?.Hide(); // _uiHandler가 null일 수도 있으므로 ? 추가
            StopChoiceActions(); // 대화 종료 시 남아있는 선택지 액션도 중단
            CoreLogger.Log("[DialogueManager] 대화 종료.", CoreLogger.LogLevel.Info,this);
        }

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
}