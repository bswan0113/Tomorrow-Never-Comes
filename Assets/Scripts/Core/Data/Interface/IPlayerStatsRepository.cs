// C:\Workspace\Tomorrow Never Comes\Assets\Scripts\Core\Data\Interface\IPlayerStatsRepository.cs

using System.EnterpriseServices;
using Features.Player;
using System.Threading.Tasks;

// ITransaction 인터페이스는 Core.Data.Interface 네임스페이스 내에 정의되어야 합니다.
// 예: public interface ITransaction : System.IDisposable { /* ... */ }
// 다음 단계에서 IDatabaseAccess 인터페이스와 함께 정의될 수 있습니다.

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
        /// 트랜잭션이 제공되면 해당 트랜잭션 내에서 작업을 수행합니다.
        /// </summary>
        /// <param name="data">저장할 PlayerStatsData 객체.</param>
        /// <param name="transaction">현재 저장 작업을 포함할 트랜잭션 객체. 트랜잭션 없이는 저장할 수 없습니다.</param>
        Task SavePlayerStatsAsync(PlayerStatsData data, ITransaction transaction); // 트랜잭션 인자 추가

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