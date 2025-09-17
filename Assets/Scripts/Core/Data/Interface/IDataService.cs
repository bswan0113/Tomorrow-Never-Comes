// Core/Interface/IDataService.cs (수정 제안)

using System.Threading.Tasks;
using Features.Data;   // GameProgressData를 사용하기 위해 추가
using Features.Player; // PlayerStatsData를 사용하기 위해 추가

namespace Core.Data.Interface
{
    public interface IDataService
    {
        bool HasSaveData { get; }

        Task CheckSaveDataAsync(int saveSlotId = 1);

        /// <summary>
        /// 여러 게임 데이터를 하나의 원자적 트랜잭션으로 묶어 저장합니다.
        /// 순환 참조를 방지하기 위해, 호출자가 저장할 데이터를 직접 전달해야 합니다.
        /// </summary>
        /// <param name="gameProgress">저장할 게임 진행 데이터.</param>
        /// <param name="playerStats">저장할 플레이어 스탯 데이터.</param>
        /// <returns>비동기 저장 작업을 나타내는 Task.</returns>
        // CHANGED: saveSlotId 대신 저장할 데이터 객체를 직접 받도록 시그니처 변경
        Task SaveAllGameData(GameProgressData gameProgress, PlayerStatsData playerStats);

        // 제네릭 메서드를 통해 특정 타입의 데이터를 로드/저장/삭제
        Task<T> LoadDataAsync<T>(int saveSlotId) where T : class;

        /// <summary>
        /// 단일 데이터 엔티티를 저장합니다.
        /// 이 메서드는 내부적으로 자체 트랜잭션을 생성하여 원자성을 보장합니다.
        /// </summary>
        Task SaveDataAsync<T>(T data) where T : class;

        Task DeleteDataAsync<T>(int saveSlotId) where T : class;
    }
}