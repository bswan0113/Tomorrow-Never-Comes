// C:\Workspace\Tomorrow Never Comes\Core\Data\Impl\DatabaseAccess.cs (REFACTORED)

using Mono.Data.Sqlite;
using System.Collections.Generic;
using System;
using System.Data;
using Core.Data.Interface;
using System.Threading;
using System.Threading.Tasks;
using Core.Logging;
using Cysharp.Threading.Tasks;
using VContainer.Unity;

namespace Core.Data.Impl
{
    public class DatabaseAccess : IDatabaseAccess, IAsyncStartable // IDatabaseAccess가 IDisposable을 상속합니다.
    {
        private readonly string m_ConnectionString;
        private readonly SchemaManager _schemaManager;

        private const int MAX_RETRY_ATTEMPTS = 3;
        private const int RETRY_DELAY_MS = 100; // 밀리초

        public DatabaseAccess(string dbPath, SchemaManager schemaManager)
        {
            if (string.IsNullOrWhiteSpace(dbPath))
            {
                throw new ArgumentException("Database path cannot be null or empty.", nameof(dbPath));
            }
            _schemaManager = schemaManager ?? throw new ArgumentNullException(nameof(schemaManager));
            m_ConnectionString = "URI=file:" + dbPath;
            CoreLogger.Log($"[DatabaseAccess] Initialized with path: {dbPath}");

        }
        public async UniTask StartAsync(CancellationToken cancellation)
        {
            CoreLogger.Log("[DatabaseAccess] Starting asynchronous initialization...");
            // 여기서 await를 사용하여 테이블 초기화가 완전히 끝날 때까지 기다립니다.
            await _schemaManager.InitializeTablesAsync(this);
            CoreLogger.Log("[DatabaseAccess] Asynchronous initialization complete. Database is ready.");
        }
        // --- Connection / Command Helpers (Internal) ---
        // 이 헬퍼는 연결을 생성, 열고, 작업을 수행한 후 닫는 역할을 합니다.
        // Task.Run 내에서 동기적으로 호출될 것을 전제로 합니다.
        private T ExecuteDbOperation<T>(Func<SqliteConnection, T> operation, string operationName)
        {
            for (int i = 0; i < MAX_RETRY_ATTEMPTS; i++)
            {
                if (i > 0)
                {
                    CoreLogger.LogWarning($"[DatabaseAccess] Retrying {operationName} (Attempt {i + 1}/{MAX_RETRY_ATTEMPTS})...");
                    Thread.Sleep(RETRY_DELAY_MS);
                }

                try
                {
                    using (var connection = new SqliteConnection(m_ConnectionString))
                    {
                        connection.Open(); // 동기적으로 연결을 열고, ADO.NET 풀링 메커니즘을 사용합니다.
                        CoreLogger.Log($"[DatabaseAccess] Connection opened for {operationName}. State: {connection.State}");
                        return operation(connection);
                    } // connection.Dispose()가 호출되어 연결이 풀에 반환됩니다.
                }
                catch (SqliteException ex)
                {
                    // SQLite의 일반적인 일시적 에러 (예: DB 잠금, busy) 발생 시 재시도합니다.
                    if (ex.Message.Contains("locked") || ex.Message.Contains("busy") || ex.ErrorCode == SQLiteErrorCode.Busy /* SQLITE_BUSY */ || ex.ErrorCode == SQLiteErrorCode.Locked /* SQLITE_LOCKED */)
                    {
                        CoreLogger.LogWarning($"[DatabaseAccess] Transient error during {operationName}: {ex.Message}. Will retry.");
                        continue;
                    }
                    CoreLogger.LogError($"[DatabaseAccess] Non-retryable Sqlite error during {operationName}: {ex.Message}");
                    throw;
                }
                catch (Exception ex)
                {
                    CoreLogger.LogError($"[DatabaseAccess] Unexpected error during {operationName}: {ex.Message}");
                    throw;
                }
            }
            throw new InvalidOperationException($"[DatabaseAccess] Failed to {operationName} after {MAX_RETRY_ATTEMPTS} attempts.");
        }

        private SqliteCommand CreateCommand(SqliteConnection connection, string query, Dictionary<string, object> parameters = null, SqliteTransaction transaction = null)
        {
            var command = connection.CreateCommand();
            command.CommandText = query;
            if (transaction != null)
            {
                command.Transaction = transaction;
            }

            if (parameters != null)
            {
                foreach (var param in parameters)
                {
                    SqliteParameter sqliteParam = command.CreateParameter();
                    sqliteParam.ParameterName = param.Key;
                    sqliteParam.Value = param.Value ?? DBNull.Value; // Null 값 처리
                    command.Parameters.Add(sqliteParam);
                }
            }
            return command;
        }

        // --- 유효성 검사 ---
        private void ValidateTableName(string tableName)
        {
            if (string.IsNullOrWhiteSpace(tableName))
            {
                throw new ArgumentException("[DatabaseAccess] Table name cannot be null or empty.", nameof(tableName));
            }
            if (!_schemaManager.IsTableNameValid(tableName))
            {
                CoreLogger.LogError($"[DatabaseAccess] Attempted to access an unallowed or invalid table: '{tableName}'. Check SchemaManager configuration.");
                throw new ArgumentException($"[DatabaseAccess] Access to table '{tableName}' is not allowed or it does not exist in schema.");
            }
        }

        private void ValidateColumnNames(string tableName, params string[] columnNames)
        {
            if (columnNames == null || columnNames.Length == 0) return;

            foreach (string colName in columnNames)
            {
                if (string.IsNullOrWhiteSpace(colName))
                {
                    throw new ArgumentException($"[DatabaseAccess] Column name cannot be null or empty for table '{tableName}'.", nameof(colName));
                }
                if (!_schemaManager.IsColumnNameValid(tableName, colName))
                {
                    CoreLogger.LogError($"[DatabaseAccess] Invalid or potentially malicious column name detected: '{colName}' for table '{tableName}'. Check SchemaManager configuration.");
                    throw new ArgumentException($"[DatabaseAccess] Invalid column name '{colName}' for table '{tableName}'.");
                }
            }
        }

        // --- IDatabaseAccess Implementation ---

        public async Task<List<Dictionary<string, object>>> SelectWhereAsync(string tableName, string[] columns, string[] operations, object[] values, string logicalOperator = "AND")
        {
            ValidateTableName(tableName);
            ValidateColumnNames(tableName, columns);

            if (columns == null || operations == null || values == null) throw new ArgumentNullException("Columns, operations, or values array cannot be null.");
            if (columns.Length != operations.Length || columns.Length != values.Length)
            {
                throw new ArgumentException("Length of columns, operations, and values arrays must be equal.", nameof(columns));
            }
            if (string.IsNullOrWhiteSpace(logicalOperator)) throw new ArgumentException("Logical operator cannot be null or empty.", nameof(logicalOperator));

            string upperLogicalOperator = logicalOperator.Trim().ToUpper();
            if (!(upperLogicalOperator == "AND" || upperLogicalOperator == "OR"))
            {
                throw new ArgumentException($"Invalid logical operator: '{logicalOperator}'. Only 'AND' or 'OR' are allowed.", nameof(logicalOperator));
            }

            return await Task.Run(() => ExecuteDbOperation(connection =>
            {
                string query = $"SELECT * FROM {tableName} WHERE ";
                var parameters = new Dictionary<string, object>();

                for (int i = 0; i < columns.Length; i++)
                {
                    string paramName = $"@param{i}";
                    query += $"{columns[i]} {operations[i]} {paramName}";
                    if (i < columns.Length - 1)
                    {
                        query += $" {upperLogicalOperator} ";
                    }
                    parameters[paramName] = values[i];
                }

                CoreLogger.Log($"[DatabaseAccess] Executing SelectWhere: {query}");
                var result = new List<Dictionary<string, object>>();
                using (var command = CreateCommand(connection, query, parameters))
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var row = new Dictionary<string, object>();
                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            row[reader.GetName(i)] = reader.GetValue(i);
                        }
                        result.Add(row);
                    }
                }
                return result;
            }, $"executing SelectWhere on {tableName}"));
        }

        public async Task<int> InsertIntoAsync(string tableName, string[] columns, object[] values)
        {
            ValidateTableName(tableName);
            ValidateColumnNames(tableName, columns);

            if (columns == null || values == null) throw new ArgumentNullException("Columns or values array cannot be null.");
            if (columns.Length != values.Length)
            {
                throw new ArgumentException("Length of columns and values arrays must be equal.", nameof(columns));
            }

            return await Task.Run(() => ExecuteDbOperation(connection =>
            {
                string[] parameterNames = new string[columns.Length];
                for (int i = 0; i < columns.Length; i++)
                {
                    parameterNames[i] = $"@{columns[i]}";
                }

                string query = $"INSERT INTO {tableName} ({string.Join(", ", columns)}) VALUES ({string.Join(", ", parameterNames)})";
                var parameters = new Dictionary<string, object>();
                for (int i = 0; i < columns.Length; i++)
                {
                    parameters[parameterNames[i]] = values[i];
                }

                CoreLogger.Log($"[DatabaseAccess] Executing InsertInto on {tableName}");
                using (var command = CreateCommand(connection, query, parameters))
                {
                    return command.ExecuteNonQuery();
                }
            }, $"executing InsertInto on {tableName}"));
        }

        public async Task<int> UpdateSetAsync(string tableName, string[] updateCols, object[] updateValues, string whereCol, object whereValue)
        {
            ValidateTableName(tableName);
            ValidateColumnNames(tableName, updateCols);
            ValidateColumnNames(tableName, whereCol);

            if (updateCols == null || updateValues == null) throw new ArgumentNullException("Update columns or values array cannot be null.");
            if (updateCols.Length != updateValues.Length)
            {
                throw new ArgumentException("Length of updateCols and updateValues arrays must be equal.", nameof(updateCols));
            }
            if (string.IsNullOrWhiteSpace(whereCol)) throw new ArgumentException("Where column cannot be null or empty.", nameof(whereCol));

            return await Task.Run(() => ExecuteDbOperation(connection =>
            {
                string query = $"UPDATE {tableName} SET ";
                var parameters = new Dictionary<string, object>();

                for (int i = 0; i < updateCols.Length; i++)
                {
                    string paramName = $"@update{i}";
                    query += $"{updateCols[i]} = {paramName}";
                    if (i < updateCols.Length - 1)
                    {
                        query += ", ";
                    }
                    parameters[paramName] = updateValues[i];
                }

                query += $" WHERE {whereCol} = @whereValue";
                parameters["@whereValue"] = whereValue;

                CoreLogger.Log($"[DatabaseAccess] Executing UpdateSet on {tableName}");
                using (var command = CreateCommand(connection, query, parameters))
                {
                    return command.ExecuteNonQuery();
                }
            }, $"executing UpdateSet on {tableName}"));
        }

        public async Task<int> DeleteContentsAsync(string tableName)
        {
            ValidateTableName(tableName);

            return await Task.Run(() => ExecuteDbOperation(connection =>
            {
                string query = $"DELETE FROM {tableName}";
                CoreLogger.Log($"[DatabaseAccess] Executing DeleteContents on {tableName}");
                using (var command = CreateCommand(connection, query))
                {
                    return command.ExecuteNonQuery();
                }
            }, $"executing DeleteContents on {tableName}"));
        }

        public async Task<int> DeleteWhereAsync(string tableName, string whereCol, object whereValue)
        {
            ValidateTableName(tableName);
            ValidateColumnNames(tableName, whereCol);

            if (string.IsNullOrWhiteSpace(whereCol)) throw new ArgumentException("Where column cannot be null or empty.", nameof(whereCol));

            return await Task.Run(() => ExecuteDbOperation(connection =>
            {
                string query = $"DELETE FROM {tableName} WHERE {whereCol} = @whereValue";
                var parameters = new Dictionary<string, object>
                {
                    { "@whereValue", whereValue }
                };
                CoreLogger.Log($"[DatabaseAccess] Executing DeleteWhere on {tableName}");
                using (var command = CreateCommand(connection, query, parameters))
                {
                    return command.ExecuteNonQuery();
                }
            }, $"executing DeleteWhere on {tableName}"));
        }

        public async Task<int> ExecuteNonQueryAsync(string query, Dictionary<string, object> parameters = null)
        {
            if (string.IsNullOrWhiteSpace(query)) throw new ArgumentException("Query cannot be null or empty.", nameof(query));

            return await Task.Run(() => ExecuteDbOperation(connection =>
            {
                using (var command = CreateCommand(connection, query, parameters))
                {
                    int rowsAffected = command.ExecuteNonQuery();
                    CoreLogger.Log($"[DatabaseAccess] Executed NonQuery: '{query}'. Rows affected: {rowsAffected}");
                    return rowsAffected;
                }
            }, $"executing non-query '{query}'"));
        }

        public async Task<long> GetLastInsertRowIdAsync()
        {
            return await Task.Run(() => ExecuteDbOperation(connection =>
            {
                long lastId = 0;
                using (var command = CreateCommand(connection, "SELECT last_insert_rowid()"))
                {
                    object result = command.ExecuteScalar();
                    if (result != null && result != DBNull.Value)
                    {
                        lastId = (long)result;
                    }
                }
                return lastId;
            }, "getting last insert row ID"));
        }

        // --- Transaction Management ---
        // 트랜잭션은 하나의 전용 연결에서 여러 작업을 수행해야 하므로,
        // 별도의 연결 관리 방식을 사용합니다.
        public async Task<T> ExecuteInTransactionAsync<T>(Func<IDbConnection, IDbTransaction, Task<T>> transactionAction, IsolationLevel isolationLevel = IsolationLevel.ReadCommitted)
        {
            return await Task.Run(async () =>
            {
                using (var connection = new SqliteConnection(m_ConnectionString))
                {
                    connection.Open();
                    CoreLogger.Log($"[DatabaseAccess] Connection opened for transaction. State: {connection.State}");
                    using (var transaction = connection.BeginTransaction(isolationLevel))
                    {
                        try
                        {
                            CoreLogger.Log($"[DatabaseAccess] Transaction started with IsolationLevel: {isolationLevel}");
                            // 트랜잭션 작업을 비동기로 실행
                            T result = await transactionAction(connection, transaction);
                            transaction.Commit();
                            CoreLogger.Log("[DatabaseAccess] Transaction committed.");
                            return result;
                        }
                        catch (Exception ex)
                        {
                            CoreLogger.LogError($"[DatabaseAccess] Error during transaction, rolling back: {ex.Message}");
                            try
                            {
                                transaction.Rollback();
                                CoreLogger.Log("[DatabaseAccess] Transaction rolled back.");
                            }
                            catch (Exception rollbackEx)
                            {
                                CoreLogger.LogError($"[DatabaseAccess] Error during rollback: {rollbackEx.Message}");
                            }
                            throw; // 원래 예외 다시 던지기
                        }
                    } // transaction.Dispose() 호출
                } // connection.Dispose() 호출 (연결을 풀에 반환)
            });
        }

        public async Task ExecuteInTransactionAsync(Func<IDbConnection, IDbTransaction, Task> transactionAction, IsolationLevel isolationLevel = IsolationLevel.ReadCommitted)
        {
            await ExecuteInTransactionAsync(async (conn, trans) => {
                await transactionAction(conn, trans);
                return true; // 더미 반환 값
            }, isolationLevel);
        }

        // IDisposable 구현
        public void Dispose()
        {
            // Mono.Data.Sqlite의 경우, 개별 connection.Dispose() 호출이 연결을 풀에 반환하므로,
            // DatabaseAccess 클래스 자체에서 명시적인 풀 정리 로직은 일반적으로 필요하지 않습니다.
            // 모든 연결은 각 작업 후 즉시 반환됩니다.
            // 만약 풀 전체를 강제로 비워야 하는 특정 상황이 있다면 SqliteConnection.ClearAllPools()를 사용할 수 있으나,
            // 이는 애플리케이션 내의 모든 SQLite 연결 풀에 영향을 미치므로 신중하게 사용해야 합니다.

            CoreLogger.Log("[DatabaseAccess] Dispose called. All connections should have been returned to the pool.");
        }
    }
}