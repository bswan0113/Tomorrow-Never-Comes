// using System; // 필요한 네임스페이스들은 그대로 유지

using System;
using UnityEngine;
using System.Collections.Generic;
using System.IO;
using Core.Interface;
using Core.Interface.Core.Interface;
using Newtonsoft.Json;



public class DataManager : MonoBehaviour, IDataService // IDataService 인터페이스 구현 추가
{
    // --- 싱글턴 설정 --- <- 제거

    private Dictionary<string, string> m_SQLQueries;

    // --- DB 접근 및 데이터 ---
    // DatabaseAccess는 외부에서 주입받도록 변경
    private IDatabaseAccess _dbAccess; // 이제 인터페이스 타입으로 의존성을 저장

    // HasSaveData는 private set이므로 생성자 또는 Initialize에서 설정해야 합니다.
    public bool HasSaveData { get; private set; } = false;



    public void Initialize(IDatabaseAccess dbAccess)
    {
        _dbAccess = dbAccess ?? throw new ArgumentNullException(nameof(dbAccess));

        // DB 연결은 DatabaseAccess 자체에서 관리하는 것이 더 적절합니다.
        // DataManager는 이미 연결된 DatabaseAccess를 사용한다고 가정합니다.
        // P0 문제점 "OnApplicationPause/Focus 처리 미흡"은 DatabaseAccess에서 처리하는 것이 좋습니다.

        LoadSQLQueries();
        InitializeDatabaseTables();
        LoadAllGameData(); // 초기 로드 후 HasSaveData 설정

        Debug.Log("DataManager Initialized.");
    }

    public void InsertData(string tableName, string[] columns, object[] values)
    {
        if (_dbAccess == null)
        {
            Debug.LogError("DataManager: DatabaseAccess가 초기화되지 않았습니다.");
            return;
        }
        _dbAccess.InsertInto(tableName, columns, values);
    }


    private void LoadSQLQueries()
    {
        TextAsset sqlJson = Resources.Load<TextAsset>("SQLSchemas");
        if (sqlJson == null)
        {
            Debug.LogError("Resources/SQLSchemas.json 파일을 찾을 수 없습니다!");
            m_SQLQueries = new Dictionary<string, string>();
            return;
        }
        m_SQLQueries = JsonConvert.DeserializeObject<Dictionary<string, string>>(sqlJson.text);
    }

    private void InitializeDatabaseTables()
    {
        if (_dbAccess == null)
        {
            Debug.LogError("DataManager: DatabaseAccess가 초기화되지 않았습니다. Initialize()를 먼저 호출해주세요.");
            return;
        }

        foreach (var queryPair in m_SQLQueries)
        {
            // P0: SQL 식별자 문자열 삽입 취약 - 이 부분은 나중에 개선해야 합니다.
            _dbAccess.ExecuteNonQuery(queryPair.Value);
            Debug.Log($"Executed table creation query: {queryPair.Key}");
        }
        Debug.Log("Database tables are verified.");
    }

    public void LoadAllGameData()
    {
        if (_dbAccess == null)
        {
            Debug.LogError("DataManager: DatabaseAccess가 초기화되지 않았습니다.");
            return;
        }

        var loadedData = LoadData("PlayerStats", "SaveSlotID", 1);

        HasSaveData = (loadedData != null && loadedData.Count > 0);

        Debug.Log($"DataManager: HasSaveData = {HasSaveData}");
    }

    // IDataService 인터페이스에 SaveAllGameData는 현재 정의되어 있지 않으므로,
    // 필요하다면 인터페이스에 추가하거나 다른 방식으로 처리해야 합니다.
    public void SaveAllGameData()
    {
        Debug.Log("모든 게임 데이터를 저장합니다... (이 메서드는 아직 외부에서 호출되지 않을 수 있습니다)");
        // 예시:
        // UpdateData("PlayerStatus", new string[]{"Level"}, new object[]{Status.Level}, "PlayerID", 1);
    }

    // 새 게임 시작 시 데이터를 초기화하는 메서드 (IDataService에 포함)
    public void CreateNewGameData()
    {
        if (_dbAccess == null)
        {
            Debug.LogError("FATAL ERROR: DatabaseAccess가 초기화되지 않았습니다. DataManager initialization failed.");
            return;
        }

        _dbAccess.DeleteContents("GameProgress");
        _dbAccess.InsertInto("GameProgress", new string[]{"SaveSlotID", "CurrentDay"}, new object[]{1, 1});

        HasSaveData = true;
        Debug.Log("New game data created.");
    }


    // --- 범용 CRUD 메서드 (IDataService 인터페이스 메서드 구현) ---

    public List<Dictionary<string, object>> LoadData(string tableName, string whereCol, object whereValue)
    {
        if (_dbAccess == null)
        {
            Debug.LogError("DataManager: DatabaseAccess가 초기화되지 않았습니다.");
            return null;
        }
        // P0: SQL 식별자 문자열 삽입 취약 - 이 부분도 나중에 개선해야 합니다.
        return _dbAccess.SelectWhere(tableName, new string[] { whereCol }, new string[] { "=" }, new object[] { whereValue });
    }

    public void SaveData(string tableName, string[] columns, object[] values)
    {
        if (_dbAccess == null)
        {
            Debug.LogError("DataManager: DatabaseAccess가 초기화되지 않았습니다.");
            return;
        }
        _dbAccess.InsertInto(tableName, columns, values);
    }

    public void UpdateData(string tableName, string[] updateCols, object[] updateValues, string whereCol, object whereValue)
    {
        if (_dbAccess == null)
        {
            Debug.LogError("DataManager: DatabaseAccess가 초기화되지 않았습니다.");
            return;
        }
        _dbAccess.UpdateSet(tableName, updateCols, updateValues, whereCol, whereValue);
    }

    public void DeleteData(string tableName, string whereCol, object whereValue)
    {
        if (_dbAccess == null)
        {
            Debug.LogError("DataManager: DatabaseAccess가 초기화되지 않았습니다.");
            return;
        }
        _dbAccess.DeleteWhere(tableName, whereCol, whereValue);
    }
}