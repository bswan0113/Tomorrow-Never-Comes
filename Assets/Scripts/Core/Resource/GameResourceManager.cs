// C:\Workspace\Tomorrow Never Comes\Assets\Scripts\Core\Resource\GameResourceManager.cs

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks; // Task 사용을 위해 추가
using Core.Interface;
using Core.Logging;
using ScriptableObjects.Abstract;
using ScriptableObjects.Util;
using UnityEngine;
using UnityEngine.AddressableAssets; // Addressables 사용을 위해 추가
using UnityEngine.ResourceManagement.AsyncOperations; // AsyncOperationHandle을 위해 추가

namespace Core.Resource
{
    // IGameResourceService 인터페이스 구현 추가 (현재 인터페이스는 변경이 필요 없음)
    public class GameResourceManager :  IGameResourceService, IDisposable
    {
        // Addressables를 통해 로드된 데이터를 관리할 딕셔너리
        private Dictionary<string, GameData> _gameDatabase;
        // Addressables 핸들을 추적하여 리소스 해제를 관리
        private List<AsyncOperationHandle> _loadedHandles;

        // 컴포지션 루트에서 호출될 비동기 초기화 메서드
        public async Task InitializeAsync() // 메서드 이름을 InitializeAsync로 변경, Task 반환
        {
            _gameDatabase = new Dictionary<string, GameData>();
            _loadedHandles = new List<AsyncOperationHandle>();

            CoreLogger.LogInfo("[GameResourceManager] 초기화 시작. Addressables 데이터 로드 중...");

            // --- P1: 로딩 스파이크 (선로딩 부재) / 리소스 메모리/로딩 비효율 해결 ---
            // Addressables를 사용하여 모든 DataImportContainer를 비동기적으로 로드
            // Addressables Group에 "DataImportContainer" 태그를 추가하여 모든 컨테이너를 한 번에 로드하는 것이 이상적입니다.
            // 여기서는 모든 ScriptableObject를 로드하는 일반적인 Addressables 태그를 사용한다고 가정합니다.
            // 예: "GameData" 태그를 가진 모든 ScriptableObject 로드
            var loadContainersHandle = Addressables.LoadAssetsAsync<DataImportContainer>("DataImportContainersTag", null); // "DataImportContainersTag"는 예시
            _loadedHandles.Add(loadContainersHandle); // 핸들 추적

            await loadContainersHandle.Task; // 비동기 로딩 완료 대기

            if (loadContainersHandle.Status != AsyncOperationStatus.Succeeded)
            {
                CoreLogger.LogError($"[GameResourceManager] DataImportContainer 로드 실패! {loadContainersHandle.OperationException?.Message}");
                // 실패 시 초기화 중단
                return;
            }

            var allContainers = loadContainersHandle.Result;

            var allData = new List<GameData>();
            foreach (var container in allContainers)
            {
                if (container == null)
                {
                    CoreLogger.LogWarning("[GameResourceManager] 로드된 DataImportContainer 중 null 항목이 있습니다. 건너뜜.");
                    continue;
                }

                if (container.importedObjects == null)
                {
                    CoreLogger.LogWarning($"[GameResourceManager] DataImportContainer '{container.name}'에 importedObjects 목록이 null입니다. 건너뜜.");
                    continue;
                }

                foreach (var obj in container.importedObjects)
                {
                    if (obj == null)
                    {
                        CoreLogger.LogWarning($"[GameResourceManager] DataImportContainer '{container.name}'의 importedObjects 목록에 null 항목이 있습니다. 건너뜜.");
                        continue;
                    }
                    if (obj is GameData gameData)
                    {
                        allData.Add(gameData);
                    }
                    else
                    {
                        CoreLogger.LogWarning($"[GameResourceManager] DataImportContainer '{container.name}'에서 GameData 타입이 아닌 오브젝트 발견: {obj.name} (Type: {obj.GetType()})");
                    }
                }
            }

            // 중복 ID 검사 (기존 로직 유지)
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

            _gameDatabase = allData.ToDictionary(data => data.id, data => data);
            CoreLogger.LogInfo($"<color=cyan>{_gameDatabase.Count}개의 게임 데이터를 로드했습니다.</color>");
            CoreLogger.LogInfo("[GameResourceManager] 초기화 완료 및 데이터 로드 완료.");
        }

        /// <summary>
        /// ID와 타입(T)을 이용해 게임 데이터를 가져옵니다. (IGameResourceService 인터페이스에 추가 필요)
        /// </summary>
        public T GetDataByID<T>(string id) where T : GameData
        {
            if (_gameDatabase == null)
            {
                CoreLogger.LogError("GameResourceManager: _gameDatabase가 초기화되지 않았습니다. InitializeAsync()를 먼저 호출해주세요.");
                return null;
            }

            if (_gameDatabase.TryGetValue(id, out GameData data))
            {
                if (data is T requestedData)
                {
                    return requestedData;
                }
                else
                {
                    CoreLogger.LogWarning($"ID '{id}'의 데이터는 존재하지만, 요청한 타입({typeof(T).Name})이 아닙니다. 실제 타입: {data.GetType().Name}");
                    return null;
                }
            }

            CoreLogger.LogWarning($"요청한 ID '{id}'를 가진 데이터를 찾을 수 없습니다!");
            return null;
        }

        // IGameResourceService 인터페이스 메서드 구현
        public T[] GetAllDataOfType<T>() where T : GameData
        {
            if (_gameDatabase == null)
            {
                CoreLogger.LogError("GameResourceManager: _gameDatabase가 초기화되지 않았습니다. InitializeAsync()를 먼저 호출해주세요.");
                return new T[0];
            }
            return _gameDatabase.Values.OfType<T>().ToArray();
        }

        // --- P1: 리소스 메모리/로딩 비효율 해결을 위한 Addressables 리소스 해제 ---

        public void Dispose()
        {
            if (_loadedHandles == null) return;

            CoreLogger.LogInfo("[GameResourceManager] Dispose 호출. Addressables 리소스 해제 시도...");
            foreach (var handle in _loadedHandles)
            {
                if (handle.IsValid())
                {
                    Addressables.Release(handle);
                }
            }
            _loadedHandles.Clear();
            _gameDatabase?.Clear();
        }
    }
}