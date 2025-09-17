
// Scripts/Core/Interface/IGameService.cs
using System;
using System.Threading.Tasks;
using ScriptableObjects.Data; // Action 이벤트를 위해 필요

namespace Core.Interface
{
    public interface IGameService
    {
        int DayCount { get; }
        int CurrentActionPoint { get; }

        bool UseActionPoint(int amount);
        Task AdvanceToNextDay();

        // static 대신 인스턴스 이벤트로 변경하여 인터페이스에 포함
        event Action OnDayStart;
        event Action OnActionPointChanged;
        bool EvaluateCondition(ConditionData condition);
    }
}
