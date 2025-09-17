// C:\Workspace\Tomorrow Never Comes\Assets\Scripts\Features\UI\DialogueManager.cs (REFACTORED)

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Core.Interface;
using Core.Logging;
using ScriptableObjects.Abstract;
using ScriptableObjects.Action;
using ScriptableObjects.Data;
using UnityEngine;
using VContainer;
using Cysharp.Threading.Tasks;

namespace Features.UI.Common
{
    public class DialogueManager : IDialogueService, IGameActionContext, IDisposable
    {
        // 의존성
        private readonly IGameResourceService _gameResourceService;
        private readonly IGameService _gameService;
        private readonly IPlayerService _playerService; // IPlayerService 의존성 추가

        private IDialogueUIHandler _uiHandler;
        private MonoBehaviour _coroutineRunner;

        // IGameActionContext 구현
        public IGameService gameService => _gameService;
        public IDialogueService dialogueService => this;
        public IPlayerService playerService => _playerService; // IPlayerService 속성 구현 추가
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
        public DialogueManager(IGameResourceService gameResourceService, IGameService gameService, IPlayerService playerService) // IPlayerService 인자 추가
        {
            _gameResourceService = gameResourceService ?? throw new ArgumentNullException(nameof(gameResourceService));
            _gameService = gameService ?? throw new ArgumentNullException(nameof(gameService));
            _playerService = playerService ?? throw new ArgumentNullException(nameof(playerService)); // IPlayerService 주입 및 할당
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
            if (data.dialogueLines != null)
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

                        IEnumerator actionCoroutine;
                        try
                        {
                            actionCoroutine = action.Execute(this);
                        }
                        catch (Exception ex)
                        {
                            ReportError(ex);
                            yield break;
                        }

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
                _choiceActionCts?.Dispose();
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