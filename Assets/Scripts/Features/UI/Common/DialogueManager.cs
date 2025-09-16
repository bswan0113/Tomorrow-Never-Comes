// C:\Workspace\Tomorrow Never Comes\Assets\Scripts\Features\UI\Common\DialogueManager.cs

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
using VContainer; // VContainer를 사용하여 의존성 주입을 명확히 함

namespace Features.UI.Common
{
    /// <summary>
    /// 게임의 전체 대화 흐름을 관리하는 매니저입니다.
    /// 데이터(SO)와 UI(Handler) 사이의 다리 역할을 합니다.
    /// VContainer를 통해 의존성을 주입받아 사용합니다.
    /// </summary>
    public class DialogueManager : MonoBehaviour, IDialogueService, IGameActionContext
    {
        // 의존성 주입을 위한 필드
        private IGameResourceService _gameResourceService;
        private IDialogueUIHandler _uiHandler;
        private IGameService _gameService;

        // VContainer를 통한 생성자 주입
        // MonoBehaviour이므로 생성자 주입은 불가능합니다.
        // 대신 VContainer의 [Inject] 필드/메서드 주입을 사용하거나,
        // 별도의 Initializer 클래스에서 Initialize 메서드를 호출하도록 합니다.
        // 현재 Initialize 메서드를 사용하는 방식이 적절합니다.

        // IGameActionContext 인터페이스 구현
        public IGameService gameService => _gameService;
        public IDialogueService dialogueService => this; // DialogueManager 자신이 IDialogueService 구현
        public MonoBehaviour coroutineRunner => this; // 코루틴 실행을 위해 자신을 MonoBehaviour로 노출

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
            StopChoiceActions(true); // 오류 발생 시 액션 강제 중단 및 취소 예외 보고
            EndDialogue(); // 오류 발생 시 대화 종료
        }

        private Queue<DialogueLine> dialogueQueue;
        private DialogueData currentDialogueData;

        private bool isDialogueActive = false;
        private bool isDisplayingChoices = false;
        private bool canProcessInput = true; // 입력 처리가 가능한 상태 (예: 타이핑 중이거나 딜레이 중이 아님)
        private const string noneRegisteredIdentifier = "0";

        // 인스턴스 이벤트로 변경되었으므로, 외부에서 구독 및 해지 필요
        public event Action OnDialogueEnded;
        public event Action<bool> OnDialogueStateChanged;

        /// <summary>
        /// DialogueManager를 초기화하고 필요한 서비스들을 주입합니다.
        /// VContainer 라이프사이클의 적절한 지점(예: SceneLifetimeScope)에서 호출되어야 합니다.
        /// </summary>
        [Inject] // VContainer가 이 메서드를 호출하여 의존성을 주입합니다.
        public void Construct(IGameResourceService gameResourceService, IGameService gameService)
        {
            _gameResourceService = gameResourceService ?? throw new ArgumentNullException(nameof(gameResourceService));
            _gameService = gameService ?? throw new ArgumentNullException(nameof(gameService));
            CoreLogger.Log("[DialogueManager] Initialized via VContainer [Inject] Construct method.");

            // 초기화 로직 (필요하다면 여기에 추가)
            // 예: _gameResourceService.LoadDialogueAssets();
        }
        void Awake()
        {
            dialogueQueue = new Queue<DialogueLine>();
            // CoreLogger.LogDebug("[DialogueManager] Awake - Singleton 로직 제거됨."); // 불필요한 로그 제거
        }

        /// <summary>
        /// GameObject 파괴 시 리소스 클린업을 수행합니다.
        /// </summary>
        private void OnDestroy()
        {
            StopChoiceActions(); // 선택지 액션 실행 중이었다면 중단하고 CancellationTokenSource 해제
            // DialogueManager는 이벤트를 발행하는 주체이므로, 자신이 발행하는 이벤트를 구독 해지할 필요는 없습니다.
            // 대신, DialogueManager를 구독하는 객체들이 OnDisable/OnDestroy에서 구독을 해지해야 합니다.
            // StopAllCoroutines(); // 명시적으로 모든 코루틴 중지 (선택 사항이지만 안전을 위해)
        }

        void Update()
        {
            // 대화가 활성 상태이고, 선택지를 표시 중이 아니며, 입력 처리가 가능할 때
            // 마우스 클릭 또는 특정 대화 진행 키(예: Space)를 사용하도록 변경할 수 있습니다.
            // Input.anyKeyDown은 어떤 키든 눌리면 반응하므로 의도치 않은 동작을 유발할 수 있습니다.
            if (isDialogueActive && !isDisplayingChoices && canProcessInput && Input.GetKeyDown(KeyCode.Space)) // 또는 Input.GetKeyDown(KeyCode.Space) 등
            {
                canProcessInput = false; // 중복 입력 방지

                if (_uiHandler != null && _uiHandler.IsTyping)
                {
                    _uiHandler.SkipTypingEffect();
                    canProcessInput = true; // 타이핑 스킵 후 바로 다음 입력 가능
                }
                else
                {
                    DisplayNextLine();
                }
            }
        }

        /// <summary>
        /// 대화 UI 핸들러를 등록합니다. 이 핸들러를 통해 대화 UI를 제어합니다.
        /// </summary>
        /// <param name="uiHandler">대화 UI를 제어하는 IDialogueUIHandler 구현체.</param>
        public void RegisterDialogueUI(IDialogueUIHandler uiHandler)
        {
            _uiHandler = uiHandler ?? throw new ArgumentNullException(nameof(uiHandler));
            CoreLogger.LogDebug("[DialogueManager] IDialogueUIHandler 등록 완료.");
        }

        /// <summary>
        /// 주어진 Dialogue ID로 대화를 시작합니다.
        /// </summary>
        /// <param name="dialogueID">시작할 대화 데이터의 ID.</param>
        public void StartDialogue(string dialogueID)
        {
            // _gameResourceService는 Initialize에서 주입되므로 null이 아닐 것이라 기대
            if (_gameResourceService == null)
            {
                CoreLogger.LogError("DialogueManager: IGameResourceService가 초기화되지 않았습니다. Initialize() 호출을 확인하세요.", this);
                return;
            }

            if (string.IsNullOrEmpty(dialogueID) || dialogueID == noneRegisteredIdentifier)
            {
                CoreLogger.LogWarning($"DialogueManager: 유효하지 않은 Dialogue ID '{dialogueID}'로 대화 시작 요청을 무시합니다.", this);
                return;
            }

            DialogueData data = _gameResourceService.GetDataByID<DialogueData>(dialogueID);
            if (data != null)
            {
                StartDialogue(data);
            }
            else
            {
                CoreLogger.LogError($"Dialogue ID '{dialogueID}'에 해당하는 DialogueData를 찾을 수 없습니다!", this);
            }
        }

        /// <summary>
        /// 주어진 DialogueData로 대화를 시작합니다.
        /// </summary>
        /// <param name="data">시작할 DialogueData 객체.</param>
        public void StartDialogue(DialogueData data)
        {
            // _uiHandler는 RegisterDialogueUI를 통해 주입되므로 null이 아닐 것이라 기대
            if (_uiHandler == null)
            {
                CoreLogger.LogError("DialogueUIHandler가 등록되지 않아 대화를 시작할 수 없습니다! RegisterDialogueUI() 호출을 확인하세요.", this);
                return;
            }
            if (isDialogueActive)
            {
                CoreLogger.LogWarning("DialogueManager: 이미 대화가 활성 상태입니다. 새로운 대화 요청을 무시합니다.", this);
                return;
            }

            OnDialogueStateChanged?.Invoke(true); // 대화 시작 이벤트 발행

            currentDialogueData = data;
            isDialogueActive = true;
            isDisplayingChoices = false;
            canProcessInput = true; // 대화 시작 시 입력 가능 상태로 설정

            _uiHandler.Show(); // UI 표시

            dialogueQueue.Clear();
            if (data.dialogueLines != null)
            {
                foreach (var line in data.dialogueLines)
                {
                    if (line != null) dialogueQueue.Enqueue(line);
                    else CoreLogger.LogWarning($"DialogueData '{data.id}'에 null 대화 라인 항목이 있습니다. 건너뜜.", this);
                }
            }
            else
            {
                CoreLogger.LogWarning($"DialogueData '{data.id}'에 대화 라인이 없습니다. 선택지로 바로 넘어갑니다.", this);
            }

            DisplayNextLine(); // 첫 번째 라인 표시
        }

        /// <summary>
        /// 대화 큐에서 다음 라인을 가져와 화면에 표시하거나, 선택지를 표시합니다.
        /// </summary>
        private void DisplayNextLine()
        {
            if (dialogueQueue.Count > 0)
            {
                DialogueLine currentLine = dialogueQueue.Dequeue();
                string speakerName = GetSpeakerName(currentLine.speakerID);
                _uiHandler.ShowLine(speakerName, currentLine.dialogueText);

                // 입력 딜레이 적용
                StartCoroutine(EnableInputAfterDelay(0.2f));
            }
            else // 모든 대화 라인이 표시되면 선택지 또는 대화 종료
            {
                if (currentDialogueData != null && currentDialogueData.choices != null && currentDialogueData.choices.Count > 0)
                {
                    isDisplayingChoices = true;
                    _uiHandler.ShowChoices(currentDialogueData.choices);
                }
                else
                {
                    EndDialogue(); // 선택지가 없으면 대화 종료
                }
            }
        }

        /// <summary>
        /// Speaker ID에 해당하는 캐릭터 이름을 가져옵니다.
        /// </summary>
        /// <param name="speakerID">캐릭터 데이터의 ID.</param>
        /// <returns>캐릭터 이름 또는 오류 메시지.</returns>
        private string GetSpeakerName(string speakerID)
        {
            if (_gameResourceService == null)
            {
                CoreLogger.LogError("DialogueManager: IGameResourceService가 초기화되지 않아 화자 이름을 가져올 수 없습니다.", this);
                return "[ERR: 서비스 없음]";
            }

            if (string.IsNullOrEmpty(speakerID) || speakerID == noneRegisteredIdentifier) return ""; // 빈 이름

            CharacterData speakerData = _gameResourceService.GetDataByID<CharacterData>(speakerID);
            if (speakerData != null)
            {
                return speakerData.characterName;
            }

            CoreLogger.LogError($"Character ID '{speakerID}'에 해당하는 CharacterData를 찾을 수 없습니다!", this);
            return $"[ID:{speakerID} 없음]"; // 찾지 못했을 경우의 메시지
        }

        /// <summary>
        /// 사용자가 선택한 선택지를 처리하고 관련 액션을 실행합니다.
        /// </summary>
        /// <param name="choice">사용자가 선택한 ChoiceData.</param>
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

            // 다음 대화가 없거나, AdvanceDayAction을 포함하는 경우 대화 종료 로직을 따름
            bool isEffectivelyNoNextDialogue = string.IsNullOrEmpty(choice.nextDialogueID) || choice.nextDialogueID == noneRegisteredIdentifier;
            bool containsAdvanceToNextDay = choice.actions != null && choice.actions.Any(action => action is AdvanceDayAction);

            // 선택지 액션 실행 코루틴 시작
            StartCoroutine(ExecuteChoiceActionsCoroutine(choice.actions, () =>
            {
                canProcessInput = true; // 액션 실행 완료 후 입력 재활성화

                if (isEffectivelyNoNextDialogue || containsAdvanceToNextDay)
                {
                    EndDialogue(); // 대화 종료
                }
                else
                {
                    StartDialogue(choice.nextDialogueID); // 다음 대화 시작
                }
            }));
        }

        /// <summary>
        /// 현재 실행 중인 선택지 액션을 중단하고 관련 리소스를 해제합니다.
        /// </summary>
        /// <param name="reportCancellationError">취소 시 OperationCanceledException을 ReportError로 보고할지 여부.</param>
        private void StopChoiceActions(bool reportCancellationError = false)
        {
            if (!_isExecutingChoiceActions) return; // 실행 중인 액션이 없으면 아무것도 하지 않음

            CoreLogger.LogDebug("[DialogueManager] 선택지 액션 실행 중단 요청.", this);

            _choiceActionCts?.Cancel(); // 모든 액션에 취소 요청

            if (reportCancellationError)
            {
                ReportError(new OperationCanceledException("Dialogue choice actions were explicitly stopped due to an error."));
            }

            _choiceActionCts?.Dispose(); // CancellationTokenSource 리소스 해제
            _choiceActionCts = null;
            _isExecutingChoiceActions = false;
            CoreLogger.LogDebug("[DialogueManager] 선택지 액션 실행 중단 및 리소스 해제 완료.", this);
        }

        /// <summary>
        /// 주어진 액션 목록을 순차적으로 실행하는 코루틴.
        /// </summary>
        /// <param name="actions">실행할 BaseAction 목록.</param>
        /// <param name="onCompleted">모든 액션 실행 완료 또는 중단 시 호출될 콜백.</param>
        private IEnumerator ExecuteChoiceActionsCoroutine(List<BaseAction> actions, Action onCompleted)
        {
            _isExecutingChoiceActions = true;
            _choiceActionCts?.Dispose(); // 이전 CancellationTokenSource가 남아있을 경우 해제
            _choiceActionCts = new CancellationTokenSource();
            CancellationToken token = _choiceActionCts.Token;

            // DialogueManager 자신이 IGameActionContext 역할을 수행
            IGameActionContext context = this;

            try
            {
                if (actions != null)
                {
                    foreach (var action in actions)
                    {
                        if (token.IsCancellationRequested) // 액션 실행 전 취소 요청 확인
                        {
                            CoreLogger.LogDebug("[DialogueManager] 선택지 액션 시퀀스 중간에 취소 요청 감지. 종료합니다.", this);
                            break;
                        }

                        if (action == null)
                        {
                            CoreLogger.LogWarning("DialogueManager: 선택지 액션 목록에 null 항목이 있습니다. 건너뜜.", this);
                            continue;
                        }

                        IEnumerator actionExecution = null;
                        bool executionStarted = false;

                        try
                        {
                            actionExecution = action.Execute(context);
                            executionStarted = true;
                        }
                        catch (OperationCanceledException)
                        {
                            CoreLogger.LogDebug($"[DialogueManager] 액션 '{action.name}' 실행 중 취소 요청 감지. 시퀀스를 중단합니다.", this);
                            break;
                        }
                        catch (Exception ex)
                        {
                            ReportError(ex); // 액션 실행 중 발생한 예외 보고
                            break; // 오류 발생 시 시퀀스 중단
                        }

                        if (executionStarted && actionExecution != null)
                        {
                            yield return StartCoroutine(actionExecution); // 액션 코루틴 실행 대기
                        }
                        else if (executionStarted) // actionExecution이 null인 경우 (즉시 완료되는 액션 등)
                        {
                            // CoreLogger.LogDebug($"[DialogueManager] 액션 '{action.name}'이 코루틴을 반환하지 않았습니다. 즉시 완료로 처리.", this);
                        }
                    }
                }
            }
            finally // 코루틴이 어떻게 끝나든 (정상 완료, 중단, 예외) 리소스 정리 및 상태 초기화
            {
                _isExecutingChoiceActions = false;
                _choiceActionCts?.Dispose(); // CancellationTokenSource 리소스 해제
                _choiceActionCts = null;
                onCompleted?.Invoke(); // 완료 콜백 호출
                CoreLogger.LogDebug("[DialogueManager] 선택지 액션 실행 완료 또는 중단 후 정리.", this);
            }
        }

        /// <summary>
        /// 현재 진행 중인 대화를 종료하고 관련 UI를 숨깁니다.
        /// </summary>
        private void EndDialogue()
        {
            if (!isDialogueActive) return; // 이미 비활성 상태라면 중복 호출 방지

            OnDialogueStateChanged?.Invoke(false); // 대화 종료 이벤트 발행

            isDialogueActive = false;
            isDisplayingChoices = false;
            currentDialogueData = null; // 현재 대화 데이터 초기화
            OnDialogueEnded?.Invoke(); // 대화 종료 완료 이벤트 발행
            _uiHandler?.Hide(); // UI 숨기기 (null 체크 추가)
            StopChoiceActions(); // 대화 종료 시 남아있는 선택지 액션도 중단 및 리소스 해제

            CoreLogger.LogDebug("[DialogueManager] 대화 종료.", this);
        }

        /// <summary>
        /// 현재 대화가 활성 상태인지 여부를 반환합니다.
        /// </summary>
        public bool IsDialogueActive()
        {
            return isDialogueActive;
        }

        /// <summary>
        /// 지정된 딜레이 시간 후에 입력 처리를 다시 활성화하는 코루틴.
        /// </summary>
        private IEnumerator EnableInputAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            canProcessInput = true;
        }
    }
}