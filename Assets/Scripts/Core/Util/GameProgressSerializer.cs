using Core.Data.Interface;
using Features.Data;

namespace Core.Util
{
    // --- START OF FILE GameProgressSerializer.cs ---

// C:\Workspace\Tomorrow Never Comes\Assets\Scripts\Game\Serializers\GameProgressSerializer.cs

using System;
using System.Collections.Generic;
using Core.Interface; // IDataSerializer 인터페이스를 사용하기 위해 필요

/// <summary>
/// GameProgressData 객체를 Dictionary 형태로 직렬화하고 역직렬화하는 클래스입니다.
/// IDataSerializer 인터페이스를 구현하여 DataManager와 연동됩니다.
/// </summary>
public class GameProgressSerializer : IDataSerializer<GameProgressData>
{
    private const string TABLE_NAME = "GameProgress"; // 이 시리얼라이저가 다룰 테이블 이름
    private const string PRIMARY_KEY_COLUMN = "SaveSlotID"; // 주 키 컬럼 이름
    private const int PRIMARY_KEY_DEFAULT_VALUE = 1; // 주 키의 기본값

    public Dictionary<string, object> Serialize(GameProgressData data)
    {
        if (data == null) throw new ArgumentNullException(nameof(data), "[GameProgressSerializer] Data to serialize cannot be null.");

        return new Dictionary<string, object>
        {
            { "SaveSlotID", data.SaveSlotID },
            { "CurrentDay", data.CurrentDay },
            { "LastSceneName", data.LastSceneName },
            { "SaveDateTime", data.SaveDateTime.ToString("o") } // ISO 8601 형식으로 저장
        };
    }

    public GameProgressData Deserialize(Dictionary<string, object> dataMap)
    {
        if (dataMap == null || dataMap.Count == 0)
        {
            UnityEngine.Debug.LogWarning("[GameProgressSerializer] Data map is null or empty, returning null GameProgressData.");
            return null;
        }

        try
        {
            return new GameProgressData
            {
                SaveSlotID = Convert.ToInt32(dataMap[PRIMARY_KEY_COLUMN]),
                CurrentDay = Convert.ToInt32(dataMap["CurrentDay"]),
                LastSceneName = dataMap["LastSceneName"].ToString(),
                SaveDateTime = DateTime.Parse(dataMap["SaveDateTime"].ToString(), null, System.Globalization.DateTimeStyles.RoundtripKind) // ISO 8601 형식 파싱
            };
        }
        catch (KeyNotFoundException ex)
        {
            UnityEngine.Debug.LogError($"[GameProgressSerializer] Missing key in data map during deserialization: {ex.Message}");
            return null;
        }
        catch (InvalidCastException ex)
        {
            UnityEngine.Debug.LogError($"[GameProgressSerializer] Type cast error during deserialization: {ex.Message}");
            return null;
        }
        catch (FormatException ex)
        {
            UnityEngine.Debug.LogError($"[GameProgressSerializer] Date time format error during deserialization: {ex.Message}");
            return null;
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogError($"[GameProgressSerializer] Unexpected error during deserialization: {ex.Message}");
            return null;
        }
    }

    public string GetTableName() => TABLE_NAME;
    public string GetPrimaryKeyColumnName() => PRIMARY_KEY_COLUMN;
    public object GetPrimaryKeyDefaultValue() => PRIMARY_KEY_DEFAULT_VALUE;
}
// --- END OF FILE GameProgressSerializer.cs ---
}