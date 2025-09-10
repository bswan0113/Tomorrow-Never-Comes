using System.Collections.Generic;
using Core.Interface.Core.Interface;

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