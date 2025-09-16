// --- START OF FILE IDataService.txt ---

using System.Collections.Generic;
using System.Threading.Tasks; // SaveAllGameData가 비동기 작업을 포함할 수 있으므로 추가

namespace Core.Data.Interface
{

    public interface IDataService
    {
        bool HasSaveData { get; }

        // P24: IDataService에 SaveAllGameData 메서드 추가 (파사드 역할 강화)
        Task SaveAllGameData(int saveSlotId = 1); // 기본 세이브 슬롯 ID를 매개변수로 받음

        // 범용 CRUD 메서드 (필요에 따라 유지하거나 제거)
        List<Dictionary<string, object>> LoadData(string tableName, string keyColumn, object keyValue);
        void UpdateData(string tableName, string[] columnNames, object[] values, string keyColumn, object keyValue);
        void InsertData(string tableName, string[] columns, object[] values);
        void DeleteData(string tableName, string whereCol, object whereValue);

        // DataManager가 테이블 생성 시 사용할 일반 쿼리 실행
        // ExecuteNonQuery는 IDatabaseAccess가 직접 제공하므로 IDataService에는 필요 없을 수 있습니다.
        // DataManager가 테이블 초기화 로직을 가지고 있으므로, DataManager.Initialize() 내에서
        // 주입받은 IDatabaseAccess를 통해 ExecuteNonQuery를 호출하면 됩니다.
        // -> IDataService에서는 제거하는 것이 더 적절합니다.
    }
}