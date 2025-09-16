// C:\Workspace\Tomorrow Never Comes\Assets\Scripts\Core\Data\Interface\IPlayerStatsRepository.cs

using Features.Player;
using System.Threading.Tasks;

namespace Core.Data.Interface
{
    /// <summary>
    /// PlayerStatsData에 대한 저장 및 로드 작업을 추상화하는 리포지토리 인터페이스입니다.
    /// </summary>
    public interface IPlayerStatsRepository
    {
        /// <summary>
        /// 지정된 세이브 슬롯 ID에 해당하는 PlayerStatsData를 비동기적으로 로드합니다.
        /// </summary>
        /// <param name="saveSlotId">로드할 세이브 슬롯 ID.</param>
        /// <returns>로드된 PlayerStatsData 객체 또는 데이터가 없을 경우 null.</returns>
        Task<PlayerStatsData> LoadPlayerStatsAsync(int saveSlotId);

        /// <summary>
        /// PlayerStatsData를 비동기적으로 저장합니다. 기존 데이터가 있으면 업데이트하고 없으면 삽입합니다.
        /// </summary>
        /// <param name="data">저장할 PlayerStatsData 객체.</param>
        Task SavePlayerStatsAsync(PlayerStatsData data);

        /// <summary>
        /// 지정된 세이브 슬롯 ID에 해당하는 PlayerStatsData를 비동기적으로 삭제합니다.
        /// </summary>
        /// <param name="saveSlotId">삭제할 세이브 슬롯 ID.</param>
        Task DeletePlayerStatsAsync(int saveSlotId);

        /// <summary>
        /// 지정된 세이브 슬롯 ID에 PlayerStatsData가 존재하는지 동기적으로 확인합니다.
        /// </summary>
        /// <param name="saveSlotId">확인할 세이브 슬롯 ID.</param>
        /// <returns>데이터가 존재하면 true, 아니면 false.</returns>
        Task<bool> HasPlayerStatsDataAsync(int saveSlotId);
    }
}