// --- START OF FILE IDatabaseAccess.cs ---

using System.Collections.Generic;
using System.Data; // IsolationLevel enum을 위해 추가

namespace Core.Data.Interface
{
    /// <summary>
    /// 데이터베이스 접근을 위한 기본 기능을 정의하는 인터페이스입니다.
    /// 연결 관리, CRUD 작업, 트랜잭션 관리를 포함합니다.
    /// </summary>
    public interface IDatabaseAccess
    {
        // --- 연결 관리 ---
        void OpenConnection();
        void CloseConnection();

        // --- 트랜잭션 관리 ---
        void BeginTransaction(IsolationLevel isolationLevel = IsolationLevel.ReadCommitted);
        void CommitTransaction();
        void RollbackTransaction();
        bool IsInTransaction { get; } // 현재 트랜잭션 중인지 확인하는 속성

        // --- 데이터 조회 (Read) ---
        /// <summary>
        /// 지정된 테이블에서 특정 조건에 따라 데이터를 조회합니다.
        /// SELECT 대상 컬럼은 '*'로 가정하며, 'columns' 매개변수는 WHERE 절의 조건 컬럼을 정의합니다.
        /// </summary>
        /// <param name="tableName">조회할 테이블 이름</param>
        /// <param name="columns">WHERE 절에 사용될 컬럼 이름 배열</param>
        /// <param name="operations">WHERE 절에 사용될 연산자 배열 (예: "=", ">", "<")</param>
        /// <param name="values">WHERE 절의 조건 값 배열</param>
        /// <param name="logicalOperator">조건들을 연결할 논리 연산자 ("AND" 또는 "OR")</param>
        /// <returns>조회된 데이터를 담은 Dictionary 리스트</returns>
        List<Dictionary<string, object>> SelectWhere(string tableName, string[] columns, string[] operations, object[] values, string logicalOperator);
        /// <summary>
        /// 지정된 테이블에서 특정 조건에 따라 데이터를 조회합니다 (기본 AND 연산 사용).
        /// SELECT 대상 컬럼은 '*'로 가정하며, 'columns' 매개변수는 WHERE 절의 조건 컬럼을 정의합니다.
        /// </summary>
        List<Dictionary<string, object>> SelectWhere(string tableName, string[] columns, string[] operations, object[] values); // 기본 AND 연산 사용 오버로드

        // --- 데이터 변경 (Create, Update, Delete) ---
        void InsertInto(string tableName, string[] columns, object[] values);
        void UpdateSet(string tableName, string[] updateCols, object[] updateValues, string whereCol, object whereValue);
        void DeleteContents(string tableName);
        void DeleteWhere(string tableName, string whereCol, object whereValue);

        // --- 범용 쿼리 실행 ---
        int ExecuteNonQuery(string query);
        int ExecuteNonQuery(string query, Dictionary<string, object> parameters);

        // --- 기타 ---
        long GetLastInsertRowId();
    }
}
// --- END OF FILE IDatabaseAccess.cs ---