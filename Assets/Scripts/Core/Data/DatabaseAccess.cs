using UnityEngine;
using Mono.Data.Sqlite;
using System.Collections.Generic;

public class DatabaseAccess
{
    private readonly string m_ConnectionString;
    private SqliteConnection m_Connection; // 클래스 전체에서 공유할 연결 객체

    public DatabaseAccess(string dbPath)
    {
        m_ConnectionString = "URI=file:" + dbPath;
    }

    // --- 연결 관리 ---
    public void OpenConnection()
    {
        if (m_Connection != null && m_Connection.State == System.Data.ConnectionState.Open)
        {
            return; // 이미 열려있으면 아무것도 하지 않음
        }
        m_Connection = new SqliteConnection(m_ConnectionString);
        m_Connection.Open();
    }

    public void CloseConnection()
    {
        if (m_Connection != null && m_Connection.State != System.Data.ConnectionState.Closed)
        {
            m_Connection.Close();
        }
        m_Connection = null;
    }

    // --- 내부 헬퍼 메서드 ---
    private SqliteCommand CreateCommand(string query, Dictionary<string, object> parameters = null)
    {
        if (m_Connection == null || m_Connection.State != System.Data.ConnectionState.Open)
        {
            throw new SqliteException("Database connection is not open.");
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
    public List<Dictionary<string, object>> ExecuteQuery(string query, Dictionary<string, object> parameters = null)
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

    public List<Dictionary<string, object>> SelectWhere(string tableName, string[] columns, string[] operations, object[] values, string logicalOperator = "AND")
    {
        if (columns.Length != operations.Length || columns.Length != values.Length)
        {
            throw new SqliteException("컬럼, 연산자, 값 배열의 길이가 일치해야 합니다.");
        }

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

        return ExecuteQuery(query, parameters);
    }


    // --- 데이터 변경 (Create, Update, Delete) ---
    public void InsertInto(string tableName, string[] columns, object[] values)
    {
        if (columns.Length != values.Length)
        {
            throw new SqliteException("컬럼과 값 배열의 길이가 일치해야 합니다.");
        }

        string query = $"INSERT INTO {tableName} ({string.Join(", ", columns)}) VALUES (@{string.Join(", @", columns)})";
        var parameters = new Dictionary<string, object>();
        for (int i = 0; i < columns.Length; i++)
        {
            parameters[$"@{columns[i]}"] = values[i];
        }

        ExecuteNonQuery(query, parameters);
    }

    public void UpdateSet(string tableName, string[] updateCols, object[] updateValues, string whereCol, object whereValue)
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

        ExecuteNonQuery(query, parameters);
    }

    public void DeleteContents(string tableName)
    {
        string query = $"DELETE FROM {tableName}";
        ExecuteNonQuery(query);
    }

    public int ExecuteNonQuery(string query, Dictionary<string, object> parameters = null)
    {
        using (var command = CreateCommand(query, parameters))
        {
            return command.ExecuteNonQuery();
        }
    }

    public long GetLastInsertRowId()
    {
        long lastId = 0;
        using (var command = CreateCommand("SELECT last_insert_rowid()"))
        {
            object result = command.ExecuteScalar();
            if (result != null && result != System.DBNull.Value)
            {
                lastId = (long)result;
            }
        }
        return lastId;
    }

    public void DeleteWhere(string tableName, string whereCol, object whereValue)
    {
        string query = $"DELETE FROM {tableName} WHERE {whereCol} = @whereValue";
        var parameters = new Dictionary<string, object>
        {
            { "@whereValue", whereValue }
        };
        ExecuteNonQuery(query, parameters);
    }
}