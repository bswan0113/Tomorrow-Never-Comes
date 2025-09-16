// C:\Workspace\Tomorrow Never Comes\Assets\Scripts\Features\UI\Common\DialogueManager.cs (REFACTORED)

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Core.Interface;
using Core.Interface.Core.Interface;
using Core.Logging;
using ScriptableObjects.Abstract;
using ScriptableObjects.Action;
using ScriptableObjects.Data;
using UnityEngine;
using VContainer;
using Cysharp.Threading.Tasks; // UniTask 사용을 위해

namespace Features.UI.Common
{
    public class DialogueManager : IDialogueService, IGameActionContext, IDisposable
    {
        // 의존성
        private readonly IGameResourceService _gameResourceService;
        private readonly IGameService _gameService;

        private IDialogueUIHandler _uiHandler;
        private MonoBehaviour _coroutineRunner; // 코루틴 실행을 위한 대리자

        // IGameActionContext 구현
        public IGameService gameService => _gameService;
        public IDialogueService dialogueService => this;
        public MonoBehaviour coroutineRunner => _coroutineRunner;

        // 선택지 액션
        private CancellationTokenSource _choiceActionCts;
        private bool _isExecutingChoiceActions = false;
        public CancellationToken CancellationToken => _choiceActionCts?.Token ?? CancellationToken.None;

        // 상태
        private Queue<DialogueLine> dialogueQueue;
        private DialogueData currentDialogueData;
        private bool isDialogueActive = false;
        private bool isDisplayingChoices = false;
        private bool canProcessInput = true;
        private const string noneRegisteredIdentifier = "0";

        // 이벤트
        public event Action OnDialogueEnded;
        public event Action<bool> OnDialogueStateChanged;

        [Inject]
        public DialogueManager(IGameResourceService gameResourceService, IGameService gameService)
        {
            _gameResourceService = gameResourceService ?? throw new ArgumentNullException(nameof(gameResourceService));
            _gameService = gameService ?? throw new ArgumentNullException(nameof(gameService));
            dialogueQueue = new Queue<DialogueLine>();
            CoreLogger.Log("[DialogueManager] Constructed via VContainer.");
        }

        public void Dispose()
        {
            StopChoiceActions();
            OnDialogueEnded = null;
            OnDialogueStateChanged = null;
            CoreLogger.Log("[DialogueManager] Disposed.");
        }

        // DialogueSystemUpdater가 대신 호출해 줄 Update 로직
        public void OnUpdate()
        {
            if (isDialogueActive && !isDisplayingChoices && canProcessInput && Input.GetKeyDown(KeyCode.Space))
            {
                canProcessInput = false;

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

        // 코루틴 실행 대리자를 설정하는 메서드
        public void SetCoroutineRunner(MonoBehaviour runner)
        {
            _coroutineRunner = runner;
        }

        public void RegisterDialogueUI(IDialogueUIHandler uiHandler)
        {
            _uiHandler = uiHandler ?? throw new ArgumentNullException(nameof(uiHandler));
            CoreLogger.LogDebug("[DialogueManager] IDialogueUIHandler 등록 완료.");
        }

        public void StartDialogue(string dialogueID)
        {
            if (string.IsNullOrEmpty(dialogueID) || dialogueID == noneRegisteredIdentifier) return;
            DialogueData data = _gameResourceService.GetDataByID<DialogueData>(dialogueID);
            if (data != null) StartDialogue(data);
            else CoreLogger.LogError($"Dialogue ID '{dialogueID}'에 해당하는 DialogueData를 찾을 수 없습니다!");
        }

        public void StartDialogue(DialogueData data)
        {
            if (_uiHandler == null)
            {
                CoreLogger.LogError("DialogueUIHandler가 등록되지 않아 대화를 시작할 수 없습니다!");
                return;
            }
            if (isDialogueActive) return;

            OnDialogueStateChanged?.Invoke(true);
            currentDialogueData = data;
            isDialogueActive = true;
            isDisplayingChoices = false;
            canProcessInput = true;
            _uiHandler.Show();

            dialogueQueue.Clear();
            if(data.dialogueLines != null)
            {
                foreach (var line in data.dialogueLines)
                {
                    if (line != null) dialogueQueue.Enqueue(line);
                }
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
                _coroutineRunner.StartCoroutine(EnableInputAfterDelay(0.2f));
            }
            else
            {
                if (currentDialogueData?.choices?.Count > 0)
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
            if (string.IsNullOrEmpty(speakerID) || speakerID == noneRegisteredIdentifier) return "";
            CharacterData speakerData = _gameResourceService.GetDataByID<CharacterData>(speakerID);
            return speakerData != null ? speakerData.characterName : $"[ID:{speakerID} 없음]";
        }

        public void ProcessChoice(ChoiceData choice)
        {
            if (_isExecutingChoiceActions) return;
            if (choice == null)
            {
                EndDialogue();
                return;
            }

            _coroutineRunner.StartCoroutine(ExecuteChoiceActionsCoroutine(choice));
        }

        private void StopChoiceActions(bool reportCancellationError = false)
        {
            if (!_isExecutingChoiceActions) return;
            _choiceActionCts?.Cancel();
            if (reportCancellationError) ReportError(new OperationCanceledException("Dialogue choice actions were explicitly stopped."));
            _choiceActionCts?.Dispose();
            _choiceActionCts = null;
            _isExecutingChoiceActions = false;
        }

        // Action을 넘기는 대신 ChoiceData를 직접 받아 처리하도록 변경하여 더 깔끔하게 만듦
        private IEnumerator ExecuteChoiceActionsCoroutine(ChoiceData choice)
        {
            _isExecutingChoiceActions = true;
            _choiceActionCts = new CancellationTokenSource();

            try
            {
                if (choice.actions != null)
                {
                    foreach (var action in choice.actions)
                    {
                        if (_choiceActionCts.Token.IsCancellationRequested) break;
                        if (action == null) continue;

                        // action.Execute() 호출 자체에서 발생할 수 있는 동기적인 예외를 잡기 위한 부분
                        IEnumerator actionCoroutine;
                        try
                        {
                            actionCoroutine = action.Execute(this);
                        }
                        catch (Exception ex)
                        {
                            ReportError(ex);
                            yield break; // 예외 발생 시 전체 코루틴 즉시 종료
                        }

                        // yield return은 try-catch 블록 밖에서 안전하게 실행
                        // actionCoroutine 자체의 실행 중 발생하는 예외는 Unity의 Coroutine Runner가 처리합니다. (콘솔에 로그 출력)
                        if (actionCoroutine != null)
                        {
                            yield return _coroutineRunner.StartCoroutine(actionCoroutine);
                        }
                    }
                }
            }
            finally
            {
                _isExecutingChoiceActions = false;
                _choiceActionCts.Dispose();
                _choiceActionCts = null;

                bool isEffectivelyNoNextDialogue = string.IsNullOrEmpty(choice.nextDialogueID) || choice.nextDialogueID == noneRegisteredIdentifier;
                bool containsAdvanceToNextDay = choice.actions != null && choice.actions.Any(a => a is AdvanceDayAction);

                if (isEffectivelyNoNextDialogue || containsAdvanceToNextDay)
                {
                    EndDialogue();
                }
                else
                {
                    StartDialogue(choice.nextDialogueID);
                }
            }
        }

        public void ReportError(Exception ex)
        {
            CoreLogger.LogError($"[DialogueManager] 선택지 액션 실행 중 오류 발생: {ex.Message}\n{ex.StackTrace}");
            StopChoiceActions(true);
            EndDialogue();
        }

        private void EndDialogue()
        {
            if (!isDialogueActive) return;
            OnDialogueStateChanged?.Invoke(false);
            isDialogueActive = false;
            isDisplayingChoices = false;
            currentDialogueData = null;
            OnDialogueEnded?.Invoke();
            _uiHandler?.Hide();
            StopChoiceActions();
            CoreLogger.LogDebug("[DialogueManager] 대화 종료.");
        }

        public bool IsDialogueActive() => isDialogueActive;

        private IEnumerator EnableInputAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            canProcessInput = true;
        }
    }
}