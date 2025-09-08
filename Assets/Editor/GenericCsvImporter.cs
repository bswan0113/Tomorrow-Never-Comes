using UnityEngine;
using UnityEditor;
using UnityEditor.AssetImporters;
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

[ScriptedImporter(1, "csv.txt")]
public class GenericCsvImporter : ScriptedImporter
{
    public string targetTypeAssemblyQualifiedName;
    private static Dictionary<string, GameData> allGameDataCache;

    [InitializeOnLoadMethod]
    private static void Initialize() { EditorApplication.delayCall += RefreshCache; }

    public static void RefreshCache()
    {
        allGameDataCache = new Dictionary<string, GameData>();
        string[] guids = AssetDatabase.FindAssets("t:GameData");

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
            foreach (var asset in assets)
            {
                if (asset is GameData gameData && !string.IsNullOrEmpty(gameData.id) && !allGameDataCache.ContainsKey(gameData.id))
                {
                    allGameDataCache.Add(gameData.id, gameData);
                }
            }
        }
        // Debug.Log($"[GameData Cache] {allGameDataCache.Count} GameData assets cached.");
    }

    public override void OnImportAsset(AssetImportContext ctx)
    {
        // =================================================================
        // !! 가장 중요한 디버깅 로그 !!
        // 이 로그가 콘솔에 어떻게 찍히는지 알려주시면 100% 원인 파악이 가능합니다.
        Debug.Log($"--- Import Started for '{ctx.assetPath}'. Current 'targetTypeAssemblyQualifiedName' is: '{(string.IsNullOrEmpty(targetTypeAssemblyQualifiedName) ? "NULL or EMPTY" : targetTypeAssemblyQualifiedName)}'");
        // =================================================================

        if (string.IsNullOrEmpty(targetTypeAssemblyQualifiedName))
        {
            Debug.Log($"-> Type name is empty. Skipping import for now. Please set the type in the inspector and press Apply.");
            return;
        }

        Type soType = Type.GetType(targetTypeAssemblyQualifiedName); // 에러 발생 지점

        if (soType == null)
        {
            ctx.LogImportError($"Could not find the specified type: '{targetTypeAssemblyQualifiedName}'.");
            return;
        }

        var parsedData = CSVParser.ParseFromString(File.ReadAllText(ctx.assetPath));
        if (parsedData == null || parsedData.Count == 0) return;

        var container = ScriptableObject.CreateInstance<DataImportContainer>();
        container.name = Path.GetFileNameWithoutExtension(ctx.assetPath) + " Data";
        ctx.AddObjectToAsset("main", container);
        ctx.SetMainObject(container);

        string prefix = soType.Name.Replace("Data", "").Replace("SO", "");
        ImportGenericData(ctx, parsedData, container, prefix, soType);

        Debug.Log($"<color=cyan>[{Path.GetFileName(ctx.assetPath)}] Import successful.</color>");
        EditorApplication.delayCall += RefreshCache;
    }


    private void ImportGenericData(AssetImportContext ctx, List<Dictionary<string, string>> parsedData, DataImportContainer container, string prefix, Type soType)
    {
        int rowIndex = 0;
        foreach (var row in parsedData)
        {
            rowIndex++;
            string assetName = $"{prefix}_{rowIndex:D4}";
            var soInstance = (GameData)ScriptableObject.CreateInstance(soType);
            soInstance.name = assetName;
            soInstance.id = assetName;

            ctx.AddObjectToAsset(assetName, soInstance);
            container.importedObjects.Add(soInstance);

            foreach (var header in row.Keys)
            {
                FieldInfo field = soType.GetField(header, BindingFlags.Public | BindingFlags.Instance);
                if (field != null)
                {
                    try { field.SetValue(soInstance, ParseValue(row[header], field.FieldType)); }
                    catch (Exception e) { Debug.LogWarning($"[{assetName}] Parse failed for field '{header}': {e.Message}", soInstance); }
                }
            }
        }
    }

    private object ParseValue(string value, Type type)
    {
        if (string.IsNullOrEmpty(value))
        {
            if (typeof(IList).IsAssignableFrom(type)) return Activator.CreateInstance(type);
            return null;
        }

        // 1. GameData SO 참조 타입 처리
        if (typeof(GameData).IsAssignableFrom(type))
        {
            if (allGameDataCache.TryGetValue(value, out GameData data)) return data;
            Debug.LogWarning($"Could not find GameData with ID '{value}' in cache.");
            return null;
        }

        // 2. Enum 타입 처리
        if (type.IsEnum) return Enum.Parse(type, value, true);

        // 3. List 타입 처리
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
        {
            Type itemType = type.GetGenericArguments()[0];
            IList list = (IList)Activator.CreateInstance(type);
            string[] items = value.Split(';');
            foreach (var item in items)
            {
                list.Add(ParseValue(item.Trim(), itemType)); // 재귀 호출로 리스트의 각 아이템(ID 문자열 등)을 변환
            }
            return list;
        }

        // 4. 그 외 기본 타입 처리
        return Convert.ChangeType(value, type);
    }

    #region CSV Parser Utility
    /// <summary>
    /// CSV 파서: 이 임포터 클래스 내부에 포함시켜 다른 파일 의존성을 제거합니다.
    /// </summary>
    private static class CSVParser
    {
        public static List<Dictionary<string, string>> ParseFromString(string csvText)
        {
            var data = new List<Dictionary<string, string>>();
            var lines = csvText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length < 2) return data; // 헤더 + 데이터 최소 2줄 필요

            // 헤더 파싱
            var headers = SplitCsvLine(lines[0]).Select(h => h.Trim()).ToArray();

            // 데이터 라인 파싱
            for (int i = 1; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i])) continue;

                var values = SplitCsvLine(lines[i]);
                var entry = new Dictionary<string, string>();
                for (int j = 0; j < headers.Length; j++)
                {
                    string value = (j < values.Length) ? values[j] : "";

                    // 따옴표로 감싸진 문자열 처리
                    if (value.StartsWith("\"") && value.EndsWith("\""))
                    {
                        value = value.Substring(1, value.Length - 2).Replace("\"\"", "\""); // "" -> " 이스케이프 처리
                    }
                    entry[headers[j]] = value.Trim();
                }
                data.Add(entry);
            }
            return data;
        }

        private static string[] SplitCsvLine(string line)
        {
            // 정규식을 사용하여 쉼표를 기준으로 분리하되, 따옴표 안의 쉼표는 무시합니다.
            return Regex.Split(line, ",(?=(?:[^\"]*\"[^\"]*\")*[^\"]*$)");
        }
    }
    #endregion
}



