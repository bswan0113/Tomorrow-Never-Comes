using System.Collections.Generic;
using ScriptableObjects.Data;

namespace Core.Interface
{
    public interface IDialogueUIHandler
    {
        bool IsTyping { get; }
        void SkipTypingEffect();
        void ShowLine(string speakerName, string dialogueText);
        void ShowChoices(List<ChoiceData> choices);
        void Show();
        void Hide();
    }

}