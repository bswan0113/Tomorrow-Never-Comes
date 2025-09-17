// C:\Workspace\Tomorrow Never Comes\Core\Data\Interface\IDatabaseAccess.cs (REFACTORED)

using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;

namespace Core.Data.Interface
{


    /// <summary>
    /// 데이터베이스 접근을 위한 추상화된 인터페이스입니다.
    /// CRUD 작업 및 트랜잭션 관리를 포함합니다.
    /// </summary>
    public interface IDatabaseAccess : IDisposable
    {
        // --- 비트랜잭션 CRUD 작업 (기존 유지) ---

        Task<List<Dictionary<string, object>>> SelectWhereAsync(
            string tableName,
            string[] columns,
            string[] operations,
            object[] values,
            string logicalOperator = "AND");

        Task<int> DeleteContentsAsync(string tableName);

        Task<bool> TableExistsAsync(string tableName); // 추가: 테이블 존재 여부 확인 메서드
        Task CreateTableAsync(string tableName, Dictionary<string, string> columnsWithTypes, string primaryKey); // 추가: 테이블 생성 메서드

        // --- 트랜잭션 관리 ---

        /// <summary>
        /// 새로운 데이터베이스 트랜잭션을 비동기적으로 시작합니다.
        /// </summary>
        /// <param name="isolationLevel">트랜잭션 격리 수준.</param>
        /// <returns>시작된 트랜잭션을 나타내는 ITransaction 객체.</returns>
        Task<ITransaction> BeginTransactionAsync(IsolationLevel isolationLevel = IsolationLevel.ReadCommitted);


        // --- 트랜잭션 인자를 받는 CRUD 오버로드 ---

        /// <summary>
        /// 지정된 테이블에 데이터를 비동기적으로 삽입합니다. 트랜잭션 컨텍스트 내에서 실행됩니다.
        /// </summary>
        Task<int> InsertIntoAsync(string tableName, string[] columns, object[] values, ITransaction transaction);

        /// <summary>
        /// 지정된 테이블의 데이터를 비동기적으로 업데이트합니다. 트랜잭션 컨텍스트 내에서 실행됩니다.
        /// </summary>
        Task<int> UpdateSetAsync(
            string tableName,
            string[] updateCols,
            object[] updateValues,
            string whereCol,
            object whereValue,
            ITransaction transaction);

        /// <summary>
        /// 지정된 테이블의 특정 조건을 만족하는 데이터를 비동기적으로 삭제합니다. 트랜잭션 컨텍스트 내에서 실행됩니다.
        /// </summary>
        Task<int> DeleteWhereAsync(string tableName, string whereCol, object whereValue, ITransaction transaction);

        /// <summary>
        /// 범용 SQL 쿼리를 비동기적으로 실행하고 영향을 받은 행의 수를 반환합니다. 트랜잭션 컨텍스트 내에서 실행됩니다.
        /// </summary>
        Task<int> ExecuteNonQueryAsync(string query, Dictionary<string, object> parameters, ITransaction transaction);


        // --- 기존 비트랜잭션 CRUD 작업 (트랜잭션 오버로드 추가로 인한 메서드명 변경) ---

        /// <summary>
        /// 지정된 테이블에 데이터를 비동기적으로 삽입합니다. (트랜잭션 없이)
        /// </summary>
        Task<int> InsertIntoAsync(string tableName, string[] columns, object[] values);

        /// <summary>
        /// 지정된 테이블의 데이터를 비동기적으로 업데이트합니다. (트랜잭션 없이)
        /// </summary>
        Task<int> UpdateSetAsync(
            string tableName,
            string[] updateCols,
            object[] updateValues,
            string whereCol,
            object whereValue);

        /// <summary>
        /// 지정된 테이블의 특정 조건을 만족하는 데이터를 비동기적으로 삭제합니다. (트랜잭션 없이)
        /// </summary>
        Task<int> DeleteWhereAsync(string tableName, string whereCol, object whereValue);

        /// <summary>
        /// 범용 SQL 쿼리를 비동기적으로 실행하고 영향을 받은 행의 수를 반환합니다. (트랜잭션 없이)
        /// </summary>
        Task<int> ExecuteNonQueryAsync(string query, Dictionary<string, object> parameters = null);


        // --- 기타 작업 ---

        /// <summary>
        /// 마지막으로 삽입된 행의 ID를 비동기적으로 가져옵니다.
        /// </summary>
        Task<long> GetLastInsertRowIdAsync();

        // 기존 ExecuteInTransactionAsync는 사용 목적이 약간 다르지만, 여전히 유용할 수 있어 유지합니다.
        // 이들은 트랜잭션의 시작, 커밋, 롤백을 내부적으로 관리하는 헬퍼 메서드로 볼 수 있습니다.
        Task<T> ExecuteInTransactionAsync<T>(
            Func<IDbConnection, IDbTransaction, Task<T>> transactionAction,
            IsolationLevel isolationLevel = IsolationLevel.ReadCommitted);

        Task ExecuteInTransactionAsync(
            Func<IDbConnection, IDbTransaction, Task> transactionAction,
            IsolationLevel isolationLevel = IsolationLevel.ReadCommitted);
    }
}