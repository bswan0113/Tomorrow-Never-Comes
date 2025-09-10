using System;
using System.Collections;
using System.Collections.Generic;
using Core.Interface;
using Core.Interface.Core.Interface;
using UnityEngine;
using VContainer;

namespace Features.World
{
    public class ActionSequencer : MonoBehaviour, IGameActionContext
    {
        public List<BaseAction> actions;

        private Coroutine sequenceCoroutine;

        private IGameService _gameService;
        private IDialogueService _dialogueService;

        // IGameActionContext 인터페이스 구현
        public IGameService gameService => _gameService;
        public IDialogueService dialogueService => _dialogueService;
        public MonoBehaviour coroutineRunner => this;

        [Inject]
        public void Construct(IGameService gameService, IDialogueService dialogueService)
        {
            _gameService = gameService ?? throw new ArgumentNullException(nameof(gameService));
            _dialogueService = dialogueService ?? throw new ArgumentNullException(nameof(dialogueService));
            Debug.Log("[ActionSequencer] 의존성 주입 완료.");
        }

        public void ExecuteSequence()
        {
            if (sequenceCoroutine != null)
            {
                Debug.LogWarning("ActionSequencer가 이미 실행 중입니다. 이전 시퀀스를 중단하고 새 시퀀스를 시작합니다.", this);
                StopCoroutine(sequenceCoroutine);
                sequenceCoroutine = null;
            }
            sequenceCoroutine = StartCoroutine(SequenceCoroutineInternal());
        }

        private IEnumerator SequenceCoroutineInternal()
        {
            IGameActionContext context = this;

            foreach (var action in actions)
            {
                if (action == null)
                {
                    Debug.LogWarning("ActionSequencer에 비어있는(null) Action이 있습니다.", this);
                    continue;
                }

                IEnumerator actionExecution = action.Execute(context);

                if (actionExecution != null)
                {
                    yield return StartCoroutine(actionExecution);
                }
                else
                {
                    Debug.LogWarning($"[ActionSequencer] 액션 '{action.name}'이 유효한 코루틴을 반환하지 않았습니다. 다음 액션으로 넘어갑니다.", this);
                }
            }

            sequenceCoroutine = null;
            Debug.Log("[ActionSequencer] 시퀀스 실행 완료.");
        }

    }
}