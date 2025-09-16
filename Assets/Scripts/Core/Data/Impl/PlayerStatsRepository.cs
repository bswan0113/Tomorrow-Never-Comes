// C:\Workspace\Tomorrow Never Comes\Assets\Scripts\Core\Data\Impl\PlayerStatsRepository.cs (REFACTORED)

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

            // DatabaseAccess의 SelectWhereAsync는 이미 Task.Run을 내부적으로 사용하여 백그라운드 스레드에서 실행됩니다.
            var dataMaps = await _dbAccess.SelectWhereAsync(
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
        }

        public async Task SavePlayerStatsAsync(PlayerStatsData data)
        {
            if (data == null)
            {
                CoreLogger.LogError("[PlayerStatsRepository] Attempted to save null PlayerStatsData.");
                throw new ArgumentNullException(nameof(data));
            }

            CoreLogger.Log($"[PlayerStatsRepository] Saving PlayerStatsData for SaveSlotID: {data.SaveSlotID}");

            var dataMap = _serializer.Serialize(data);
            string tableName = _serializer.GetTableName();
            string primaryKeyCol = _serializer.GetPrimaryKeyColumnName();
            object primaryKeyValue = data.SaveSlotID;

            // DatabaseAccess의 SelectWhereAsync는 이미 Task.Run을 내부적으로 사용하여 백그라운드 스레드에서 실행됩니다.
            var existingData = await _dbAccess.SelectWhereAsync(
                tableName,
                new string[] { primaryKeyCol },
                new string[] { "=" },
                new object[] { primaryKeyValue }
            );

            if (existingData != null && existingData.Any()) // 기존 데이터 존재 여부 확인
            {
                // DatabaseAccess의 UpdateSetAsync는 이미 Task.Run을 내부적으로 사용하여 백그라운드 스레드에서 실행됩니다.
                await _dbAccess.UpdateSetAsync(
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
                // DatabaseAccess의 InsertIntoAsync는 이미 Task.Run을 내부적으로 사용하여 백그라운드 스레드에서 실행됩니다.
                await _dbAccess.InsertIntoAsync(
                    tableName,
                    dataMap.Keys.ToArray(),
                    dataMap.Values.ToArray()
                );
                CoreLogger.Log($"[PlayerStatsRepository] Inserted new PlayerStatsData for SaveSlotID: {primaryKeyValue}");
            }
        }

        public async Task DeletePlayerStatsAsync(int saveSlotId)
        {
            CoreLogger.Log($"[PlayerStatsRepository] Deleting PlayerStatsData for SaveSlotID: {saveSlotId}");
            // DatabaseAccess의 DeleteWhereAsync는 이미 Task.Run을 내부적으로 사용하여 백그라운드 스레드에서 실행됩니다.
            await _dbAccess.DeleteWhereAsync(
                _serializer.GetTableName(),
                _serializer.GetPrimaryKeyColumnName(),
                saveSlotId
            );
        }

        public async Task<bool> HasPlayerStatsDataAsync(int saveSlotId) // 메서드 이름 변경 및 async 추가
        {
            CoreLogger.Log($"[PlayerStatsRepository] Checking for PlayerStatsData for SaveSlotID: {saveSlotId}");
            // DatabaseAccess의 SelectWhereAsync는 이미 Task.Run을 내부적으로 사용하여 백그라운드 스레드에서 실행됩니다.
            var dataMaps = await _dbAccess.SelectWhereAsync(
                _serializer.GetTableName(),
                new string[] { _serializer.GetPrimaryKeyColumnName() },
                new string[] { "=" },
                new object[] { saveSlotId }
            );
            return dataMaps != null && dataMaps.Any();
        }
    }
}