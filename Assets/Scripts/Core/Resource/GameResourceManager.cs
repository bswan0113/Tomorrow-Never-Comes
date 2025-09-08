// Scripts/UI/Manager/GameResourceManager.cs
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace Manager // 주인님의 패키징 구조에 맞춰 네임스페이스를 사용합니다.
{
    public class GameResourceManager : MonoBehaviour
    {
        public static GameResourceManager Instance { get; private set; }

        // 모든 GameData를 id를 키로 하여 저장하는 단일 데이터베이스
        private Dictionary<string, GameData> gameDatabase;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this.gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(this.gameObject);

            LoadAllGameData();
            Debug.Log("GameResourceManager Instance Created");
        }

        private void LoadAllGameData()
        {
            // 1. Resources 폴더 하위의 모든 GameData 상속 에셋을 불러옵니다.
            var allData = Resources.LoadAll<GameData>(""); // 빈 문자열은 Resources 폴더 전체를 의미

            // 2. 중복 ID가 있는지 검사하고, 있다면 경고를 출력합니다.
            var duplicates = allData.GroupBy(data => data.id)
                                    .Where(group => group.Count() > 1)
                                    .Select(group => group.Key);

            if (duplicates.Any())
            {
                foreach (var duplicateId in duplicates)
                {
                    Debug.LogError($"[GameResourceManager] 중복된 ID({duplicateId})가 존재합니다! SO 에셋을 확인해주세요.");
                }
            }

            // 3. ID를 Key로 하여 Dictionary에 저장합니다.
            gameDatabase = allData.ToDictionary(data => data.id, data => data);
            Debug.Log($"<color=cyan>{gameDatabase.Count}개의 게임 데이터를 로드했습니다.</color>");
        }

        /// <summary>
        /// ID와 타입(T)을 이용해 게임 데이터를 가져옵니다.
        /// </summary>
        /// <typeparam name="T">가져올 데이터의 타입 (SpellData, MagicBookData 등)</typeparam>
        /// <param name="id">찾고자 하는 데이터의 ID</param>
        /// <returns>요청한 타입의 데이터. 없으면 null을 반환합니다.</returns>
        public T GetDataByID<T>(string id) where T : GameData
        {
            if (gameDatabase.TryGetValue(id, out GameData data))
            {
                if (data is T requestedData)
                {
                    return requestedData;
                }
                else
                {
                    Debug.LogWarning($"ID '{id}'의 데이터는 존재하지만, 요청한 타입({typeof(T)})이 아닙니다. 실제 타입: {data.GetType()}");
                    return null;
                }
            }

            Debug.LogWarning($"요청한 ID '{id}'를 가진 데이터를 찾을 수 없습니다!");
            return null;
        }
        public List<T> GetAllDataOfType<T>() where T : GameData
        {
            return gameDatabase.Values.OfType<T>().ToList();
        }
        void OnDestroy()
        {
            Debug.Log("Destory");
        }
    }




}