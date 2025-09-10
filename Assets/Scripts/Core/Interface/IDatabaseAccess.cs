namespace Core.Interface
{
    // Scripts/Core/Interface/IDatabaseAccess.cs (별도 파일로 존재한다고 가정)
    using                 System.Collections.Generic;
    using System;
    
    public interface IDatabaseAccess
    {
        void OpenConnection();
        void CloseConnection();
    
        // 기존 SelectWhere (logicalOperator 매개변수 포함)
        List<Dictionary<string, object>> SelectWhere(
            string tableName,
            string[] whereCols,
            string[] operators,
            object[] whereValues,
            string logicalOperator // <- 이 매개변수는 필수 매개변수로 인터페이스에 정의
        );
    
        // logicalOperator 매개변수 없는 오버로드 추가 (이것이 컴파일 에러를 해결할 것)
        List<Dictionary<string, object>> SelectWhere(
            string tableName,
            string[] whereCols,
            string[] operators,
            object[] whereValues
        );
    
        void InsertInto(string tableName, string[] columns, object[] values);
        void UpdateSet(string tableName, string[] updateCols, object[] updateValues, string whereCol, object whereValue);
        void DeleteWhere(string tableName, string whereCol, object whereValue);
        void DeleteContents(string tableName);
    
        int ExecuteNonQuery(string query);
        int ExecuteNonQuery(string query, Dictionary<string, object> parameters);
    }
}