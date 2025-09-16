// C:\Workspace\Tomorrow Never Comes\Assets\Scripts\Core\Data\Interface\IGameProgressRepository.cs

using Features.Data;
using System.Threading.Tasks;

namespace Core.Data.Interface
{
    /// <summary>
    /// GameProgressData에 대한 저장 및 로드 작업을 추상화하는 리포지토리 인터페이스입니다.
    /// </summary>
    public interface IGameProgressRepository
    {
        /// <summary>
        /// 지정된 세이브 슬롯 ID에 해당하는 GameProgressData를 비동기적으로 로드합니다.
        /// </summary>
        /// <param name="saveSlotId">로드할 세이브 슬롯 ID.</param>
        /// <returns>로드된 GameProgressData 객체 또는 데이터가 없을 경우 null.</returns>
        Task<GameProgressData> LoadGameProgressAsync(int saveSlotId);

        /// <summary>
        /// GameProgressData를 비동기적으로 저장합니다. 기존 데이터가 있으면 업데이트하고 없으면 삽입합니다.
        /// </summary>
        /// <param name="data">저장할 GameProgressData 객체.</param>
        Task SaveGameProgressAsync(GameProgressData data);

        /// <summary>
        /// 지정된 세이브 슬롯 ID에 해당하는 GameProgressData를 비동기적으로 삭제합니다.
        /// </summary>
        /// <param name="saveSlotId">삭제할 세이브 슬롯 ID.</param>
        Task DeleteGameProgressAsync(int saveSlotId);

        /// <summary>
        /// 지정된 세이브 슬롯 ID에 GameProgressData가 존재하는지 동기적으로 확인합니다.
        /// </summary>
        /// <param name="saveSlotId">확인할 세이브 슬롯 ID.</param>
        /// <returns>데이터가 존재하면 true, 아니면 false.</returns>
        Task<bool> HasGameProgressDataAsync(int saveSlotId);
    }
}