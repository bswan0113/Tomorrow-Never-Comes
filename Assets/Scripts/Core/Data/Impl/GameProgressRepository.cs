// C:\Workspace\Tomorrow Never Comes\Assets\Scripts\Core\Data\Impl\GameProgressRepository.cs

using Core.Data.Interface;
using Core.Logging;
using Features.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Core.Util; // GameProgressSerializer가 이 네임스페이스에 있음

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
                    CoreLogger.LogWarning($"[GameProgressRepository] No GameProgressData found for SaveSlotID: {saveSlotId}");
                    return null;
                }

                return _serializer.Deserialize(dataMaps.First());
            });
        }

        public async Task SaveGameProgressAsync(GameProgressData data)
        {
            if (data == null)
            {
                CoreLogger.LogError("[GameProgressRepository] Attempted to save null GameProgressData.");
                throw new ArgumentNullException(nameof(data));
            }

            CoreLogger.Log($"[GameProgressRepository] Saving GameProgressData for SaveSlotID: {data.SaveSlotID}");
            await Task.Run(() =>
            {
                var dataMap = _serializer.Serialize(data);
                string tableName = _serializer.GetTableName();
                string primaryKeyCol = _serializer.GetPrimaryKeyColumnName();
                object primaryKeyValue = data.SaveSlotID; // GameProgressData 객체에서 직접 SaveSlotID를 가져옴

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
                    CoreLogger.Log($"[GameProgressRepository] Updated GameProgressData for SaveSlotID: {primaryKeyValue}");
                }
                else
                {
                    _dbAccess.InsertInto(
                        tableName,
                        dataMap.Keys.ToArray(),
                        dataMap.Values.ToArray()
                    );
                    CoreLogger.Log($"[GameProgressRepository] Inserted new GameProgressData for SaveSlotID: {primaryKeyValue}");
                }
            });
        }

        public async Task DeleteGameProgressAsync(int saveSlotId)
        {
            CoreLogger.Log($"[GameProgressRepository] Deleting GameProgressData for SaveSlotID: {saveSlotId}");
            await Task.Run(() =>
            {
                _dbAccess.DeleteWhere(
                    _serializer.GetTableName(),
                    _serializer.GetPrimaryKeyColumnName(),
                    saveSlotId
                );
            });
        }

        public bool HasGameProgressData(int saveSlotId)
        {
            CoreLogger.Log($"[GameProgressRepository] Checking for GameProgressData for SaveSlotID: {saveSlotId}");
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