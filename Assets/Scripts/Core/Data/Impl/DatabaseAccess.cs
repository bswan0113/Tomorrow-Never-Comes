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
using System.Linq; // For .ToArray() in CreateTableAsync

namespace Core.Data.Impl
{
    /// <summary>
    /// IDatabaseAccess 인터페이스를 구현하는 SQLite 데이터베이스 접근 클래스입니다.
    /// 비동기 CRUD 작업과 트랜잭션 관리를 지원합니다.
    /// </summary>
    public class DatabaseAccess : IDatabaseAccess
    {
        private readonly string m_ConnectionString;
        private readonly SchemaManager _schemaManager;

        private const int MAX_RETRY_ATTEMPTS = 3;
        private const int RETRY_DELAY_MS = 100; // 밀리초

        // --- DatabaseTransaction 내부 클래스 ---
        /// <summary>
        /// IDatabaseAccess에서 시작된 데이터베이스 트랜잭션을 캡슐화하는 클래스입니다.
        /// </summary>
        private class DatabaseTransaction : ITransaction
        {
            public IDbConnection Connection { get; private set; }
            public IDbTransaction DbTransaction { get; private set; }
            private bool _isCommittedOrRolledBack = false;

            public DatabaseTransaction(IDbConnection connection, IDbTransaction dbTransaction)
            {
                Connection = connection ?? throw new ArgumentNullException(nameof(connection));
                DbTransaction = dbTransaction ?? throw new ArgumentNullException(nameof(dbTransaction));
            }

            public async Task CommitAsync()
            {
                if (_isCommittedOrRolledBack) return;
                await Task.Run(() => DbTransaction.Commit());
                _isCommittedOrRolledBack = true;
                CoreLogger.Log("[DatabaseTransaction] Committed.");
            }

            public async Task RollbackAsync()
            {
                if (_isCommittedOrRolledBack) return;
                await Task.Run(() => DbTransaction.Rollback());
                _isCommittedOrRolledBack = true;
                CoreLogger.Log("[DatabaseTransaction] Rolled back.");
            }

            public void Dispose()
            {
                if (!_isCommittedOrRolledBack)
                {
                    CoreLogger.LogWarning("[DatabaseTransaction] Disposing without explicit Commit/Rollback. Performing implicit Rollback.");
                    try
                    {
                        DbTransaction.Rollback();
                    }
                    catch (Exception ex)
                    {
                        CoreLogger.LogError($"[DatabaseTransaction] Error during implicit rollback on dispose: {ex.Message}");
                    }
                }
                DbTransaction?.Dispose();
                Connection?.Dispose(); // Connection도 여기서 닫아서 풀에 반환
                CoreLogger.Log("[DatabaseTransaction] Disposed.");
            }
        }
        // --- End of DatabaseTransaction ---


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

        // public async UniTask StartAsync(CancellationToken cancellation)
        // {
        //     CoreLogger.Log("[DatabaseAccess] Starting asynchronous initialization...");
        //     // SchemaManager의 테이블 초기화가 DatabaseAccess의 메서드를 사용하도록 변경
        //     await _schemaManager.InitializeTablesAsync(this);
        //     CoreLogger.Log("[DatabaseAccess] Asynchronous initialization complete. Database is ready.");
        // }

        // --- Connection / Command Helpers (Internal) ---

        /// <summary>
        /// 데이터베이스 작업을 백그라운드 스레드에서 실행하고 연결을 관리합니다. (비트랜잭션용)
        /// </summary>
        private T ExecuteDbOperation<T>(Func<IDbConnection, T> operation, string operationName)
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
                        connection.Open();
                        CoreLogger.Log($"[DatabaseAccess] Connection opened for {operationName}. State: {connection.State}");
                        return operation(connection);
                    }
                }
                catch (SqliteException ex)
                {
                    if (ex.Message.Contains("locked") || ex.Message.Contains("busy") || ex.ErrorCode == SQLiteErrorCode.Busy || ex.ErrorCode == SQLiteErrorCode.Locked)
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

        /// <summary>
        /// SQL 명령어를 생성합니다. 트랜잭션 컨텍스트를 지원합니다.
        /// </summary>
        private SqliteCommand CreateCommand(IDbConnection connection, string query, Dictionary<string, object> parameters = null, IDbTransaction transaction = null)
        {
            var command = (SqliteCommand)connection.CreateCommand();
            command.CommandText = query;
            if (transaction != null)
            {
                command.Transaction = (SqliteTransaction)transaction;
            }

            if (parameters != null)
            {
                foreach (var param in parameters)
                {
                    SqliteParameter sqliteParam = command.CreateParameter();
                    sqliteParam.ParameterName = param.Key;
                    sqliteParam.Value = param.Value ?? DBNull.Value;
                    command.Parameters.Add(sqliteParam);
                }
            }
            return command;
        }

        // --- 유효성 검사 ---
        private void ValidateTableName(string tableName, bool allowInternal = false)
        {
            if (string.IsNullOrWhiteSpace(tableName))
            {
                throw new ArgumentException("[DatabaseAccess] Table name cannot be null or empty.", nameof(tableName));
            }
            if (!allowInternal && !_schemaManager.IsTableNameValid(tableName))
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
                string query = $"SELECT {(columns.Any() ? string.Join(", ", columns) : "*")} FROM {tableName} WHERE ";
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
                using (var command = CreateCommand(connection, query, parameters)) // Pass null for transaction
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

        public async Task<int> DeleteContentsAsync(string tableName)
        {
            ValidateTableName(tableName);

            return await Task.Run(() => ExecuteDbOperation(connection =>
            {
                string query = $"DELETE FROM {tableName}";
                CoreLogger.Log($"[DatabaseAccess] Executing DeleteContents on {tableName}");
                using (var command = CreateCommand(connection, query)) // Pass null for transaction
                {
                    return command.ExecuteNonQuery();
                }
            }, $"executing DeleteContents on {tableName}"));
        }

        public async Task<bool> TableExistsAsync(string tableName)
        {
            // For checking internal SQLite tables, bypass schema manager validation on table name itself
            ValidateTableName(tableName, allowInternal: true);

            return await Task.Run(() => ExecuteDbOperation(connection =>
            {
                string query = $"SELECT name FROM sqlite_master WHERE type='table' AND name='{tableName}'";
                using (var command = CreateCommand(connection, query))
                {
                    object result = command.ExecuteScalar();
                    return result != null && result != DBNull.Value;
                }
            }, $"checking if table {tableName} exists"));
        }

        public async Task CreateTableAsync(string tableName, Dictionary<string, string> columnsWithTypes, string primaryKey)
        {
            ValidateTableName(tableName); // Use schema validation for application tables
            ValidateColumnNames(tableName, columnsWithTypes.Keys.ToArray());
            ValidateColumnNames(tableName, primaryKey);

            if (columnsWithTypes == null || columnsWithTypes.Count == 0)
            {
                throw new ArgumentException("Columns with types cannot be null or empty.", nameof(columnsWithTypes));
            }
            if (string.IsNullOrWhiteSpace(primaryKey))
            {
                throw new ArgumentException("Primary key cannot be null or empty.", nameof(primaryKey));
            }
            if (!columnsWithTypes.ContainsKey(primaryKey))
            {
                throw new ArgumentException($"Primary key column '{primaryKey}' must be present in columnsWithTypes.", nameof(primaryKey));
            }

            await Task.Run(() => ExecuteDbOperation(connection =>
            {
                string columnDefinitions = string.Join(", ", columnsWithTypes.Select(kvp => $"{kvp.Key} {kvp.Value}"));
                string query = $"CREATE TABLE IF NOT EXISTS {tableName} ({columnDefinitions}, PRIMARY KEY({primaryKey}))";

                CoreLogger.Log($"[DatabaseAccess] Executing CreateTable: {query}");
                using (var command = CreateCommand(connection, query)) // Pass null for transaction
                {
                    return command.ExecuteNonQuery();
                }
            }, $"creating table {tableName}"));
        }

        // --- 트랜잭션 관리 ---

        public async Task<ITransaction> BeginTransactionAsync(IsolationLevel isolationLevel = IsolationLevel.ReadCommitted)
        {
            return await Task.Run(() =>
            {
                var connection = new SqliteConnection(m_ConnectionString);
                connection.Open();
                CoreLogger.Log($"[DatabaseAccess] Connection opened for new transaction. State: {connection.State}");
                var dbTransaction = connection.BeginTransaction(isolationLevel);
                CoreLogger.Log($"[DatabaseAccess] Transaction started with IsolationLevel: {isolationLevel}");
                return new DatabaseTransaction(connection, dbTransaction);
            });
        }


        // --- 트랜잭션 인자를 받는 CRUD 오버로드 ---

        public async Task<int> InsertIntoAsync(string tableName, string[] columns, object[] values, ITransaction transaction)
        {
            ValidateTableName(tableName);
            ValidateColumnNames(tableName, columns);

            if (transaction == null) throw new ArgumentNullException(nameof(transaction), "Transaction is required for this operation.");
            if (columns == null || values == null) throw new ArgumentNullException("Columns or values array cannot be null.");
            if (columns.Length != values.Length) throw new ArgumentException("Length of columns and values arrays must be equal.", nameof(columns));

            return await Task.Run(() =>
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

                CoreLogger.Log($"[DatabaseAccess] Executing InsertInto in transaction on {tableName}");
                using (var command = CreateCommand(transaction.Connection, query, parameters, transaction.DbTransaction))
                {
                    return command.ExecuteNonQuery();
                }
            });
        }

        public async Task<int> UpdateSetAsync(string tableName, string[] updateCols, object[] updateValues, string whereCol, object whereValue, ITransaction transaction)
        {
            ValidateTableName(tableName);
            ValidateColumnNames(tableName, updateCols);
            ValidateColumnNames(tableName, whereCol);

            if (transaction == null) throw new ArgumentNullException(nameof(transaction), "Transaction is required for this operation.");
            if (updateCols == null || updateValues == null) throw new ArgumentNullException("Update columns or values array cannot be null.");
            if (updateCols.Length != updateValues.Length) throw new ArgumentException("Length of updateCols and updateValues arrays must be equal.", nameof(updateCols));
            if (string.IsNullOrWhiteSpace(whereCol)) throw new ArgumentException("Where column cannot be null or empty.", nameof(whereCol));

            return await Task.Run(() =>
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

                CoreLogger.Log($"[DatabaseAccess] Executing UpdateSet in transaction on {tableName}");
                using (var command = CreateCommand(transaction.Connection, query, parameters, transaction.DbTransaction))
                {
                    return command.ExecuteNonQuery();
                }
            });
        }

        public async Task<int> DeleteWhereAsync(string tableName, string whereCol, object whereValue, ITransaction transaction)
        {
            ValidateTableName(tableName);
            ValidateColumnNames(tableName, whereCol);

            if (transaction == null) throw new ArgumentNullException(nameof(transaction), "Transaction is required for this operation.");
            if (string.IsNullOrWhiteSpace(whereCol)) throw new ArgumentException("Where column cannot be null or empty.", nameof(whereCol));

            return await Task.Run(() =>
            {
                string query = $"DELETE FROM {tableName} WHERE {whereCol} = @whereValue";
                var parameters = new Dictionary<string, object>
                {
                    { "@whereValue", whereValue }
                };
                CoreLogger.Log($"[DatabaseAccess] Executing DeleteWhere in transaction on {tableName}");
                using (var command = CreateCommand(transaction.Connection, query, parameters, transaction.DbTransaction))
                {
                    return command.ExecuteNonQuery();
                }
            });
        }

        public async Task<int> ExecuteNonQueryAsync(string query, Dictionary<string, object> parameters, ITransaction transaction)
        {
            if (string.IsNullOrWhiteSpace(query)) throw new ArgumentException("Query cannot be null or empty.", nameof(query));
            if (transaction == null) throw new ArgumentNullException(nameof(transaction), "Transaction is required for this operation.");

            return await Task.Run(() =>
            {
                CoreLogger.Log($"[DatabaseAccess] Executing NonQuery in transaction: '{query}'");
                using (var command = CreateCommand(transaction.Connection, query, parameters, transaction.DbTransaction))
                {
                    return command.ExecuteNonQuery();
                }
            });
        }


        // --- 기존 비트랜잭션 CRUD 작업 (새로운 트랜잭션 오버로드 추가로 인한 오버로드 유지) ---

        public async Task<int> InsertIntoAsync(string tableName, string[] columns, object[] values)
        {
            ValidateTableName(tableName);
            ValidateColumnNames(tableName, columns);

            if (columns == null || values == null) throw new ArgumentNullException("Columns or values array cannot be null.");
            if (columns.Length != values.Length) throw new ArgumentException("Length of columns and values arrays must be equal.", nameof(columns));

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
                using (var command = CreateCommand(connection, query, parameters)) // Pass null for transaction
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
            if (updateCols.Length != updateValues.Length) throw new ArgumentException("Length of updateCols and updateValues arrays must be equal.", nameof(updateCols));
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
                using (var command = CreateCommand(connection, query, parameters)) // Pass null for transaction
                {
                    return command.ExecuteNonQuery();
                }
            }, $"executing UpdateSet on {tableName}"));
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
                using (var command = CreateCommand(connection, query, parameters)) // Pass null for transaction
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
                using (var command = CreateCommand(connection, query, parameters)) // Pass null for transaction
                {
                    int rowsAffected = command.ExecuteNonQuery();
                    CoreLogger.Log($"[DatabaseAccess] Executed NonQuery: '{query}'. Rows affected: {rowsAffected}");
                    return rowsAffected;
                }
            }, $"executing non-query '{query}'"));
        }

        // --- 기타 작업 ---

        public async Task<long> GetLastInsertRowIdAsync()
        {
            return await Task.Run(() => ExecuteDbOperation(connection =>
            {
                long lastId = 0;
                using (var command = CreateCommand(connection, "SELECT last_insert_rowid()")) // Pass null for transaction
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

        // 기존 ExecuteInTransactionAsync는 유지합니다.
        // 이들은 트랜잭션의 시작, 커밋, 롤백을 내부적으로 관리하는 헬퍼 메서드로 볼 수 있습니다.
        public async Task<T> ExecuteInTransactionAsync<T>(Func<IDbConnection, IDbTransaction, Task<T>> transactionAction, IsolationLevel isolationLevel = IsolationLevel.ReadCommitted)
        {
            return await Task.Run(async () =>
            {
                using (var connection = new SqliteConnection(m_ConnectionString))
                {
                    connection.Open();
                    CoreLogger.Log($"[DatabaseAccess] Connection opened for ExecuteInTransaction. State: {connection.State}");
                    using (var transaction = connection.BeginTransaction(isolationLevel))
                    {
                        try
                        {
                            CoreLogger.Log($"[DatabaseAccess] Transaction (helper) started with IsolationLevel: {isolationLevel}");
                            T result = await transactionAction(connection, transaction);
                            transaction.Commit();
                            CoreLogger.Log("[DatabaseAccess] Transaction (helper) committed.");
                            return result;
                        }
                        catch (Exception ex)
                        {
                            CoreLogger.LogError($"[DatabaseAccess] Error during transaction (helper), rolling back: {ex.Message}");
                            try
                            {
                                transaction.Rollback();
                                CoreLogger.Log("[DatabaseAccess] Transaction (helper) rolled back.");
                            }
                            catch (Exception rollbackEx)
                            {
                                CoreLogger.LogError($"[DatabaseAccess] Error during rollback (helper): {rollbackEx.Message}");
                            }
                            throw;
                        }
                    }
                }
            });
        }

        public async Task ExecuteInTransactionAsync(Func<IDbConnection, IDbTransaction, Task> transactionAction, IsolationLevel isolationLevel = IsolationLevel.ReadCommitted)
        {
            await ExecuteInTransactionAsync(async (conn, trans) => {
                await transactionAction(conn, trans);
                return true;
            }, isolationLevel);
        }

        // IDisposable 구현
        public void Dispose()
        {
            CoreLogger.Log("[DatabaseAccess] Dispose called. All connections opened via 'using' statements should have been returned to the pool.");
            // Mono.Data.Sqlite의 연결 풀링 메커니즘에 따라, 각 SqliteConnection 인스턴스가 Dispose될 때
            // 자동으로 연결이 풀에 반환되므로, DatabaseAccess 자체에서 특별히 할당 해제할 리소스는 없습니다.
            // BeginTransactionAsync에서 생성된 connection과 transaction은 DatabaseTransaction.Dispose()에서 처리됩니다.
        }
    }
}