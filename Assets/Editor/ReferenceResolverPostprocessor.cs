using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Core.Logging;

public class ReferenceResolverPostprocessor : AssetPostprocessor
{
    static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
    {
        // .tncd 파일이 하나라도 임포트 되었는지 확인합니다.
        // 이 검사를 통해 관련 없는 에셋 임포트 시에는 불필요한 작업을 하지 않습니다.
        if (!importedAssets.Any(path => path.EndsWith(".tncd")))
        {
            return;
        }

        CoreLogger.Log("<color=orange>Starting reference resolving process because .tncd files were imported.</color>");

        // --- 올바르게 수정된 캐시 생성 로직 ---
        var gameDataCache = new Dictionary<string, GameData>();

        // 프로젝트 내의 모든 .tncd 임포터가 만든 에셋을 찾습니다.
        // t:DataImportContainer 검색은 이 컨테이너를 메인 에셋으로 사용하는 모든 파일을 찾아줍니다.
        string[] containerGuids = AssetDatabase.FindAssets("t:DataImportContainer");

        foreach (string guid in containerGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);

            // 파일 내의 '모든' 에셋 (메인+서브)을 불러옵니다.
            UnityEngine.Object[] allSubAssets = AssetDatabase.LoadAllAssetsAtPath(path);

            foreach (var asset in allSubAssets)
            {
                // 서브 에셋이 GameData 타입인지 확인합니다.
                if (asset is GameData gameData)
                {
                    if (!string.IsNullOrEmpty(gameData.id) && !gameDataCache.ContainsKey(gameData.id))
                    {
                        gameDataCache.Add(gameData.id, gameData);
                    }
                }
            }
        }
        CoreLogger.Log($"<color=orange>GameData cache built successfully with {gameDataCache.Count} entries.</color>");
        // --- 캐시 생성 로직 종료 ---


        bool needsReSave = false;

        foreach (string path in importedAssets)
        {
            if (path.EndsWith(".tncd"))
            {
                var allAssetsInFile = AssetDatabase.LoadAllAssetsAtPath(path);
                var container = allAssetsInFile.OfType<DataImportContainer>().FirstOrDefault();

                if (container == null || !container.pendingReferences.Any())
                {
                    continue;
                }

                CoreLogger.Log($"<color=yellow>Resolving {container.pendingReferences.Count} pending references for: {path}</color>");

                foreach (var pending in container.pendingReferences)
                {
                    if (pending.targetObject == null) continue;

                    FieldInfo field = pending.targetObject.GetType().GetField(pending.fieldName);
                    if (field == null) continue;

                    System.Type itemType = field.FieldType.GetGenericArguments()[0];
                    var list = (System.Collections.IList)System.Activator.CreateInstance(field.FieldType);

                    foreach (string id in pending.requiredIds)
                    {
                        if (gameDataCache.TryGetValue(id, out GameData data))
                        {
                            if (itemType.IsAssignableFrom(data.GetType()))
                            {
                                list.Add(data);
                            }
                        }
                        else
                        {
                            CoreLogger.LogWarning($"Could not find GameData with ID '{id}' to link in '{pending.targetObject.name}'.", pending.targetObject);
                            list.Add(null);
                        }
                    }

                    field.SetValue(pending.targetObject, list);
                    EditorUtility.SetDirty(pending.targetObject);
                    needsReSave = true;
                }

                container.pendingReferences.Clear();
                EditorUtility.SetDirty(container);
            }
        }

        if (needsReSave)
        {
            AssetDatabase.SaveAssets();
            CoreLogger.Log("<color=green>--- All references resolved and assets saved. ---</color>");
        }
    }
}