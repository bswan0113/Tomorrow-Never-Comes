// C:\Workspace\Tomorrow Never Comes\Assets\Scripts\Features\Player\PlayerStatsSerializer.cs

using Core.Data.Interface; // IDataSerializer 인터페이스를 사용하기 위해 필요
using Core.Logging;
using System;
using System.Collections.Generic;
// using Core.Interface; // 이 using은 Core.Data.Interface로 대체됨

namespace Features.Player
{
    /// <summary>
    /// PlayerStatsData 객체를 Dictionary 형태로 직렬화하고 역직렬화하는 클래스입니다.
    /// IDataSerializer 인터페이스를 구현하여 DataManager와 연동됩니다.
    /// </summary>
    public class PlayerStatsSerializer : IDataSerializer<PlayerStatsData>
    {
        private const string TABLE_NAME = "PlayerStats"; // 이 시리얼라이저가 다룰 테이블 이름
        private const string PRIMARY_KEY_COLUMN = "SaveSlotID"; // 주 키 컬럼 이름
        private const int PRIMARY_KEY_DEFAULT_VALUE = 1; // 주 키의 기본값

        public Dictionary<string, object> Serialize(PlayerStatsData data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data), "[PlayerStatsSerializer] Data to serialize cannot be null.");

            return new Dictionary<string, object>
            {
                { "SaveSlotID", data.SaveSlotID },
                { "Intellect", data.Intellect },
                { "Charm", data.Charm },
                { "Endurance", data.Endurance },
                { "Money", data.Money },
                { "HeroineALiked", data.HeroineALiked },
                { "HeroineBLiked", data.HeroineBLiked },
                { "HeroineCLiked", data.HeroineCLiked }
            };
        }

        public PlayerStatsData Deserialize(Dictionary<string, object> dataMap)
        {
            if (dataMap == null || dataMap.Count == 0)
            {
                CoreLogger.LogWarning("[PlayerStatsSerializer] Data map is null or empty, returning null PlayerStatsData.");
                return null;
            }

            try
            {
                return new PlayerStatsData
                {
                    SaveSlotID = Convert.ToInt32(dataMap[PRIMARY_KEY_COLUMN]),
                    Intellect = Convert.ToInt32(dataMap["Intellect"]),
                    Charm = Convert.ToInt32(dataMap["Charm"]),
                    Endurance = Convert.ToInt32(dataMap["Endurance"]),
                    Money = Convert.ToInt64(dataMap["Money"]), // Money는 long 타입이므로 Convert.ToInt64 사용
                    HeroineALiked = Convert.ToInt32(dataMap["HeroineALiked"]),
                    HeroineBLiked = Convert.ToInt32(dataMap["HeroineBLiked"]),
                    HeroineCLiked = Convert.ToInt32(dataMap["HeroineCLiked"])
                };
            }
            catch (KeyNotFoundException ex)
            {
                CoreLogger.LogError($"[PlayerStatsSerializer] Missing key in data map during deserialization: {ex.Message}");
                return null;
            }
            catch (InvalidCastException ex)
            {
                CoreLogger.LogError($"[PlayerStatsSerializer] Type cast error during deserialization: {ex.Message}");
                return null;
            }
            catch (Exception ex)
            {
                CoreLogger.LogError($"[PlayerStatsSerializer] Unexpected error during deserialization: {ex.Message}");
                return null;
            }
        }

        public string GetTableName() => TABLE_NAME;
        public string GetPrimaryKeyColumnName() => PRIMARY_KEY_COLUMN;
        public object GetPrimaryKeyDefaultValue() => PRIMARY_KEY_DEFAULT_VALUE;
    }
}