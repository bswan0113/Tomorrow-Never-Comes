using UnityEngine;
using UnityEditor;
using System.Collections;
using System.Linq;
using System.Reflection;
using Core.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using ScriptableObjects.Abstract;
using ScriptableObjects.Util; // Type.GetType을 위해 추가

public class ReferenceResolverPostprocessor : AssetPostprocessor
{
    static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
    {
        // .tncd 파일이 하나라도 임포트 되었는지 확인합니다.
        // 이 검사를 통해 관련 없는 에셋 임포트 시에는 불필요한 작업을 하지 않습니다.
        bool tncdFileImported = false;
        foreach (string path in importedAssets)
        {
            if (path.EndsWith(".tncd"))
            {
                tncdFileImported = true;
                break;
            }
        }

        if (!tncdFileImported)
        {
            return;
        }

        CoreLogger.Log("<color=orange>--- Starting GameData reference resolving process ---</color>");

        // --- GameData 에셋 캐시 생성 ---
        // Postprocessor 실행 시점에는 모든 새로운 .tncd 파일이 이미 임포트되어 있으므로,
        // 이 시점에서 프로젝트 내의 모든 GameData 에셋을 스캔하여 캐시를 만듭니다.
        // 이는 임포트 직후의 최신 상태를 반영하며, 스태일 데이터 위험을 줄입니다.
        var gameDataCache = new Dictionary<string, GameData>();
        string[] allGameDataGuids = AssetDatabase.FindAssets("t:GameData"); // 모든 GameData SO를 찾습니다.

        foreach (string guid in allGameDataGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            UnityEngine.Object[] assetsInPath = AssetDatabase.LoadAllAssetsAtPath(path); // 해당 경로의 모든 서브 에셋 로드

            foreach (var asset in assetsInPath)
            {
                if (asset is GameData gameData && !string.IsNullOrEmpty(gameData.id))
                {
                    if (!gameDataCache.ContainsKey(gameData.id))
                    {
                        gameDataCache.Add(gameData.id, gameData);
                    }
                    else
                    {
                        CoreLogger.LogWarning($"Duplicate GameData ID found: '{gameData.id}' at path '{path}'. Only the first instance will be used for reference resolution.", gameData);
                    }
                }
            }
        }
        CoreLogger.Log($"<color=orange>GameData cache built with {gameDataCache.Count} unique entries.</color>");
        // --- 캐시 생성 로직 종료 ---

        bool needsReSave = false;
        List<DataImportContainer> containersToClean = new List<DataImportContainer>();

        foreach (string path in importedAssets)
        {
            if (path.EndsWith(".tncd"))
            {
                var allAssetsInFile = AssetDatabase.LoadAllAssetsAtPath(path);
                var container = allAssetsInFile.OfType<DataImportContainer>().FirstOrDefault();

                if (container == null || !container.pendingReferences.Any())
                {
                    CoreLogger.Log($"No pending references found for {Path.GetFileName(path)}.");
                    continue;
                }

                CoreLogger.Log($"<color=yellow>Resolving {container.pendingReferences.Count} pending references for: {Path.GetFileName(path)}</color>");

                foreach (var pending in container.pendingReferences)
                {
                    if (pending.targetObject == null)
                    {
                        CoreLogger.LogWarning($"Pending reference target object is null (was it deleted?). Skipping.", container);
                        continue;
                    }

                    FieldInfo field = pending.targetObject.GetType().GetField(pending.fieldName, BindingFlags.Public | BindingFlags.Instance);
                    if (field == null)
                    {
                        CoreLogger.LogWarning($"Field '{pending.fieldName}' not found on target object '{pending.targetObject.name}'. Skipping reference resolution.", pending.targetObject);
                        continue;
                    }

                    Type fieldType = field.FieldType;
                    Type itemType = null; // 리스트/배열 아이템 타입 또는 단일 GameData 타입

                    if (pending.isList)
                    {
                        if (!typeof(IList).IsAssignableFrom(fieldType))
                        {
                            CoreLogger.LogWarning($"Pending reference for field '{field.Name}' on '{pending.targetObject.name}' is marked as a list, but the field type is not IList. Skipping.", pending.targetObject);
                            continue;
                        }
                        itemType = fieldType.IsArray ? fieldType.GetElementType() : fieldType.GetGenericArguments()[0];
                    }
                    else // 단일 GameData 참조
                    {
                        if (!typeof(GameData).IsAssignableFrom(fieldType))
                        {
                             CoreLogger.LogWarning($"Pending reference for field '{field.Name}' on '{pending.targetObject.name}' is marked as single, but the field type is not GameData. Skipping.", pending.targetObject);
                             continue;
                        }
                        itemType = fieldType; // 단일 참조의 경우 필드 타입 자체가 아이템 타입
                    }

                    if (itemType == null)
                    {
                         CoreLogger.LogError($"Failed to determine item type for field '{field.Name}' on '{pending.targetObject.name}'. Skipping.", pending.targetObject);
                         continue;
                    }

                    if (pending.isList)
                    {
                        // 리스트 참조 처리
                        var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(itemType));

                        foreach (string id in pending.requiredIds)
                        {
                            if (gameDataCache.TryGetValue(id, out GameData data))
                            {
                                if (itemType.IsAssignableFrom(data.GetType()))
                                {
                                    list.Add(data);
                                    CoreLogger.Log($"  <color=lime>Resolved</color> list reference for '{pending.targetObject.name}.{field.Name}': added '{data.id}'.");
                                }
                                else
                                {
                                    CoreLogger.LogWarning($"Type mismatch for list item '{id}'. Expected '{itemType.Name}', got '{data.GetType().Name}'. Adding null instead.", pending.targetObject);
                                    list.Add(null);
                                }
                            }
                            else
                            {
                                CoreLogger.LogWarning($"Could not find GameData with ID '{id}' to link in '{pending.targetObject.name}.{field.Name}'. Adding null instead.", pending.targetObject);
                                list.Add(null);
                            }
                        }

                        // 필드가 배열인 경우 List를 배열로 변환
                        if (fieldType.IsArray)
                        {
                            Array array = Array.CreateInstance(itemType, list.Count);
                            list.CopyTo(array, 0);
                            field.SetValue(pending.targetObject, array);
                        }
                        else
                        {
                            field.SetValue(pending.targetObject, list);
                        }
                    }
                    else // 단일 참조 처리
                    {
                        string id = pending.requiredIds.FirstOrDefault();
                        if (id != null && gameDataCache.TryGetValue(id, out GameData data))
                        {
                            if (itemType.IsAssignableFrom(data.GetType()))
                            {
                                field.SetValue(pending.targetObject, data);
                                CoreLogger.Log($"  <color=lime>Resolved</color> single reference for '{pending.targetObject.name}.{field.Name}': linked to '{data.id}'.");
                            }
                            else
                            {
                                CoreLogger.LogWarning($"Type mismatch for single GameData '{id}'. Expected '{itemType.Name}', got '{data.GetType().Name}'. Setting null instead.", pending.targetObject);
                                field.SetValue(pending.targetObject, null);
                            }
                        }
                        else
                        {
                            CoreLogger.LogWarning($"Could not find single GameData with ID '{id}' to link in '{pending.targetObject.name}.{field.Name}'. Setting null instead.", pending.targetObject);
                            field.SetValue(pending.targetObject, null);
                        }
                    }

                    EditorUtility.SetDirty(pending.targetObject);
                    needsReSave = true;
                }

                // 모든 참조 해결 후 pendingReferences 목록을 비웁니다.
                containersToClean.Add(container);
            }
        }

        foreach(var container in containersToClean)
        {
            if (container.pendingReferences.Any())
            {
                CoreLogger.Log($"Clearing {container.pendingReferences.Count} pending references from container '{container.name}'.");
                container.pendingReferences.Clear();
                EditorUtility.SetDirty(container);
                needsReSave = true;
            }
        }

        if (needsReSave)
        {
            AssetDatabase.SaveAssets();
            CoreLogger.Log("<color=green>--- All GameData references resolved and assets saved. ---</color>");
        }
        else
        {
            CoreLogger.Log("<color=green>--- No GameData references required resolution or no changes were made. ---</color>");
        }
    }
}