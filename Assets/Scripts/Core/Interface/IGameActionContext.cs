using Core.Interface.Core.Interface;
using UnityEngine; // MonoBehaviour를 상속하는 executor를 위해 필요

namespace Core.Interface
{
    public interface IGameActionContext
    {
        IGameService gameService { get; }
        IDialogueService dialogueService { get; }
        MonoBehaviour coroutineRunner { get; }
    }
}
