// C:\Workspace\Tomorrow Never Comes\Assets\Scripts\Features\World\ActionSequencer.cs

using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Core.Interface;
using Core.Interface.Core.Interface;
using Core.Logging;
using UnityEngine;
using VContainer;

namespace Features.World
{
    public class ActionSequencer : MonoBehaviour, IGameActionContext
    {
        public List<BaseAction> actions;

        private Coroutine _sequenceCoroutine;
        private CancellationTokenSource _cancellationTokenSource;
        private bool _isRunning = false;

        private IGameService _gameService;
        private IDialogueService _dialogueService;

        // IGameActionContext 구현
        public IGameService gameService => _gameService;
        public IDialogueService dialogueService => _dialogueService;
        public MonoBehaviour coroutineRunner => this;
        public CancellationToken CancellationToken => _cancellationTokenSource?.Token ?? CancellationToken.None;

        public void ReportError(Exception ex)
        {
            CoreLogger.LogError($"[ActionSequencer] 액션 실행 중 오류 발생: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}", this);
            StopSequence(true); // 오류 발생 시 시퀀스 강제 중단
        }

        [Inject]
        public void Construct(IGameService gameService, IDialogueService dialogueService)
        {
            _gameService = gameService ?? throw new ArgumentNullException(nameof(gameService));
            _dialogueService = dialogueService ?? throw new ArgumentNullException(nameof(dialogueService));
        }

        private void OnDisable()
        {
            StopSequence();
        }

        private void OnDestroy()
        {
            StopSequence();
        }

        public void ExecuteSequence()
        {
            if (_isRunning)
            {
                CoreLogger.LogWarning("ActionSequencer가 이미 실행 중입니다. 새로운 실행 요청을 무시합니다.", this);
                return;
            }

            if (_sequenceCoroutine != null)
            {
                StopCoroutine(_sequenceCoroutine);
                _sequenceCoroutine = null;
            }

            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = new CancellationTokenSource();

            _isRunning = true;
            _sequenceCoroutine = StartCoroutine(SequenceCoroutineInternal());
        }

        public void StopSequence(bool reportCancellationError = false)
        {
            if (!_isRunning) return;
            // _isRunning = false; // 코루틴이 완전히 종료된 후 SequenceCoroutineInternal의 finally 블록에서 설정하도록 변경

            CoreLogger.Log("[ActionSequencer] 시퀀스 중단 요청.",  CoreLogger.LogLevel.Info,this);

            _cancellationTokenSource?.Cancel();

            if (_sequenceCoroutine != null)
            {
                StopCoroutine(_sequenceCoroutine);
                _sequenceCoroutine = null;
            }

            // _cancellationTokenSource?.Dispose(); // Dispose는 코루틴이 확실히 종료된 후 SequenceCoroutineInternal의 finally에서 처리
            // _cancellationTokenSource = null;

            if (reportCancellationError)
            {
                // ReportError를 호출하면 재귀적으로 StopSequence가 호출될 수 있으므로, 예외만 로그에 남기거나
                // ReportError 내부에서 _isRunning 상태를 확인해야 합니다.
                // 여기서는 ReportError가 StopSequence를 호출하므로, 이미 중단 프로세스 중이라면 로그만 남깁니다.
                // (현재 ReportError 구현은 StopSequence를 호출하므로, 이 플로우는 괜찮습니다.)
                CoreLogger.LogWarning("[ActionSequencer] 오류로 인해 시퀀스가 중단되었습니다.", this);
            }

            // _isRunning = false; // 코루틴이 완전히 종료된 후 SequenceCoroutineInternal의 finally 블록에서 설정
            CoreLogger.Log("[ActionSequencer] 시퀀스 중단 요청 처리 완료 (코루틴 종료 대기 중).");
        }


        private IEnumerator SequenceCoroutineInternal()
        {
            IGameActionContext context = this;
            CancellationToken token = _cancellationTokenSource.Token;
            bool sequenceInterrupted = false;

            try // 코루틴 전체를 try-finally로 감싸서 리소스 정리를 보장합니다.
            {
                foreach (var action in actions)
                {
                    if (token.IsCancellationRequested)
                    {
                        CoreLogger.Log("[ActionSequencer] 시퀀스 중간에 취소 요청 감지. 종료합니다.",  CoreLogger.LogLevel.Info,this);
                        sequenceInterrupted = true;
                        break;
                    }

                    if (action == null)
                    {
                        CoreLogger.LogWarning("ActionSequencer에 비어있는(null) Action이 있습니다. 건너뜁니다.", this);
                        continue;
                    }

                    // --- P2: 액션 시퀀서 확장 가드 구현 ---
                    if (!action.IsValid(context, out string validationErrorReason))
                    {
                        // 유효하지 않은 액션 발견 시, 예외를 발생시켜 ReportError로 전달하고 시퀀스 중단
                        throw new InvalidOperationException($"Action '{action.name}' is not valid: {validationErrorReason}");
                    }
                    // ------------------------------------

                    IEnumerator actionExecution = null;
                    bool executionSuccess = false;

                    try
                    {
                        actionExecution = action.Execute(context); // 동기적 예외(생성 오류) 포착
                        executionSuccess = true;
                    }
                    catch (OperationCanceledException)
                    {
                        CoreLogger.Log($"[ActionSequencer] 액션 '{action.name}' 실행 중 취소 요청 감지. 시퀀스를 중단합니다.",  CoreLogger.LogLevel.Info,this);
                        sequenceInterrupted = true;
                        break;
                    }
                    catch (Exception ex)
                    {
                        ReportError(ex); // 모든 다른 예외는 ReportError를 통해 보고
                        sequenceInterrupted = true;
                        break;
                    }

                    if (executionSuccess && actionExecution != null)
                    {
                        // 비동기 예외(코루틴 실행 중 오류)는 BaseAction 내부의 try-catch + context.ReportError()를 통해 처리되어야 합니다.
                        yield return StartCoroutine(actionExecution);
                    }
                    else if (executionSuccess) // actionExecution이 null (즉시 완료 액션)
                    {
                        // CoreLogger.LogWarning($"[ActionSequencer] 액션 '{action.name}'이 유효한 코루틴을 반환하지 않았습니다. (즉시 완료로 간주)", this);
                        yield return null; // 만약의 경우를 대비해 한 프레임 대기
                    }

                    // ReportError가 호출되면 _cancellationTokenSource가 Cancel되고,
                    // 다음 루프에서 token.IsCancellationRequested가 true가 되어 루프가 중단됩니다.
                }
            }
            finally // 코루틴이 어떻게 끝나든 (정상 완료, 취소, 예외) 항상 실행됩니다.
            {
                _isRunning = false;
                _sequenceCoroutine = null;

                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;

                if (sequenceInterrupted)
                {
                    CoreLogger.Log("[ActionSequencer] 시퀀스 비정상 종료 및 리소스 해제 완료.");
                }
                else
                {
                    CoreLogger.Log("[ActionSequencer] 시퀀스 정상 실행 완료 및 리소스 해제 완료.");
                }
            }
        }
    }
}