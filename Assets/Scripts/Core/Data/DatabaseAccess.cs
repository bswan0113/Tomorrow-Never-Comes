// C:\Workspace\Tomorrow Never Comes\Assets\Scripts\Core\Data\DatabaseAccess.cs

using UnityEngine;
using Mono.Data.Sqlite;
using System.Collections.Generic;
using System; // ArgumentNullException, InvalidOperationException, System.Data.ConnectionState 등을 위해 추가
using System.Data;
using Core.Interface; // ConnectionState를 위해 추가

public class DatabaseAccess : IDatabaseAccess // 인터페이스 구현 추가
{
    private readonly string m_ConnectionString;
    private SqliteConnection m_Connection; // 클래스 전체에서 공유할 연결 객체

    public DatabaseAccess(string dbPath)
    {
        if (string.IsNullOrWhiteSpace(dbPath))
        {
            throw new ArgumentException("Database path cannot be null or empty.", nameof(dbPath));
        }
        m_ConnectionString = "URI=file:" + dbPath;
        Debug.Log($"DatabaseAccess initialized with path: {dbPath}");
    }

    // --- 연결 관리 (IDatabaseAccess 구현) ---
    public void OpenConnection()
    {
        if (m_Connection != null && m_Connection.State == ConnectionState.Open)
        {
            Debug.Log("Database connection already open.");
            return; // 이미 열려있으면 아무것도 하지 않음
        }

        try
        {
            m_Connection = new SqliteConnection(m_ConnectionString);
            m_Connection.Open();
            Debug.Log("Database connection opened.");
        }
        catch (SqliteException ex)
        {
            Debug.LogError($"Failed to open database connection: {ex.Message}");
            throw; // 연결 실패 시 예외를 던져 상위 호출자가 처리하도록 함
        }
    }

    public void CloseConnection()
    {
        if (m_Connection != null && m_Connection.State != ConnectionState.Closed)
        {
            m_Connection.Close();
            m_Connection.Dispose(); // 연결 객체 리소스 해제
            m_Connection = null;
            Debug.Log("Database connection closed.");
        }
    }

    // --- 내부 헬퍼 메서드 ---
    private SqliteCommand CreateCommand(string query, Dictionary<string, object> parameters = null)
    {
        if (m_Connection == null || m_Connection.State != ConnectionState.Open)
        {
            // 연결이 열려있지 않으면 InvalidOperationException을 던짐
            throw new InvalidOperationException("Database connection is not open. Call OpenConnection() first.");
        }

        var command = m_Connection.CreateCommand();
        command.CommandText = query;
        if (parameters != null)
        {
            foreach (var param in parameters)
            {
                command.Parameters.AddWithValue(param.Key, param.Value);
            }
        }
        return command;
    }

    // --- 데이터 조회 (Read) ---
    // ExecuteQuery는 SelectWhere에서 내부적으로 사용하므로 private으로 유지
    private List<Dictionary<string, object>> ExecuteQueryInternal(string query, Dictionary<string, object> parameters = null)
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
    }

    // IDatabaseAccess 구현
    public List<Dictionary<string, object>> SelectWhere(string tableName, string[] columns, string[] operations, object[] values, string logicalOperator) // logicalOperator에 = "AND" 제거
    {
        if (string.IsNullOrWhiteSpace(tableName)) throw new ArgumentException("Table name cannot be null or empty.", nameof(tableName));
        if (columns == null || operations == null || values == null) throw new ArgumentNullException("Columns, operations, or values array cannot be null.");
        if (columns.Length != operations.Length || columns.Length != values.Length)
        {
            throw new ArgumentException("Length of columns, operations, and values arrays must be equal.", nameof(columns));
        }
        if (string.IsNullOrWhiteSpace(logicalOperator)) throw new ArgumentException("Logical operator cannot be null or empty.", nameof(logicalOperator));


        string query = $"SELECT * FROM {tableName} WHERE ";
        var parameters = new Dictionary<string, object>();

        for (int i = 0; i < columns.Length; i++)
        {
            string paramName = $"@param{i}";
            query += $"{columns[i]} {operations[i]} {paramName}";
            if (i < columns.Length - 1)
            {
                query += $" {logicalOperator} ";
            }
            parameters[paramName] = values[i];
        }

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
        if (string.IsNullOrWhiteSpace(tableName)) throw new ArgumentException("Table name cannot be null or empty.", nameof(tableName));
        if (columns == null || values == null) throw new ArgumentNullException("Columns or values array cannot be null.");
        if (columns.Length != values.Length)
        {
            throw new ArgumentException("Length of columns and values arrays must be equal.", nameof(columns));
        }

        string query = $"INSERT INTO {tableName} ({string.Join(", ", columns)}) VALUES (@{string.Join(", @", columns)})";
        var parameters = new Dictionary<string, object>();
        for (int i = 0; i < columns.Length; i++)
        {
            parameters[$"@{columns[i]}"] = values[i];
        }

        ExecuteNonQuery(query, parameters);
    }

    // IDatabaseAccess 구현
    public void UpdateSet(string tableName, string[] updateCols, object[] updateValues, string whereCol, object whereValue)
    {
        if (string.IsNullOrWhiteSpace(tableName)) throw new ArgumentException("Table name cannot be null or empty.", nameof(tableName));
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

        ExecuteNonQuery(query, parameters);
    }

    // IDatabaseAccess 구현
    public void DeleteContents(string tableName)
    {
        if (string.IsNullOrWhiteSpace(tableName)) throw new ArgumentException("Table name cannot be null or empty.", nameof(tableName));
        string query = $"DELETE FROM {tableName}";
        ExecuteNonQuery(query);
    }

    // IDatabaseAccess 구현 (매개변수 없는 오버로드)
    public int ExecuteNonQuery(string query)
    {
        return ExecuteNonQuery(query, null); // 매개변수 있는 오버로드 호출
    }

    // IDatabaseAccess 구현 (매개변수 있는 오버로드)
    public int ExecuteNonQuery(string query, Dictionary<string, object> parameters = null)
    {
        if (string.IsNullOrWhiteSpace(query)) throw new ArgumentException("Query cannot be null or empty.", nameof(query));
        using (var command = CreateCommand(query, parameters))
        {
            return command.ExecuteNonQuery();
        }
    }

    // GetLastInsertRowId는 IDatabaseAccess에 포함되지 않으므로, public으로 유지하되
    // 필요에 따라 private으로 변경하거나 별도의 인터페이스로 분리할 수 있습니다.
    // 현재 DataManager가 직접 호출하지 않으므로 그대로 둡니다.
    public long GetLastInsertRowId()
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
    }

    // IDatabaseAccess 구현
    public void DeleteWhere(string tableName, string whereCol, object whereValue)
    {
        if (string.IsNullOrWhiteSpace(tableName)) throw new ArgumentException("Table name cannot be null or empty.", nameof(tableName));
        if (string.IsNullOrWhiteSpace(whereCol)) throw new ArgumentException("Where column cannot be null or empty.", nameof(whereCol));

        string query = $"DELETE FROM {tableName} WHERE {whereCol} = @whereValue";
        var parameters = new Dictionary<string, object>
        {
            { "@whereValue", whereValue }
        };
        ExecuteNonQuery(query, parameters);
    }
}