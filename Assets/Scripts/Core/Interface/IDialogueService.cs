namespace Core.Interface
{
    // Scripts/Core/Interface/IDialogueService.cs
    using System;
    using System.Collections.Generic; // ChoiceData, BaseAction 등을 위해 필요

// DialogueData, CharacterData, ChoiceData, DialogueLine, BaseAction 등의 클래스 정의가 필요합니다.
// 이들은 ScriptableObject이거나 일반 C# 클래스일 수 있습니다.

    namespace Core.Interface
    {
        // DialogueUIHandler는 MonoBehaviour이므로, 이를 추상화하는 인터페이스도 필요할 수 있습니다.
        // 여기서는 일단 DialogueUIHandler를 직접 주입받는다고 가정하고, 필요하다면 IUIHandler 같은 인터페이스를 추가합니다.

        public interface IDialogueService
        {
            // 초기화 메서드 (필요한 의존성 주입)
            void Initialize(IGameResourceService gameResourceService); // DialogueUIHandler를 인터페이스로 받음

            void StartDialogue(string dialogueID);
            void StartDialogue(DialogueData data);
            void RegisterDialogueUI(IDialogueUIHandler uiHandler); // DialogueUIHandler를 인터페이스로 받음

            bool IsDialogueActive(); // 현재 대화 활성화 상태
            // ProcessChoice는 외부에서 호출될 수 있으므로 public으로 유지하고 인터페이스에 추가합니다.
            void ProcessChoice(ChoiceData choice);

            event Action OnDialogueEnded;
            event Action<bool> OnDialogueStateChanged;

            // BaseAction.Execute(this) 부분이 DialogueManager가 BaseAction에 필요한 Context를 제공한다는 의미이므로,
            // BaseAction에 필요한 최소한의 Context 인터페이스 (예: IGameActionContext)를 정의하고
            // DialogueManager가 이를 구현하도록 할 수도 있습니다.
            // 현재는 편의상 DialogueManager 자체를 Context로 넘겨주고 있으므로, 인터페이스에 특정 BaseAction 관련 메서드는 추가하지 않습니다.
        }
    }
}