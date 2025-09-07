using System;
using UnityEngine;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

public class DataManager : MonoBehaviour
{
    // --- 싱글턴 설정 ---
    private static DataManager _instance;
    public static DataManager Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject go = new GameObject("DataManager");
                _instance = go.AddComponent<DataManager>();
            }
            return _instance;
        }
    }

    private Dictionary<string, string> m_SQLQueries;

    // --- DB 접근 및 데이터 ---
    private DatabaseAccess m_DB;
    public bool HasSaveData { get; private set; } = false;

    void Awake()
    {
        // --- 싱글턴 인스턴스 관리 ---
        if (_instance != null && _instance != this)
        {
            Destroy(this.gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(this.gameObject);

        // --- 모든 데이터 로드 ---
        Debug.LogWarning($"[DataManager AWAKE] DataManager ID: {this.GetInstanceID()} / DB 초기화를 진행합니다.", this.gameObject);
        LoadSQLQueries();

        // <<-- 변경점 1: DB 객체 생성 및 연결을 Awake에서 한 번만 수행합니다.
        string dbPath = Path.Combine(Application.persistentDataPath, "PlayerSaveData.db");
        m_DB = new DatabaseAccess(dbPath);
        m_DB.OpenConnection(); // 여기서 연결을 엽니다.

        InitializeDatabaseTables();
        LoadAllGameData();
    }

    // <<-- 변경점 2: 게임 종료 시 호출되는 OnApplicationQuit() 에서 연결을 닫습니다.
    private void OnApplicationQuit()
    {
        if (m_DB != null)
        {
            m_DB.CloseConnection();
            Debug.Log("Database connection closed on application quit.");
        }
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
        foreach (var queryPair in m_SQLQueries)
        {
            m_DB.ExecuteNonQuery(queryPair.Value);
            Debug.Log($"Executed table creation query: {queryPair.Key}");
        }
        Debug.Log("Database tables are verified.");
    }

    public void LoadAllGameData()
    {
        var loadedData = LoadData("PlayerStats", "SaveSlotID", 1);

        if (loadedData != null && loadedData.Count > 0)
        {
            HasSaveData = true;
        }
        else
        {
            HasSaveData = false;
        }
    }

    public void SaveAllGameData()
    {
        Debug.Log("모든 게임 데이터를 저장합니다...");
        // 예시:
        // UpdateData("PlayerStatus", new string[]{"Level"}, new object[]{Status.Level}, "PlayerID", 1);
    }

    // 새 게임 시작 시 데이터를 초기화하는 메서드
    public void CreateNewGameData()
    {
        // <<-- 변경점 3: 불필요한 Open/Close 로직을 모두 제거했습니다.
        if (m_DB == null)
        {
            Debug.LogError("FATAL ERROR: m_DB is null. DataManager initialization failed.");
            return;
        }

        // 여기에 새로운 게임 데이터(기본 스탯 등)를 DB에 INSERT 하는 로직을 추가합니다.
        // 예: SaveData("PlayerStatus", new string[]{"HP", "Intellect"}, new object[]{100, 10});
        m_DB.DeleteContents("GameProgress");
        m_DB.InsertInto("GameProgress", new string[]{"SaveSlotID", "CurrentDay"}, new object[]{1, 1});

        HasSaveData = true;
        Debug.Log("New game data created.");
    }


    // --- 범용 CRUD 메서드 ---

    /// <summary>
    /// 특정 테이블에서 조건에 맞는 데이터를 불러옵니다.
    /// </summary>
    public List<Dictionary<string, object>> LoadData(string tableName, string whereCol, object whereValue)
    {
        // <<-- 변경점 4: 모든 CRUD 메서드에서 반복적인 연결/해제 코드를 제거하여 코드를 간결하고 효율적으로 만듭니다.
        return m_DB.SelectWhere(tableName, new string[] { whereCol }, new string[] { "=" }, new object[] { whereValue });
    }

    /// <summary>
    /// 특정 테이블에 새로운 데이터 행을 삽입합니다.
    /// </summary>
    public void SaveData(string tableName, string[] columns, object[] values)
    {
        m_DB.InsertInto(tableName, columns, values);
    }

    /// <summary>
    /// 특정 테이블에서 조건에 맞는 데이터 행을 수정합니다.
    /// </summary>
    public void UpdateData(string tableName, string[] updateCols, object[] updateValues, string whereCol, object whereValue)
    {
        m_DB.UpdateSet(tableName, updateCols, updateValues, whereCol, whereValue);
    }

    /// <summary>
    /// 특정 테이블에서 조건에 맞는 데이터 행을 삭제합니다.
    /// </summary>
    public void DeleteData(string tableName, string whereCol, object whereValue)
    {
        m_DB.DeleteWhere(tableName, whereCol, whereValue);
    }
}