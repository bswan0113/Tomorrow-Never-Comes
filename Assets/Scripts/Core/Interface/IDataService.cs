using System.Collections.Generic;

namespace Core.Interface
{
    // Scripts/Core/Interface/IDataService.cs
    using System.Collections.Generic;
    using System;

    namespace Core.Interface
    {
        public interface IDataService
        {
            bool HasSaveData { get; }
            List<Dictionary<string, object>> LoadData(string tableName, string keyColumn, object keyValue);
            void UpdateData(string tableName, string[] columnNames, object[] values, string keyColumn, object keyValue);
            void CreateNewGameData(); // 이 메서드는 DataManager의 역할이 아님 (DataManager 역할 혼합 P0) -> 추후 개선 대상
            // 일단 지금은 DataManager에 유지하되, 인터페이스에는 넣지 않거나,
            // DataManager가 내부적으로 사용하는 Private 메서드로 전환하는 것을 고려

            // PlayerDataManager가 새 데이터를 '삽입'할 때 사용하는 메서드 (이전에 'SaveData'라고 불렸던 것)
            void InsertData(string tableName, string[] columns, object[] values); // 이름을 InsertData로 명확히 변경

            void DeleteData(string tableName, string whereCol, object whereValue);
            // void SavePlayerData(); // <- 제거

            // DataManager가 테이블 생성 시 사용할 일반 쿼리 실행
            // ExecuteNonQuery는 IDatabaseAccess가 직접 제공하므로 IDataService에는 필요 없을 수 있습니다.
            // DataManager가 테이블 초기화 로직을 가지고 있으므로, DataManager.Initialize() 내에서
            // 주입받은 IDatabaseAccess를 통해 ExecuteNonQuery를 호출하면 됩니다.
            // -> IDataService에서는 제거하는 것이 더 적절합니다.
        }
    }
}