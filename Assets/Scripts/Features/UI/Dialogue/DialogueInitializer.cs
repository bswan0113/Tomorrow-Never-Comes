using Core.Interface;
using Core.Logging;
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
            _dialogueService.RegisterDialogueUI(_dialogueUIHandler);
            CoreLogger.Log("DialogueUIHandler가 DialogueManager에 성공적으로 등록되었습니다.");
        }
    }
}