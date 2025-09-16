// C:\Workspace\Tomorrow Never Comes\Core\Data\Interface\IDatabaseAccess.cs (NEW FILE)

using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;

namespace Core.Data.Interface
{
    public interface IDatabaseAccess : IDisposable // For potential pool clearing or finalization
    {
        // CRUD operations - all async, returning rows affected or data
        Task<List<Dictionary<string, object>>> SelectWhereAsync(
            string tableName,
            string[] columns,
            string[] operations,
            object[] values,
            string logicalOperator = "AND");

        Task<int> InsertIntoAsync(string tableName, string[] columns, object[] values);

        Task<int> UpdateSetAsync(
            string tableName,
            string[] updateCols,
            object[] updateValues,
            string whereCol,
            object whereValue);

        Task<int> DeleteContentsAsync(string tableName);

        Task<int> DeleteWhereAsync(string tableName, string whereCol, object whereValue);

        // Generic query execution - all async
        Task<int> ExecuteNonQueryAsync(string query, Dictionary<string, object> parameters = null);
        Task<long> GetLastInsertRowIdAsync();

        // Transaction management (allows multiple operations on a single connection/transaction)
        Task<T> ExecuteInTransactionAsync<T>(
            Func<IDbConnection, IDbTransaction, Task<T>> transactionAction,
            IsolationLevel isolationLevel = IsolationLevel.ReadCommitted);

        Task ExecuteInTransactionAsync(
            Func<IDbConnection, IDbTransaction, Task> transactionAction,
            IsolationLevel isolationLevel = IsolationLevel.ReadCommitted);
    }
}