// C:\Workspace\Tomorrow Never Comes\Assets\Scripts\Core\Data\Impl\GameProgressRepository.cs (REFACTORED)

using Core.Data.Interface;
using Core.Logging;
using Features.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
// using Core.Util; // GameProgressSerializer의 실제 네임스페이스를 확인하고 필요에 따라 유지 또는 제거

namespace Core.Data.Impl
{
    /// <summary>
    /// GameProgressData에 대한 데이터베이스 CRUD 작업을 처리하는 리포지토리 구현체입니다.
    /// IGameProgressRepository 인터페이스를 구현하며, IDatabaseAccess와 IDataSerializer를 사용합니다.
    /// </summary>
    public class GameProgressRepository : IGameProgressRepository
    {
        private readonly IDatabaseAccess _dbAccess;
        private readonly IDataSerializer<GameProgressData> _serializer;

        /// <summary>
        /// GameProgressRepository의 새 인스턴스를 초기화합니다.
        /// </summary>
        /// <param name="dbAccess">데이터베이스 접근을 위한 IDatabaseAccess 인스턴스.</param>
        /// <param name="serializer">GameProgressData 객체를 직렬화/역직렬화하기 위한 IDataSerializer 인스턴스.</param>
        public GameProgressRepository(IDatabaseAccess dbAccess, IDataSerializer<GameProgressData> serializer)
        {
            _dbAccess = dbAccess ?? throw new ArgumentNullException(nameof(dbAccess));
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));

            if (_serializer.GetTableName() != "GameProgress")
            {
                CoreLogger.LogWarning($"[GameProgressRepository] Configured serializer's table name is '{_serializer.GetTableName()}' but expected 'GameProgress'. This might indicate a misconfiguration.");
            }
            CoreLogger.Log("[GameProgressRepository] Initialized.");
        }

        public async Task<GameProgressData> LoadGameProgressAsync(int saveSlotId)
        {
            CoreLogger.Log($"[GameProgressRepository] Loading GameProgressData for SaveSlotID: {saveSlotId}");

            // DatabaseAccess의 SelectWhereAsync는 이미 Task.Run을 내부적으로 사용하여 백그라운드 스레드에서 실행됩니다.
            var dataMaps = await _dbAccess.SelectWhereAsync(
                _serializer.GetTableName(),
                new string[] { _serializer.GetPrimaryKeyColumnName() },
                new string[] { "=" },
                new object[] { saveSlotId }
            );

            if (dataMaps == null || !dataMaps.Any())
            {
                CoreLogger.LogWarning($"[GameProgressRepository] No GameProgressData found for SaveSlotID: {saveSlotId}");
                return null;
            }

            return _serializer.Deserialize(dataMaps.First());
        }

        public async Task SaveGameProgressAsync(GameProgressData data)
        {
            if (data == null)
            {
                CoreLogger.LogError("[GameProgressRepository] Attempted to save null GameProgressData.");
                throw new ArgumentNullException(nameof(data));
            }

            CoreLogger.Log($"[GameProgressRepository] Saving GameProgressData for SaveSlotID: {data.SaveSlotID}");

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
                CoreLogger.Log($"[GameProgressRepository] Updated GameProgressData for SaveSlotID: {primaryKeyValue}");
            }
            else
            {
                // DatabaseAccess의 InsertIntoAsync는 이미 Task.Run을 내부적으로 사용하여 백그라운드 스레드에서 실행됩니다.
                await _dbAccess.InsertIntoAsync(
                    tableName,
                    dataMap.Keys.ToArray(),
                    dataMap.Values.ToArray()
                );
                CoreLogger.Log($"[GameProgressRepository] Inserted new GameProgressData for SaveSlotID: {primaryKeyValue}");
            }
        }

        public async Task DeleteGameProgressAsync(int saveSlotId)
        {
            CoreLogger.Log($"[GameProgressRepository] Deleting GameProgressData for SaveSlotID: {saveSlotId}");
            // DatabaseAccess의 DeleteWhereAsync는 이미 Task.Run을 내부적으로 사용하여 백그라운드 스레드에서 실행됩니다.
            await _dbAccess.DeleteWhereAsync(
                _serializer.GetTableName(),
                _serializer.GetPrimaryKeyColumnName(),
                saveSlotId
            );
        }

        public async Task<bool> HasGameProgressDataAsync(int saveSlotId) // 메서드 이름 변경 및 async 추가
        {
            CoreLogger.Log($"[GameProgressRepository] Checking for GameProgressData for SaveSlotID: {saveSlotId}");
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