// --- START OF FILE DatabaseAccess.cs ---

using UnityEngine;
using Mono.Data.Sqlite;
using System.Collections.Generic;
using System;
using System.Data; // IsolationLevel, ConnectionState를 위해 추가
using Core.Data.Interface; // IDatabaseAccess를 사용하기 위해 추가
using System.Threading;
using Core.Logging;
using VContainer.Unity;

namespace Core.Data.Impl
{
    public class DatabaseAccess : IDatabaseAccess, IInitializable
    {
        private readonly string m_ConnectionString;
        private SqliteConnection m_Connection;
        private SqliteTransaction m_Transaction;
        private bool _isInTransaction = false; // IsInTransaction 속성 구현을 위한 내부 필드
        private const int MAX_RETRY_ATTEMPTS = 3;
        private const int RETRY_DELAY_MS = 100; // 밀리초
        private readonly SchemaManager _schemaManager;
        private readonly ThreadLocal<SqliteConnection> _threadLocalConnection;

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
            // 각 스레드에서 .Value에 접근할 때 연결을 생성하고 엽니다.
            _threadLocalConnection = new ThreadLocal<SqliteConnection>(() =>
            {
                var conn = new SqliteConnection(m_ConnectionString);
                conn.Open(); // 스레드 로컬 연결은 생성 시 바로 엽니다.
                CoreLogger.Log($"[DatabaseAccess] New thread-local connection opened on Thread ID: {Thread.CurrentThread.ManagedThreadId}");
                return conn;
            }, trackAllValues: true); // trackAllValues: true는 모든 스레드에 할당된 값들을 추적하여 Dispose 시 모두 닫을 수 있게 합니다.
        }
        public void Initialize()
        {
            CoreLogger.Log("[DatabaseAccess] VContainer Initialize called. Initializing schema.");
        }
        // --- 연결 관리 ---
        public void OpenConnection()
        {
            // _threadLocalConnection.Value 접근 시 이미 연결이 열리도록 설정되어 있습니다.
            // 만약 필요하다면, 여기서 추가적인 로직을 넣을 수 있습니다.
            if (_threadLocalConnection.Value.State != ConnectionState.Open)
            {
                _threadLocalConnection.Value.Open();
                CoreLogger.Log($"[DatabaseAccess] Thread-local connection re-opened on Thread ID: {Thread.CurrentThread.ManagedThreadId}");
            }
        }


        public void CloseConnection()
        {
            if (_threadLocalConnection.IsValueCreated && _threadLocalConnection.Value.State == ConnectionState.Open)
            {
                // 트랜잭션 관리: 현재 스레드에 활성 트랜잭션이 있다면 롤백
                // if (_threadLocalIsInTransaction.Value) { RollbackTransaction(); }

                _threadLocalConnection.Value.Close();
                _threadLocalConnection.Value.Dispose();
                _threadLocalConnection.Value = null; // ThreadLocal에서 현재 스레드의 값 제거
                CoreLogger.Log(
                    $"[DatabaseAccess] Thread-local connection closed on Thread ID: {Thread.CurrentThread.ManagedThreadId}");
            }
        }
        public void Dispose()
        {
            // 모든 스레드에 할당된 SqliteConnection 객체들을 닫고 해제합니다.
            if (_threadLocalConnection != null)
            {
                CoreLogger.Log("[DatabaseAccess] Disposing all thread-local connections.");
                _threadLocalConnection.Dispose();
            }
        }

        // --- 트랜잭션 관리 ---
        public void BeginTransaction(IsolationLevel isolationLevel = IsolationLevel.ReadCommitted)
        {
            if (m_Connection == null || m_Connection.State != ConnectionState.Open)
            {
                throw new InvalidOperationException("Connection is not open. Call OpenConnection() first.");
            }
            if (_isInTransaction)
            {
                throw new InvalidOperationException("A transaction is already active.");
            }
            ExecuteWithRetry(() =>
            {
                m_Transaction = m_Connection.BeginTransaction(isolationLevel);
                CoreLogger.Log($"[DatabaseAccess] Transaction started with IsolationLevel: {isolationLevel}");
                return true;
            }, "beginning transaction", false);
        }

        public void CommitTransaction()
        {
            if (!_isInTransaction || m_Transaction == null)
            {
                throw new InvalidOperationException("No active transaction to commit.");
            }
            try
            {
                m_Transaction.Commit();
                CoreLogger.Log("[DatabaseAccess] Transaction committed.");
            }
            catch (SqliteException ex)
            {
                CoreLogger.LogError($"[DatabaseAccess] Error committing transaction: {ex.Message}");
                // 커밋 실패 시 롤백 시도하여 일관성 유지
                RollbackTransaction();
                throw new InvalidOperationException("Error during transaction commit.", ex);
            }
            ExecuteWithRetry(() =>
            {
                m_Transaction.Commit();
                CoreLogger.Log("[DatabaseAccess] Transaction committed successfully.");
                return true;
            }, "committing transaction");
            _isInTransaction = false; // 트랜잭션 종료 시 상태 업데이트
            m_Transaction.Dispose(); // finally 블록에서 수행되던 부분 이동
            m_Transaction = null;    // finally 블록에서 수행되던 부분 이동
        }

        public void RollbackTransaction()
        {
            if (!_isInTransaction || m_Transaction == null)
            {
                throw new InvalidOperationException("No active transaction to rollback.");
            }
            try
            {
                m_Transaction.Rollback();
                CoreLogger.Log("[DatabaseAccess] Transaction rolled back.");
            }
            catch (SqliteException ex)
            {
                CoreLogger.LogError($"[DatabaseAccess] Error rolling back transaction: {ex.Message}");
                throw new InvalidOperationException("Error during transaction rollback.", ex);
            }
            finally
            {
                m_Transaction.Dispose();
                m_Transaction = null;
                _isInTransaction = false; // 트랜잭션 종료 시 상태 업데이트
            }
        }

        // IsInTransaction 속성 구현
        public bool IsInTransaction => _isInTransaction;

        // --- Command 생성 헬퍼 (누락된 부분 추가) ---
        private SqliteCommand CreateCommand(string query, Dictionary<string, object> parameters = null)
        {
            // 현재 스레드의 연결을 사용합니다.
            var connection = _threadLocalConnection.Value; // <<--- 현재 스레드의 연결 사용!

            if (connection == null || connection.State != ConnectionState.Open)
            {
                CoreLogger.LogError($"[DatabaseAccess] Database connection is not open on Thread ID: {Thread.CurrentThread.ManagedThreadId}. Call OpenConnection() first. (This should not happen with ThreadLocal setup)", null);
                throw new InvalidOperationException("Database connection is not open. Call OpenConnection() first.");
            }

            var command = connection.CreateCommand();
            command.CommandText = query;
            // 트랜잭션 로직도 스레드 로컬 트랜잭션 객체를 사용해야 합니다.
            // if (m_Transaction != null) { command.Transaction = m_Transaction; }

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
            // P18: SQL 화이트리스트/enum 매핑 부재 (SchemaManager로 완전 해결)
            if (!_schemaManager.IsTableNameValid(tableName))
            {
                CoreLogger.LogError($"[DatabaseAccess] Attempted to access an unallowed or invalid table: '{tableName}'. Check SchemaManager configuration.");
                throw new ArgumentException($"[DatabaseAccess] Access to table '{tableName}' is not allowed or it does not exist in schema.");
            }
        }

        // P17: SQL 식별자 문자열 삽입 취약 - 컬럼 이름 유효성 검사 강화
        private void ValidateColumnNames(string tableName, params string[] columnNames) // params 키워드 추가
        {
            if (columnNames == null || columnNames.Length == 0) return;

            foreach (string colName in columnNames)
            {
                if (string.IsNullOrWhiteSpace(colName)) // 컬럼 이름 자체의 null/empty 검사 추가
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
                    // CreateCommand에서 이미 _threadLocalConnection.Value를 사용하고 연결은 열려있으므로,
                    // 여기서 별도의 connection.Open()은 필요 없습니다.
                    // 만약 연결이 끊어졌다면 ThreadLocal 생성자가 다시 열어줄 것입니다.

                    var result = new List<Dictionary<string, object>>();
                    using (var command = CreateCommand(query, parameters)) // <<--- 매개변수 connection 제거
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

        // IDatabaseAccess 구현
        public List<Dictionary<string, object>> SelectWhere(string tableName, string[] columns, string[] operations, object[] values, string logicalOperator)
        {
            ValidateTableName(tableName);
            ValidateColumnNames(tableName, columns); // WHERE 절 컬럼 이름 검증

            if (columns == null || operations == null || values == null) throw new ArgumentNullException("Columns, operations, or values array cannot be null.");
            if (columns.Length != operations.Length || columns.Length != values.Length)
            {
                throw new ArgumentException("Length of columns, operations, and values arrays must be equal.", nameof(columns));
            }
            if (string.IsNullOrWhiteSpace(logicalOperator)) throw new ArgumentException("Logical operator cannot be null or empty.", nameof(logicalOperator));

            // logicalOperator 유효성 검증 강화
            string upperLogicalOperator = logicalOperator.Trim().ToUpper();
            if (!(upperLogicalOperator == "AND" || upperLogicalOperator == "OR"))
            {
                throw new ArgumentException($"Invalid logical operator: '{logicalOperator}'. Only 'AND' or 'OR' are allowed.", nameof(logicalOperator));
            }


            string query = $"SELECT * FROM {tableName} WHERE "; // 이 메서드는 인터페이스 정의에 따라 모든 컬럼을 선택합니다.
            var parameters = new Dictionary<string, object>();

            for (int i = 0; i < columns.Length; i++)
            {
                string paramName = $"@param{i}";
                query += $"{columns[i]} {operations[i]} {paramName}";
                if (i < columns.Length - 1)
                {
                    query += $" {upperLogicalOperator} "; // 검증된 연산자 사용
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

        // IDatabaseAccess 구현
        public void InsertInto(string tableName, string[] columns, object[] values)
        {
            ValidateTableName(tableName);
            ValidateColumnNames(tableName, columns); // INSERT 절 컬럼 이름 검증

            if (columns == null || values == null) throw new ArgumentNullException("Columns or values array cannot be null.");
            if (columns.Length != values.Length)
            {
                throw new ArgumentException("Length of columns and values arrays must be equal.", nameof(columns));
            }

            // 컬럼 이름에 `@` 접두사를 붙여 매개변수 이름으로 사용
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

        // IDatabaseAccess 구현
        public void UpdateSet(string tableName, string[] updateCols, object[] updateValues, string whereCol, object whereValue)
        {
            ValidateTableName(tableName);
            ValidateColumnNames(tableName, updateCols); // UPDATE SET 절 컬럼 이름 검증
            ValidateColumnNames(tableName, whereCol); // WHERE 절 컬럼 이름 검증

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

        // IDatabaseAccess 구현
        public void DeleteContents(string tableName)
        {
            ValidateTableName(tableName);

            string query = $"DELETE FROM {tableName}";
            CoreLogger.Log($"[DatabaseAccess] Executing DeleteContents on {tableName}");
            ExecuteNonQuery(query);
        }

        // IDatabaseAccess 구현
        public void DeleteWhere(string tableName, string whereCol, object whereValue)
        {
            ValidateTableName(tableName);
            ValidateColumnNames(tableName, whereCol); // WHERE 절 컬럼 이름 검증

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
        // IDatabaseAccess 구현 (매개변수 없는 오버로드)
        public int ExecuteNonQuery(string query)
        {
            return ExecuteNonQuery(query, null);
        }

        // IDatabaseAccess 구현 (매개변수 있는 오버로드)
        public int ExecuteNonQuery(string query, Dictionary<string, object> parameters = null)
        {
            if (string.IsNullOrWhiteSpace(query)) throw new ArgumentException("Query cannot be null or empty.", nameof(query));

            return ExecuteWithRetry(() =>
                {
                    // CreateCommand에서 이미 _threadLocalConnection.Value를 사용하고 연결은 열려있으므로,
                    // 여기서 별도의 connection.Open()은 필요 없습니다.

                    using (var command = CreateCommand(query, parameters)) // <<--- 매개변수 connection 제거
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
                        if (IsInTransaction && shouldRetryTransaction)
                        {
                            CoreLogger.LogWarning($"[DatabaseAccess] Rolling back current transaction before retry for {operationName}.");
                            RollbackTransaction();
                        }
                        continue;
                    }
                    CoreLogger.LogError($"[DatabaseAccess] Failed to {operationName} after {MAX_RETRY_ATTEMPTS} attempts: {ex.Message}");
                    throw;
                }
                catch (InvalidOperationException ex)
                {
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

    // ExecuteWithRetry 오버로드 (void 액션용)
        private void ExecuteWithRetry(Action action, string operationName, bool shouldRetryTransaction = true)
        {
            ExecuteWithRetry<bool>(() => { action(); return true; }, operationName, shouldRetryTransaction);
        }
    }
}
// --- END OF FILE DatabaseAccess.cs ---