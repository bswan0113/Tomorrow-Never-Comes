// C:\Workspace\Tomorrow Never Comes\Core\Interface\IPlayerService.cs (새로 생성 또는 수정)

using System;
using System.Threading.Tasks;
using Features.Player; // PlayerStatsData 클래스를 사용하기 위해 추가

namespace Core.Interface
{
    /// <summary>
    /// 플레이어 데이터 및 스탯 관리를 위한 서비스 인터페이스입니다.
    /// 외부 시스템이 플레이어 데이터에 접근하고 수정할 수 있는 공통적인 방법을 제공합니다.
    /// 데이터 저장 책임은 DataManager로 이전되었습니다.
    /// </summary>
    public interface IPlayerService
    {
        /// <summary>
        /// 현재 플레이어 스탯 데이터를 동기적으로 반환합니다.
        /// 데이터가 아직 로드되지 않았거나 null인 경우, 기본값으로 초기화된 데이터를 반환합니다.
        /// </summary>
        /// <returns>현재 플레이어의 스탯 데이터</returns>
        PlayerStatsData GetCurrentPlayerStats();

        /// <summary>
        /// 플레이어의 소지금(Money)을 지정된 양만큼 추가하거나 감소시킵니다.
        /// </summary>
        /// <param name="amount">추가 또는 감소시킬 소지금의 양 (음수 가능)</param>
        void AddMoney(long amount);

        /// <summary>
        /// 플레이어의 지성(Intellect)을 지정된 양만큼 추가하거나 감소시킵니다.
        /// </summary>
        /// <param name="amount">추가 또는 감소시킬 지성의 양 (음수 가능)</param>
        void AddIntellect(int amount);

        /// <summary>
        /// 플레이어의 매력(Charm)을 지정된 양만큼 추가하거나 감소시킵니다.
        /// </summary>
        /// <param name="amount">추가 또는 감소시킬 매력의 양 (음수 가능)</param>
        void AddCharm(int amount);

        // TODO: 다른 스탯 변경 메서드들을 여기에 추가하세요 (예: AddExperience, ChangeHealth, LevelUp 등)
        // void AddExperience(long amount);
        // void ChangeHealth(int amount);

        /// <summary>
        /// 플레이어 데이터를 비동기적으로 로드합니다.
        /// 이 메서드는 서비스 초기화 시점에 주로 사용됩니다.
        /// </summary>
        /// <returns>비동기 로드 작업을 나타내는 Task</returns>
        Task LoadPlayerDataAsync();


        /// <summary>
        /// 플레이어 스탯 데이터가 변경될 때 발생하는 이벤트입니다.
        /// 이 이벤트를 구독하여 UI 업데이트 또는 다른 게임 로직을 트리거할 수 있습니다.
        /// </summary>
        event Action OnPlayerStatsChanged;
    }
}