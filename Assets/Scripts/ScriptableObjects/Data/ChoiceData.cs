using System.Collections.Generic;
using ScriptableObjects.Abstract;
using UnityEngine;

namespace ScriptableObjects.Data
{
    [CreateAssetMenu(fileName = "New Choice", menuName = "Game Data/Choice Data")]
    public class ChoiceData : GameData // GameData를 상속하여 고유 ID를 가짐
    {

        [TextArea(2, 5)]
        public string choiceText;
        public string nextDialogueID;
        public List<BaseAction> actions;
    }
}

