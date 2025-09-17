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

            // Load 작업은 트랜잭션 컨텍스트 내에서 수행될 필요가 없을 수 있으므로, 현재 시그니처 유지.
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

        /// <summary>
        /// GameProgressData를 비동기적으로 저장합니다. 기존 데이터가 있으면 업데이트하고 없으면 삽입합니다.
        /// 제공된 트랜잭션 내에서 작업을 수행합니다.
        /// </summary>
        /// <param name="data">저장할 GameProgressData 객체.</param>
        /// <param name="transaction">현재 저장 작업을 포함할 트랜잭션 객체.</param>
        public async Task SaveGameProgressAsync(GameProgressData data, ITransaction transaction) // 트랜잭션 인자 추가
        {
            if (data == null)
            {
                CoreLogger.LogError("[GameProgressRepository] Attempted to save null GameProgressData.");
                throw new ArgumentNullException(nameof(data));
            }

            if (transaction == null) // 트랜잭션 인자가 필수가 되었으므로 null 체크 추가
            {
                CoreLogger.LogError("[GameProgressRepository] Transaction object is required to save GameProgressData.");
                throw new ArgumentNullException(nameof(transaction), "Save operation requires an ITransaction object.");
            }

            CoreLogger.Log($"[GameProgressRepository] Saving GameProgressData for SaveSlotID: {data.SaveSlotID} within a transaction.");

            var dataMap = _serializer.Serialize(data);
            string tableName = _serializer.GetTableName();
            string primaryKeyCol = _serializer.GetPrimaryKeyColumnName();
            object primaryKeyValue = data.SaveSlotID;

            // Save 작업은 트랜잭션 내에서 이루어져야 하므로, 트랜잭션이 넘어온 경우 Select도 트랜잭션 컨텍스트를 활용하도록 할 수 있습니다.
            // 현재 IDatabaseAccess.SelectWhereAsync는 트랜잭션 인자를 받지 않지만, 필요에 따라 확장될 수 있습니다.
            var existingData = await _dbAccess.SelectWhereAsync( // NOTE: SelectWhereAsync도 트랜잭션을 인자로 받도록 수정될 수 있습니다.
                tableName,
                new string[] { primaryKeyCol },
                new string[] { "=" },
                new object[] { primaryKeyValue }
            );

            if (existingData != null && existingData.Any()) // 기존 데이터 존재 여부 확인
            {
                // NOTE: IDatabaseAccess.UpdateSetAsync 메서드 시그니처가 트랜잭션 인자를 받도록 수정되어야 합니다.
                // 현재는 컴파일 오류가 발생할 수 있습니다.
                await _dbAccess.UpdateSetAsync(
                    tableName,
                    dataMap.Keys.ToArray(),
                    dataMap.Values.ToArray(),
                    primaryKeyCol,
                    primaryKeyValue,
                    transaction // 트랜잭션 인자 추가
                );
                CoreLogger.Log($"[GameProgressRepository] Updated GameProgressData for SaveSlotID: {primaryKeyValue} within transaction.");
            }
            else
            {
                // NOTE: IDatabaseAccess.InsertIntoAsync 메서드 시그니처가 트랜잭션 인자를 받도록 수정되어야 합니다.
                // 현재는 컴파일 오류가 발생할 수 있습니다.
                await _dbAccess.InsertIntoAsync(
                    tableName,
                    dataMap.Keys.ToArray(),
                    dataMap.Values.ToArray(),
                    transaction // 트랜잭션 인자 추가
                );
                CoreLogger.Log($"[GameProgressRepository] Inserted new GameProgressData for SaveSlotID: {primaryKeyValue} within transaction.");
            }
        }

        public async Task DeleteGameProgressAsync(int saveSlotId)
        {
            CoreLogger.Log($"[GameProgressRepository] Deleting GameProgressData for SaveSlotID: {saveSlotId}");
            // Delete 작업도 트랜잭션의 일부가 될 수 있으나, 현재 인터페이스에서는 명시적으로 트랜잭션을 받지 않습니다.
            // 필요에 따라 IGameProgressRepository.DeleteGameProgressAsync도 트랜잭션 인자를 받도록 수정될 수 있습니다.
            await _dbAccess.DeleteWhereAsync(
                _serializer.GetTableName(),
                _serializer.GetPrimaryKeyColumnName(),
                saveSlotId
            );
        }

        public async Task<bool> HasGameProgressDataAsync(int saveSlotId)
        {
            CoreLogger.Log($"[GameProgressRepository] Checking for GameProgressData for SaveSlotID: {saveSlotId}");
            // HasData 확인은 트랜잭션 컨텍스트가 필요 없을 수 있습니다.
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