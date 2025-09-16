// Core/Interface/IDataService.cs (수정 제안)

using System.Threading.Tasks;

namespace Core.Data.Interface
{
    public interface IDataService
    {
        bool HasSaveData { get; }

        Task CheckSaveDataAsync(int saveSlotId = 1);

        Task SaveAllGameData(int saveSlotId = 1);

        // 제네릭 메서드를 통해 특정 타입의 데이터를 로드/저장/삭제
        Task<T> LoadDataAsync<T>(int saveSlotId) where T : class;
        Task SaveDataAsync<T>(T data) where T : class;
        Task DeleteDataAsync<T>(int saveSlotId) where T : class;
    }
}