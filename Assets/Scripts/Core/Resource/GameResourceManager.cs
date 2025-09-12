// Scripts/UI/Manager/GameResourceManager.cs

using System.Collections.Generic;
using System.Linq;
using Core.Interface;
using Core.Logging;
using UnityEngine;

namespace Core.Resource
{
    // IGameResourceService 인터페이스 구현 추가
    public class GameResourceManager : MonoBehaviour, IGameResourceService
    {

        private Dictionary<string, GameData> gameDatabase;


        // 컴포지션 루트에서 호출될 초기화 메서드
        public void Initialize()
        {
            LoadAllGameData();
            CoreLogger.Log("GameResourceManager Initialized and Data Loaded.");
        }

        private void LoadAllGameData()
        {
            var allContainers = Resources.LoadAll<DataImportContainer>("");

            var allData = new List<GameData>();
            foreach (var container in allContainers)
            {
                foreach (var obj in container.importedObjects)
                {
                    if (obj is GameData gameData)
                    {
                        allData.Add(gameData);
                    }
                }
            }

            var duplicates = allData.GroupBy(data => data.id)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key);

            if (duplicates.Any())
            {
                foreach (var duplicateId in duplicates)
                {
                    CoreLogger.LogError($"[GameResourceManager] 중복된 ID({duplicateId})가 존재합니다! CSV 파일을 확인해주세요.");
                }
            }

            gameDatabase = allData.ToDictionary(data => data.id, data => data);
            CoreLogger.Log($"<color=cyan>{gameDatabase.Count}개의 게임 데이터를 로드했습니다.</color>");
        }

        /// <summary>
        /// ID와 타입(T)을 이용해 게임 데이터를 가져옵니다. (IGameResourceService 인터페이스에 추가 필요)
        /// </summary>
        public T GetDataByID<T>(string id) where T : GameData
        {
            if (gameDatabase == null)
            {
                CoreLogger.LogError("GameResourceManager: gameDatabase가 초기화되지 않았습니다. Initialize()를 먼저 호출해주세요.");
                return null;
            }

            if (gameDatabase.TryGetValue(id, out GameData data))
            {
                if (data is T requestedData)
                {
                    return requestedData;
                }
                else
                {
                    CoreLogger.LogWarning($"ID '{id}'의 데이터는 존재하지만, 요청한 타입({typeof(T)})이 아닙니다. 실제 타입: {data.GetType()}");
                    return null;
                }
            }

            CoreLogger.LogWarning($"요청한 ID '{id}'를 가진 데이터를 찾을 수 없습니다!");
            return null;
        }

        // IGameResourceService 인터페이스 메서드 구현
        public T[] GetAllDataOfType<T>() where T : GameData
        {
            if (gameDatabase == null)
            {
                CoreLogger.LogError("GameResourceManager: gameDatabase가 초기화되지 않았습니다. Initialize()를 먼저 호출해주세요.");
                return new T[0]; // 빈 배열 반환
            }
            return gameDatabase.Values.OfType<T>().ToArray(); // List에서 Array로 반환 타입 변경 (인터페이스에 따라)
        }
    }
}