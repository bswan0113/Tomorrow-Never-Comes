using Core.Interface;
using Core.Interface.Core.Interface;
using UnityEngine;

namespace Features.UI.Common
{
    using VContainer;
    using VContainer.Unity;

    public class DialogueInitializer : IStartable
    {
        private readonly IDialogueService _dialogueService;
        private readonly IDialogueUIHandler _dialogueUIHandler;

        [Inject]
        public DialogueInitializer(
            IDialogueService dialogueService,
            IDialogueUIHandler dialogueUIHandler)
        {
            _dialogueService = dialogueService;
            _dialogueUIHandler = dialogueUIHandler;
        }

        public void Start()
        {
            // DialogueUIHandler를 DialogueManager에 등록
            _dialogueService.RegisterDialogueUI(_dialogueUIHandler);
            Debug.Log("DialogueUIHandler가 DialogueManager에 성공적으로 등록되었습니다.");
        }
    }
}