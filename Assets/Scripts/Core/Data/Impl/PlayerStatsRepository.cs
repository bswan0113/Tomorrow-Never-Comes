// C:\Workspace\Tomorrow Never Comes\Assets\Scripts\Core\Data\Impl\PlayerStatsRepository.cs

using Core.Data.Interface;
using Core.Logging;
using Features.Player;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Core.Data.Impl
{
    /// <summary>
    /// PlayerStatsData에 대한 데이터베이스 CRUD 작업을 처리하는 리포지토리 구현체입니다.
    /// IPlayerStatsRepository 인터페이스를 구현하며, IDatabaseAccess와 IDataSerializer를 사용합니다.
    /// </summary>
    public class PlayerStatsRepository : IPlayerStatsRepository
    {
        private readonly IDatabaseAccess _dbAccess;
        private readonly IDataSerializer<PlayerStatsData> _serializer;

        /// <summary>
        /// PlayerStatsRepository의 새 인스턴스를 초기화합니다.
        /// </summary>
        /// <param name="dbAccess">데이터베이스 접근을 위한 IDatabaseAccess 인스턴스.</param>
        /// <param name="serializer">PlayerStatsData 객체를 직렬화/역직렬화하기 위한 IDataSerializer 인스턴스.</param>
        public PlayerStatsRepository(IDatabaseAccess dbAccess, IDataSerializer<PlayerStatsData> serializer)
        {
            _dbAccess = dbAccess ?? throw new ArgumentNullException(nameof(dbAccess));
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));

            if (_serializer.GetTableName() != "PlayerStats")
            {
                CoreLogger.LogWarning($"[PlayerStatsRepository] Configured serializer's table name is '{_serializer.GetTableName()}' but expected 'PlayerStats'. This might indicate a misconfiguration.");
            }
            CoreLogger.Log("[PlayerStatsRepository] Initialized.");
        }

        public async Task<PlayerStatsData> LoadPlayerStatsAsync(int saveSlotId)
        {
            CoreLogger.Log($"[PlayerStatsRepository] Loading PlayerStatsData for SaveSlotID: {saveSlotId}");
            return await Task.Run(() =>
            {
                // P20: await Task.Run()을 사용하여 모든 데이터베이스 작업을 백그라운드 스레드에서 실행
                var dataMaps = _dbAccess.SelectWhere(
                    _serializer.GetTableName(),
                    new string[] { _serializer.GetPrimaryKeyColumnName() },
                    new string[] { "=" },
                    new object[] { saveSlotId }
                );

                if (dataMaps == null || !dataMaps.Any())
                {
                    CoreLogger.LogWarning($"[PlayerStatsRepository] No PlayerStatsData found for SaveSlotID: {saveSlotId}");
                    return null;
                }

                return _serializer.Deserialize(dataMaps.First());
            });
        }

        public async Task SavePlayerStatsAsync(PlayerStatsData data)
        {
            if (data == null)
            {
                CoreLogger.LogError("[PlayerStatsRepository] Attempted to save null PlayerStatsData.");
                throw new ArgumentNullException(nameof(data));
            }

            CoreLogger.Log($"[PlayerStatsRepository] Saving PlayerStatsData for SaveSlotID: {data.SaveSlotID}");
            await Task.Run(() =>
            {
                var dataMap = _serializer.Serialize(data);
                string tableName = _serializer.GetTableName();
                string primaryKeyCol = _serializer.GetPrimaryKeyColumnName();
                object primaryKeyValue = data.SaveSlotID; // PlayerStatsData 객체에서 직접 SaveSlotID를 가져옴

                var existingData = _dbAccess.SelectWhere(
                    tableName,
                    new string[] { primaryKeyCol },
                    new string[] { "=" },
                    new object[] { primaryKeyValue }
                );

                if (existingData != null && existingData.Count > 0)
                {
                    _dbAccess.UpdateSet(
                        tableName,
                        dataMap.Keys.ToArray(),
                        dataMap.Values.ToArray(),
                        primaryKeyCol,
                        primaryKeyValue
                    );
                    CoreLogger.Log($"[PlayerStatsRepository] Updated PlayerStatsData for SaveSlotID: {primaryKeyValue}");
                }
                else
                {
                    _dbAccess.InsertInto(
                        tableName,
                        dataMap.Keys.ToArray(),
                        dataMap.Values.ToArray()
                    );
                    CoreLogger.Log($"[PlayerStatsRepository] Inserted new PlayerStatsData for SaveSlotID: {primaryKeyValue}");
                }
            });
        }

        public async Task DeletePlayerStatsAsync(int saveSlotId)
        {
            CoreLogger.Log($"[PlayerStatsRepository] Deleting PlayerStatsData for SaveSlotID: {saveSlotId}");
            await Task.Run(() =>
            {
                _dbAccess.DeleteWhere(
                    _serializer.GetTableName(),
                    _serializer.GetPrimaryKeyColumnName(),
                    saveSlotId
                );
            });
        }

        public bool HasPlayerStatsData(int saveSlotId)
        {
            CoreLogger.Log($"[PlayerStatsRepository] Checking for PlayerStatsData for SaveSlotID: {saveSlotId}");
            // 이 메서드는 빠르게 저장 데이터 유무만 확인하므로,
            // 비동기 오버헤드 없이 동기적으로 실행합니다.
            var dataMaps = _dbAccess.SelectWhere(
                _serializer.GetTableName(),
                new string[] { _serializer.GetPrimaryKeyColumnName() },
                new string[] { "=" },
                new object[] { saveSlotId }
            );
            return dataMaps != null && dataMaps.Any();
        }
    }
}