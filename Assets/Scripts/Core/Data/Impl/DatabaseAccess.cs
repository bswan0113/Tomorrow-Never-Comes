// --- START OF FILE DatabaseAccess.cs ---

using UnityEngine;
using Mono.Data.Sqlite;
using System.Collections.Generic;
using System;
using System.Data;
using Core.Data.Interface;
using System.Threading;
using Core.Logging;
using VContainer.Unity;

namespace Core.Data.Impl
{
    public class DatabaseAccess : IDatabaseAccess, IInitializable // IDisposable은 DatabaseCleanup이 담당
    {
        private readonly string m_ConnectionString;

        // 각 스레드에 독립적인 연결을 제공하는 ThreadLocal 연결 필드입니다.
        private readonly ThreadLocal<SqliteConnection> _threadLocalConnection;
        // 각 스레드에 독립적인 트랜잭션 객체를 제공하는 ThreadLocal 필드입니다.
        private readonly ThreadLocal<SqliteTransaction> _threadLocalTransaction;
        // 각 스레드의 트랜잭션 상태를 추적하는 ThreadLocal 필드입니다.
        private readonly ThreadLocal<bool> _threadLocalIsInTransaction;

        private const int MAX_RETRY_ATTEMPTS = 3;
        private const int RETRY_DELAY_MS = 100; // 밀리초
        private readonly SchemaManager _schemaManager;

        public DatabaseAccess(string dbPath, SchemaManager schemaManager)
        {
            if (string.IsNullOrWhiteSpace(dbPath))
            {
                throw new ArgumentException("Database path cannot be null or empty.", nameof(dbPath));
            }
            _schemaManager = schemaManager ?? throw new ArgumentNullException(nameof(schemaManager));
            m_ConnectionString = "URI=file:" + dbPath;
            CoreLogger.Log($"[DatabaseAccess] Initialized with path: {dbPath}");

            // ThreadLocal<SqliteConnection> 초기화
            _threadLocalConnection = new ThreadLocal<SqliteConnection>(() =>
            {
                var conn = new SqliteConnection(m_ConnectionString);
                conn.Open();
                CoreLogger.Log($"[DatabaseAccess] New thread-local connection opened on Thread ID: {Thread.CurrentThread.ManagedThreadId}");
                return conn;
            }, trackAllValues: true);

            // ThreadLocal<SqliteTransaction> 초기화 (초기값은 null)
            _threadLocalTransaction = new ThreadLocal<SqliteTransaction>(() => null, trackAllValues: true);
            // ThreadLocal<bool> 초기화 (초기값은 false)
            _threadLocalIsInTransaction = new ThreadLocal<bool>(() => false, trackAllValues: true);
        }

        // VContainer IInitializable 구현
        public void Initialize()
        {
            CoreLogger.Log("[DatabaseAccess] VContainer Initialize called. Initializing schema.");
            // 스키마 초기화는 메인 스레드의 연결을 통해 이루어져야 합니다.
            // IDatabaseAccess를 SchemaManager에 전달하여 DB 작업을 위임합니다.
            _schemaManager.InitializeTables(this); // 'this' (IDatabaseAccess)를 SchemaManager에 전달하여 DB 작업을 위임
            CoreLogger.Log("[DatabaseAccess] Schema initialized.");
        }

        // --- 연결 관리 ---
        public void OpenConnection()
        {
            // _threadLocalConnection.Value 접근 시 연결이 생성/열리므로, 직접 호출할 일은 적습니다.
            // 만약 명시적으로 연결을 열어야 하는 상황이라면, 현재 스레드의 연결 상태를 확인하고 엽니다.
            if (_threadLocalConnection.IsValueCreated && _threadLocalConnection.Value.State != ConnectionState.Open)
            {
                _threadLocalConnection.Value.Open();
                CoreLogger.Log($"[DatabaseAccess] Thread-local connection re-opened on Thread ID: {Thread.CurrentThread.ManagedThreadId}");
            }
            else if (!_threadLocalConnection.IsValueCreated)
            {
                // _threadLocalConnection.Value에 접근하여 연결을 생성하고 엽니다.
                // 이 호출 자체가 ThreadLocal 팩토리를 트리거합니다.
                var conn = _threadLocalConnection.Value;
                CoreLogger.Log($"[DatabaseAccess] Thread-local connection opened via OpenConnection on Thread ID: {Thread.CurrentThread.ManagedThreadId}");
            }
        }

        public void CloseConnection()
        {
            // 현재 스레드의 연결을 닫고 해제합니다.
            if (_threadLocalConnection.IsValueCreated && _threadLocalConnection.Value.State == ConnectionState.Open)
            {
                // 트랜잭션이 활성화된 상태에서 연결을 닫으면 롤백됩니다.
                if (_threadLocalIsInTransaction.Value)
                {
                    CoreLogger.LogWarning($"[DatabaseAccess] Closing connection with active transaction on Thread ID: {Thread.CurrentThread.ManagedThreadId}. Transaction will be rolled back implicitly.");
                    RollbackTransactionInternal(); // 내부적으로 롤백 처리
                }

                _threadLocalConnection.Value.Close();
                _threadLocalConnection.Value.Dispose();
                _threadLocalConnection.Value = null; // ThreadLocal에서 현재 스레드의 값 제거
                CoreLogger.Log($"[DatabaseAccess] Thread-local connection closed on Thread ID: {Thread.CurrentThread.ManagedThreadId}");
            }
        }

        // --- IDisposable 역할을 DatabaseCleanup에 위임 (DatabaseAccess 클래스에서는 제거) ---
        // DatabaseCleanup이 IDatabaseAccess를 통해 이 클래스의 리소스를 정리할 수 있도록
        // DatabaseAccess가 직접 IDisposable을 구현하지 않아도 됩니다.
        // 하지만 ThreadLocal의 자원 해제는 여전히 필요하므로, DatabaseCleanup 클래스에서
        // DatabaseAccess의 DisposeAllThreadLocalResources()와 같은 메서드를 호출하거나,
        // DatabaseAccess를 IDisposable로 캐스팅하여 Dispose를 호출하도록 해야 합니다.
        // 현재 코드에서는 IDisposable이 제거되었으므로, DatabaseCleanup에서 직접 접근하여
        // 다음 메서드를 호출하는 방식으로 디자인해야 합니다.
        public void DisposeAllThreadLocalResources()
        {
            // 모든 스레드에 할당된 SqliteConnection 및 SqliteTransaction 객체들을 닫고 해제합니다.
            if (_threadLocalConnection != null)
            {
                CoreLogger.Log("[DatabaseAccess] Disposing all thread-local connections.");
                _threadLocalConnection.Dispose();
            }
            if (_threadLocalTransaction != null)
            {
                CoreLogger.Log("[DatabaseAccess] Disposing all thread-local transactions.");
                _threadLocalTransaction.Dispose();
            }
            if (_threadLocalIsInTransaction != null)
            {
                CoreLogger.Log("[DatabaseAccess] Disposing all thread-local transaction state flags.");
                _threadLocalIsInTransaction.Dispose();
            }
        }


        // --- 트랜잭션 관리 ---
        public void BeginTransaction(IsolationLevel isolationLevel = IsolationLevel.ReadCommitted)
        {
            if (_threadLocalIsInTransaction.Value)
            {
                throw new InvalidOperationException($"[DatabaseAccess] A transaction is already active on Thread ID: {Thread.CurrentThread.ManagedThreadId}. Nested transactions are not supported by this API.");
            }

            var connection = _threadLocalConnection.Value; // 연결이 없으면 생성하고 엽니다.
            if (connection.State != ConnectionState.Open)
            {
                connection.Open();
            }

            _threadLocalTransaction.Value = connection.BeginTransaction(isolationLevel);
            _threadLocalIsInTransaction.Value = true;
            CoreLogger.Log($"[DatabaseAccess] Transaction started on Thread ID: {Thread.CurrentThread.ManagedThreadId} with IsolationLevel: {isolationLevel}");
        }

        public void CommitTransaction()
        {
            if (!_threadLocalIsInTransaction.Value)
            {
                throw new InvalidOperationException($"[DatabaseAccess] No active transaction to commit on Thread ID: {Thread.CurrentThread.ManagedThreadId}.");
            }

            try
            {
                _threadLocalTransaction.Value.Commit();
                CoreLogger.Log($"[DatabaseAccess] Transaction committed on Thread ID: {Thread.CurrentThread.ManagedThreadId}.");
            }
            catch (Exception ex)
            {
                CoreLogger.LogError($"[DatabaseAccess] Error committing transaction on Thread ID: {Thread.CurrentThread.ManagedThreadId}: {ex.Message}");
                // 커밋 실패 시 롤백 시도 (최선 노력)
                RollbackTransactionInternal();
                throw;
            }
            finally
            {
                // 트랜잭션 객체 정리
                ReleaseTransactionResources();
            }
        }

        public void RollbackTransaction()
        {
            if (!_threadLocalIsInTransaction.Value)
            {
                throw new InvalidOperationException($"[DatabaseAccess] No active transaction to rollback on Thread ID: {Thread.CurrentThread.ManagedThreadId}.");
            }

            RollbackTransactionInternal();
        }

        // 내부 롤백 처리 메서드 (예외를 던지지 않고 정리만 합니다)
        private void RollbackTransactionInternal()
        {
            try
            {
                if (_threadLocalTransaction.IsValueCreated && _threadLocalTransaction.Value != null)
                {
                    _threadLocalTransaction.Value.Rollback();
                    CoreLogger.Log($"[DatabaseAccess] Transaction rolled back on Thread ID: {Thread.CurrentThread.ManagedThreadId}.");
                }
            }
            catch (Exception ex)
            {
                CoreLogger.LogError($"[DatabaseAccess] Error rolling back transaction on Thread ID: {Thread.CurrentThread.ManagedThreadId}: {ex.Message}");
            }
            finally
            {
                // 트랜잭션 객체 정리
                ReleaseTransactionResources();
            }
        }

        // 트랜잭션 리소스 해제 헬퍼
        private void ReleaseTransactionResources()
        {
            if (_threadLocalTransaction.IsValueCreated && _threadLocalTransaction.Value != null)
            {
                _threadLocalTransaction.Value.Dispose();
                _threadLocalTransaction.Value = null;
            }
            _threadLocalIsInTransaction.Value = false;
        }

        public bool IsInTransaction => _threadLocalIsInTransaction.Value;

        // --- Command 생성 헬퍼 ---
        private SqliteCommand CreateCommand(string query, Dictionary<string, object> parameters = null)
        {
            var connection = _threadLocalConnection.Value;

            if (connection == null || connection.State != ConnectionState.Open)
            {
                CoreLogger.LogError($"[DatabaseAccess] Database connection is not open on Thread ID: {Thread.CurrentThread.ManagedThreadId}. This should not happen with ThreadLocal setup unless connection was explicitly closed.", null);
                throw new InvalidOperationException("Database connection is not open. Call OpenConnection() first.");
            }

            var command = connection.CreateCommand();
            command.CommandText = query;

            // 현재 스레드에 활성 트랜잭션이 있다면 Command에 할당합니다.
            if (_threadLocalIsInTransaction.Value)
            {
                command.Transaction = _threadLocalTransaction.Value;
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

        // --- 데이터 조회 (Read) ---
        private List<Dictionary<string, object>> ExecuteQueryInternal(string query, Dictionary<string, object> parameters = null)
        {
            return ExecuteWithRetry(() =>
                {
                    var result = new List<Dictionary<string, object>>();
                    using (var command = CreateCommand(query, parameters))
                    {
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
                    }
                    return result;
                }, $"executing read query '{query}'");
        }

        public List<Dictionary<string, object>> SelectWhere(string tableName, string[] columns, string[] operations, object[] values, string logicalOperator)
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
            return ExecuteQueryInternal(query, parameters);
        }

        public List<Dictionary<string, object>> SelectWhere(string tableName, string[] columns, string[] operations, object[] values)
        {
            return SelectWhere(tableName, columns, operations, values, "AND");
        }

        // --- 데이터 변경 (Create, Update, Delete) ---
        public void InsertInto(string tableName, string[] columns, object[] values)
        {
            ValidateTableName(tableName);
            ValidateColumnNames(tableName, columns);

            if (columns == null || values == null) throw new ArgumentNullException("Columns or values array cannot be null.");
            if (columns.Length != values.Length)
            {
                throw new ArgumentException("Length of columns and values arrays must be equal.", nameof(columns));
            }

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
            ExecuteNonQuery(query, parameters);
        }

        public void UpdateSet(string tableName, string[] updateCols, object[] updateValues, string whereCol, object whereValue)
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
            ExecuteNonQuery(query, parameters);
        }

        public void DeleteContents(string tableName)
        {
            ValidateTableName(tableName);

            string query = $"DELETE FROM {tableName}";
            CoreLogger.Log($"[DatabaseAccess] Executing DeleteContents on {tableName}");
            ExecuteNonQuery(query);
        }

        public void DeleteWhere(string tableName, string whereCol, object whereValue)
        {
            ValidateTableName(tableName);
            ValidateColumnNames(tableName, whereCol);

            if (string.IsNullOrWhiteSpace(whereCol)) throw new ArgumentException("Where column cannot be null or empty.", nameof(whereCol));

            string query = $"DELETE FROM {tableName} WHERE {whereCol} = @whereValue";
            var parameters = new Dictionary<string, object>
            {
                { "@whereValue", whereValue }
            };
            CoreLogger.Log($"[DatabaseAccess] Executing DeleteWhere on {tableName}");
            ExecuteNonQuery(query, parameters);
        }

        // --- 범용 쿼리 실행 ---
        public int ExecuteNonQuery(string query)
        {
            return ExecuteNonQuery(query, null);
        }

        public int ExecuteNonQuery(string query, Dictionary<string, object> parameters = null)
        {
            if (string.IsNullOrWhiteSpace(query)) throw new ArgumentException("Query cannot be null or empty.", nameof(query));

            return ExecuteWithRetry(() =>
                {
                    using (var command = CreateCommand(query, parameters))
                    {
                        int rowsAffected = command.ExecuteNonQuery();
                        CoreLogger.Log($"[DatabaseAccess] Executed NonQuery: '{query}'. Rows affected: {rowsAffected}");
                        return rowsAffected;
                    }
                }, $"executing non-query '{query}'");
        }

        // --- 기타 ---
        public long GetLastInsertRowId()
        {
            return ExecuteWithRetry(() =>
            {
                long lastId = 0;
                using (var command = CreateCommand("SELECT last_insert_rowid()"))
                {
                    object result = command.ExecuteScalar();
                    if (result != null && result != DBNull.Value)
                    {
                        lastId = (long)result;
                    }
                }
                return lastId;
            }, "getting last insert row ID");
        }

        private T ExecuteWithRetry<T>(Func<T> action, string operationName, bool shouldRetryTransaction = true)
        {
            for (int i = 0; i < MAX_RETRY_ATTEMPTS; i++)
            {
                try
                {
                    if (i > 0)
                    {
                        CoreLogger.LogWarning($"[DatabaseAccess] Retrying {operationName} (Attempt {i + 1}/{MAX_RETRY_ATTEMPTS})...");
                        Thread.Sleep(RETRY_DELAY_MS);
                    }
                    return action();
                }
                catch (SqliteException ex)
                {
                    if (i < MAX_RETRY_ATTEMPTS - 1)
                    {
                        CoreLogger.LogWarning($"[DatabaseAccess] Transient error during {operationName}: {ex.Message}. Will retry.");
                        // 트랜잭션 중 발생한 오류라면 롤백 후 재시도
                        if (IsInTransaction && shouldRetryTransaction)
                        {
                            CoreLogger.LogWarning($"[DatabaseAccess] Rolling back current transaction before retry for {operationName}.");
                            RollbackTransactionInternal(); // RollbackTransaction을 호출하여 상태를 정리
                        }
                        continue;
                    }
                    CoreLogger.LogError($"[DatabaseAccess] Failed to {operationName} after {MAX_RETRY_ATTEMPTS} attempts: {ex.Message}");
                    throw;
                }
                catch (InvalidOperationException ex)
                {
                    // 연결 끊김 등 재시도 불가능한 오류는 즉시 던집니다.
                    CoreLogger.LogError($"[DatabaseAccess] Non-retryable error during {operationName}: {ex.Message}");
                    throw;
                }
                catch (Exception ex)
                {
                    CoreLogger.LogError($"[DatabaseAccess] Unexpected error during {operationName}: {ex.Message}");
                    throw;
                }
            }
            throw new InvalidOperationException($"[DatabaseAccess] Failed to {operationName} after {MAX_RETRY_ATTEMPTS} attempts without throwing specific exception.");
        }

        private void ExecuteWithRetry(Action action, string operationName, bool shouldRetryTransaction = true)
        {
            ExecuteWithRetry<bool>(() => { action(); return true; }, operationName, shouldRetryTransaction);
        }
    }
}
// --- END OF FILE DatabaseAccess.cs ---