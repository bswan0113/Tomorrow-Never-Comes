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
using System.Globalization;

[ScriptedImporter(1, "tncd")]
public class GenericCsvImporter : ScriptedImporter
{
    public string targetTypeAssemblyQualifiedName;
    private Dictionary<string, GameData> allGameDataCache; // static 제거

    // static 제거, RefreshCache는 이제 임포터 인스턴스에 속함
    public void RefreshCache()
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
        Debug.Log($"[GameData Cache] Refreshed. {allGameDataCache.Count} GameData assets cached.");
    }

    public override void OnImportAsset(AssetImportContext ctx)
    {
        // 임포트가 시작될 때마다 캐시를 새로고침합니다.
        RefreshCache();

        if (string.IsNullOrEmpty(targetTypeAssemblyQualifiedName)) return;

        Type soType = Type.GetType(targetTypeAssemblyQualifiedName);
        if (soType == null)
        {
            ctx.LogImportError($"Could not find the specified type: '{targetTypeAssemblyQualifiedName}'.");
            return;
        }

        var parsedData = CSVParser.ParseFromString(File.ReadAllText(ctx.assetPath));
        if (parsedData == null || parsedData.Count == 0) return;

        var container = ScriptableObject.CreateInstance<DataImportContainer>();
        container.name = Path.GetFileNameWithoutExtension(ctx.assetPath) + " Data";
        container.hideFlags = HideFlags.None;
        ctx.AddObjectToAsset("main", container);
        ctx.SetMainObject(container);

        string prefix = soType.Name.Replace("Data", "").Replace("SO", "");
        ImportGenericData(ctx, parsedData, container, prefix, soType);

        Debug.Log($"<color=cyan>[{Path.GetFileName(ctx.assetPath)}] Import successful.</color>");
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
            soInstance.hideFlags = HideFlags.None;
            ctx.AddObjectToAsset(assetName, soInstance);
            container.importedObjects.Add(soInstance);

            foreach (var header in row.Keys)
            {
                FieldInfo field = soType.GetField(header, BindingFlags.Public | BindingFlags.Instance);
                if (field != null)
                {
                    try
                    {
                        field.SetValue(soInstance, ParseValue(ctx, row[header], field.FieldType));
                    }
                    catch (Exception e)
                    {
                        ctx.LogImportWarning($"[{assetName}] Parse failed for field '{header}': {e.Message}", soInstance);
                        Debug.LogException(e); // 더 자세한 예외 정보를 위해 추가
                    }
                }
            }
        }
    }

    private object ParseValue(AssetImportContext ctx, string value, Type type)
    {
        if (string.IsNullOrEmpty(value))
        {
            if (typeof(IList).IsAssignableFrom(type) || typeof(IDictionary).IsAssignableFrom(type))
                return Activator.CreateInstance(type);
            return type.IsValueType ? Activator.CreateInstance(type) : null;
        }

        try
        {
            if (type.IsEnum)
                return Enum.Parse(type, value, true);

            if (typeof(GameData).IsAssignableFrom(type))
            {
                if (allGameDataCache.TryGetValue(value, out GameData data))
                {
                    string path = AssetDatabase.GetAssetPath(data);
                    if (!string.IsNullOrEmpty(path)) ctx.DependsOnSourceAsset(path);
                    return data;
                }
                ctx.LogImportWarning($"Could not find GameData with ID '{value}' in cache.");
                return null;
            }

            if (typeof(UnityEngine.Object).IsAssignableFrom(type))
            {
                var asset = AssetDatabase.LoadAssetAtPath(value, type);
                if (asset != null) { ctx.DependsOnSourceAsset(value); return asset; }
                ctx.LogImportWarning($"Could not find asset of type '{type.Name}' at path: '{value}'");
                return null;
            }

            if (type == typeof(Vector2)) { string[] p = value.Split(';'); if (p.Length == 2) return new Vector2(float.Parse(p[0], CultureInfo.InvariantCulture), float.Parse(p[1], CultureInfo.InvariantCulture)); }
            if (type == typeof(Vector3)) { string[] p = value.Split(';'); if (p.Length == 3) return new Vector3(float.Parse(p[0], CultureInfo.InvariantCulture), float.Parse(p[1], CultureInfo.InvariantCulture), float.Parse(p[2], CultureInfo.InvariantCulture)); }
            if (type == typeof(Color)) { if (ColorUtility.TryParseHtmlString(value, out Color c)) return c; string[] p = value.Split(';'); if (p.Length >= 3) return new Color(float.Parse(p[0]), float.Parse(p[1]), float.Parse(p[2]), p.Length > 3 ? float.Parse(p[3]) : 1f); }

            if (typeof(IList).IsAssignableFrom(type))
            {
                Type itemType = type.IsArray ? type.GetElementType() : type.GetGenericArguments()[0];
                IList list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(itemType));
                char separator = (typeof(GameData).IsAssignableFrom(itemType) || itemType.IsPrimitive || itemType.IsEnum || itemType == typeof(string)) ? ',' : ';';

                if (string.IsNullOrEmpty(value)) return type.IsArray ? Array.CreateInstance(itemType, 0) : list;

                string[] items = value.Split(new[] { separator }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var item in items)
                {
                    object parsedItem = ParseValue(ctx, item.Trim(), itemType);

                    // 타입 안전성 강화를 위한 명시적 검사 추가
                    if (parsedItem != null && !itemType.IsAssignableFrom(parsedItem.GetType()))
                    {
                        ctx.LogImportError($"Type mismatch! Parsed item of type '{parsedItem.GetType().Name}' cannot be added to a list of type '{itemType.Name}'.");
                        continue;
                    }
                    list.Add(parsedItem);
                }
                if (type.IsArray) { Array array = Array.CreateInstance(itemType, list.Count); list.CopyTo(array, 0); return array; }
                return list;
            }

            if (typeof(IDictionary).IsAssignableFrom(type)) { /* ... 딕셔너리 로직 ... */ }

            if ((type.IsClass && type != typeof(string) && !typeof(UnityEngine.Object).IsAssignableFrom(type)) || (type.IsValueType && !type.IsPrimitive && !type.IsEnum))
            {
                var instance = Activator.CreateInstance(type);
                var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);
                string[] fieldValues = value.Split('|');
                if (fields.Length != fieldValues.Length)
                {
                    ctx.LogImportWarning($"Field count mismatch for type '{type.Name}'. Expected {fields.Length}, got {fieldValues.Length} from '{value}'.");
                    return instance;
                }
                for (int i = 0; i < fields.Length; i++)
                {
                    // ▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼
                    // --- 여기가 버그가 수정된 부분입니다. ---
                    var currentField = fields[i];
                    var fieldValueStr = fieldValues[i].Trim();
                    var parsedValue = ParseValue(ctx, fieldValueStr, currentField.FieldType);
                    currentField.SetValue(instance, parsedValue);
                    // ▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲
                }
                return instance;
            }

            return Convert.ChangeType(value, type, CultureInfo.InvariantCulture);
        }
        catch (Exception e)
        {
            ctx.LogImportWarning($"!!! EXCEPTION while parsing value '{value}' for type '{type.Name}'. Reason: {e.Message}");
            Debug.LogException(e);
            return type.IsValueType ? Activator.CreateInstance(type) : null;
        }
    }

    #region CSV Parser Utility
    // --- CSV Parser 부분은 기존 코드와 동일합니다. ---
    private static class CSVParser
    {
        public static List<Dictionary<string, string>> ParseFromString(string csvText)
        {
            var data = new List<Dictionary<string, string>>();
            var lines = csvText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length < 2) return data;
            var headers = SplitCsvLine(lines[0]).Select(h => h.Trim()).ToArray();
            for (int i = 1; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i])) continue;
                var values = SplitCsvLine(lines[i]);
                var entry = new Dictionary<string, string>();
                for (int j = 0; j < headers.Length; j++)
                {
                    string value = (j < values.Length) ? values[j] : "";
                    if (value.StartsWith("\"") && value.EndsWith("\""))
                    {
                        value = value.Substring(1, value.Length - 2).Replace("\"\"", "\"");
                    }
                    entry[headers[j]] = value.Trim();
                }
                data.Add(entry);
            }
            return data;
        }

        private static string[] SplitCsvLine(string line)
        {
            return Regex.Split(line, ",(?=(?:[^\"]*\"[^\"]*\")*[^\"]*$)");
        }
    }
    #endregion
}