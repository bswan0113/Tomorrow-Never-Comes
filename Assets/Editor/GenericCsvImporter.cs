using UnityEngine;
using UnityEditor;
#if UNITY_2020_2_OR_NEWER
using UnityEditor.AssetImporters;
#else
using UnityEditor.Experimental.AssetImporters;
#endif
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Globalization;

/// <summary>
/// CSV 파일을 기반으로 ScriptableObject 에셋들을 자동으로 생성하는 제네릭 임포터입니다.
/// 'Simple'과 'GroupedById' 두 가지 전략을 지원하여 다양한 형태의 CSV에 대응할 수 있습니다.
/// </summary>
[ScriptedImporter(1, "tncd")]
public class GenericCsvImporter : ScriptedImporter
{
    #region Inspector Fields
    [Tooltip("생성할 ScriptableObject의 Assembly Qualified Name. (인스펙터의 드롭다운을 통해 선택)")]
    public string targetTypeAssemblyQualifiedName;

    public enum ImportStrategy
    {
        [Tooltip("CSV 한 행이 하나의 ScriptableObject 에셋을 생성합니다. (예: ChoiceData)")]
        Simple,
        [Tooltip("동일한 ID를 가진 여러 행을 그룹화하여 하나의 ScriptableObject 에셋을 생성합니다. (예: DialogueData)")]
        GroupedById
    }

    [Tooltip("CSV 데이터를 SO로 변환하는 방식을 선택합니다.")]
    public ImportStrategy strategy = ImportStrategy.Simple;

    [Header("Grouped Strategy Settings")]
    [Tooltip("Grouped 모드에서 여러 행의 데이터를 담을 리스트 필드의 이름 (예: dialogueLines)")]
    public string groupedListField;

    [Tooltip("Grouped 모드에서 리스트에 들어갈 아이템의 타입 Assembly Qualified Name (예: DialogueLine, Assembly-CSharp)")]
    public string groupedListItemTypeAssemblyQualifiedName;
    #endregion

    private Dictionary<string, GameData> allGameDataCache;

    /// <summary>
    /// Unity가 이 에셋을 임포트할 때 호출하는 메인 메소드입니다.
    /// </summary>
    // GenericCsvImporter.cs 파일의 OnImportAsset 메소드를 이걸로 교체하세요.

    public override void OnImportAsset(AssetImportContext ctx)
    {
        Debug.Log($"<color=lime>--- Starting import for {Path.GetFileName(ctx.assetPath)} ---</color>");

        RefreshCache();

        if (string.IsNullOrEmpty(targetTypeAssemblyQualifiedName))
        {
            ctx.LogImportWarning("Importer configuration needed. Please select this CSV file and set the 'Target Type Assembly Qualified Name' in the Inspector, then click 'Apply'.");
            return;
        }
        Debug.Log($"Target Type: {targetTypeAssemblyQualifiedName}");

        Type soType = Type.GetType(targetTypeAssemblyQualifiedName);
        if (soType == null)
        {
            ctx.LogImportError($"Could not find the specified type: '{targetTypeAssemblyQualifiedName}'.");
            return;
        }

        var parsedData = CSVParser.ParseFromString(File.ReadAllText(ctx.assetPath));
        if (parsedData == null || parsedData.Count == 0)
        {
            Debug.LogWarning($"[{Path.GetFileName(ctx.assetPath)}] CSV file is empty or could not be parsed. Parsed data count: {(parsedData?.Count ?? 0)}");
            return;
        }
        Debug.Log($"Successfully parsed {parsedData.Count} rows from CSV.");

        var container = ScriptableObject.CreateInstance<DataImportContainer>();
        container.name = Path.GetFileNameWithoutExtension(ctx.assetPath) + " Data";
        ctx.AddObjectToAsset("main", container);
        ctx.SetMainObject(container);

        switch (strategy)
        {
            case ImportStrategy.Simple:
                Debug.Log("Using Simple import strategy.");
                ImportSimpleData(ctx, parsedData, container, soType);
                break;
            case ImportStrategy.GroupedById:
                Debug.Log("Using GroupedById import strategy.");
                ImportGroupedData(ctx, parsedData, container, soType);
                break;
        }

        Debug.Log($"<color=cyan>[{Path.GetFileName(ctx.assetPath)}] Import process finished. Check for created sub-assets.</color>");
    }

    #region Import Strategies
    /// <summary>
    /// 단순 전략 (1 행 = 1 SO)을 사용하여 데이터를 임포트합니다.
    /// </summary>
    private void ImportSimpleData(AssetImportContext ctx, List<Dictionary<string, string>> parsedData, DataImportContainer container, Type soType)
    {
        foreach (var row in parsedData)
        {
            if (!row.ContainsKey("ID") || string.IsNullOrEmpty(row["ID"]))
            {
                ctx.LogImportWarning("A row was skipped because it has no ID.");
                continue;
            }

            string assetId = row["ID"];
            var soInstance = (GameData)ScriptableObject.CreateInstance(soType);
            soInstance.name = assetId;
            soInstance.id = assetId;

            PopulateFields(ctx, soInstance, soType, row);

            ctx.AddObjectToAsset(assetId, soInstance);
            container.importedObjects.Add(soInstance);
        }
    }

    #endregion

    #region Helper Methods


    /// <summary>
    /// 그룹 전략 (N 행 = 1 SO)을 사용하여 데이터를 임포트합니다.
    /// [최종 수정] Two-Pass 로직을 명확하게 분리하여 모든 참조 문제를 해결합니다.
    /// </summary>
  private void ImportGroupedData(AssetImportContext ctx, List<Dictionary<string, string>> parsedData, DataImportContainer container, Type soType)
    {
        if (string.IsNullOrEmpty(groupedListField) || string.IsNullOrEmpty(groupedListItemTypeAssemblyQualifiedName))
        {
            ctx.LogImportError("Grouped Strategy requires 'Grouped List Field' and 'Grouped List Item Type' to be set.");
            return;
        }
        Type listItemType = Type.GetType(groupedListItemTypeAssemblyQualifiedName);
        if (listItemType == null)
        {
            ctx.LogImportError($"Could not find the grouped list item type: '{groupedListItemTypeAssemblyQualifiedName}'.");
            return;
        }

        var groupedData = new Dictionary<string, List<Dictionary<string, string>>>();
        string lastIdForGrouping = null;
        foreach (var row in parsedData)
        {
            string id = row.ContainsKey("ID") && !string.IsNullOrEmpty(row["ID"]) ? row["ID"] : lastIdForGrouping;
            if (string.IsNullOrEmpty(id)) continue;

            if (!groupedData.ContainsKey(id))
            {
                groupedData[id] = new List<Dictionary<string, string>>();
            }
            groupedData[id].Add(row);
            lastIdForGrouping = id;
        }

        foreach (var group in groupedData)
        {
            string assetId = group.Key;
            var groupRows = group.Value;

            var mainSoInstance = (GameData)ScriptableObject.CreateInstance(soType);
            mainSoInstance.name = assetId;
            mainSoInstance.id = assetId;

            // --- Pass 1: 리스트 아이템 생성 및 채우기 ---
            var listField = soType.GetField(groupedListField, BindingFlags.Public | BindingFlags.Instance);
            if (listField != null)
            {
                var list = (IList)Activator.CreateInstance(listField.FieldType);
                listField.SetValue(mainSoInstance, list);

                foreach (var row in groupRows)
                {
                    var listItem = Activator.CreateInstance(listItemType);
                    PopulateFields(ctx, listItem, listItemType, row);
                    list.Add(listItem);
                }
            }

            if (groupRows.Any())
            {
                var mainSoFields = soType.GetFields(BindingFlags.Public | BindingFlags.Instance);

                foreach (var field in mainSoFields)
                {
                    // Pass 1에서 이미 처리한 리스트 필드는 건너뜁니다.
                    if (field.Name == groupedListField) continue;
                    var rowWithValue = groupRows.LastOrDefault(row => row.ContainsKey(field.Name) && !string.IsNullOrEmpty(row[field.Name]));
                    if (rowWithValue != null)
                    {
                        string value = rowWithValue[field.Name];
                        Type fieldType = field.FieldType;
                        bool isGameDataList = typeof(IList).IsAssignableFrom(fieldType) && typeof(GameData).IsAssignableFrom(fieldType.GetGenericArguments()[0]);
                        if (isGameDataList)
                        {
                            var idsToLink = value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList();
                            if (idsToLink.Any())
                            {
                                container.pendingReferences.Add(new PendingReference(mainSoInstance, field.Name, idsToLink));
                            }
                        }
                        else
                        {
                            try
                            {
                                var parsedValue = ParseValue(ctx, value, field.FieldType);
                                field.SetValue(mainSoInstance, parsedValue);
                            }
                            catch (Exception e)
                            {
                                ctx.LogImportWarning($"[{assetId}] Parse failed for main field '{field.Name}': {e.Message}", mainSoInstance);
                            }
                        }
                    }
                }
            }

            ctx.AddObjectToAsset(assetId, mainSoInstance);
            container.importedObjects.Add(mainSoInstance);
        }
    }


    /// <summary>
    /// 리플렉션을 사용해 객체의 필드를 채우는 헬퍼 메소드
    /// [수정됨] 복잡한 로직을 제거하고, 이름이 일치하는 필드를 채우는 단순한 역할만 수행합니다.
    /// </summary>
    private void PopulateFields(AssetImportContext ctx, object targetObject, Type targetType, Dictionary<string, string> rowData)
    {
        foreach (var header in rowData.Keys)
        {
            // CSV 셀이 비어있으면 이 필드는 건너뜁니다.
            if (string.IsNullOrEmpty(rowData[header]))
            {
                continue;
            }

            FieldInfo field = targetType.GetField(header, BindingFlags.Public | BindingFlags.Instance);
            if (field != null)
            {
                try
                {
                    var parsedValue = ParseValue(ctx, rowData[header], field.FieldType);
                    field.SetValue(targetObject, parsedValue);
                }
                catch (Exception e)
                {
                    ctx.LogImportWarning($"[{targetObject.ToString()}] Parse failed for field '{header}': {e.Message}", (UnityEngine.Object)targetObject);
                }
            }
        }
    }

    private object GetDefaultValue(Type t)
    {
        if (t.IsValueType) return Activator.CreateInstance(t);
        return null;
    }

    /// <summary>
    /// 프로젝트 내의 모든 GameData 에셋을 스캔하여 ID 기반으로 캐싱합니다.
    /// </summary>
    public void RefreshCache()
    {
        allGameDataCache = new Dictionary<string, GameData>();
        string[] guids = AssetDatabase.FindAssets("t:GameData");

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameData gameData = AssetDatabase.LoadAssetAtPath<GameData>(path);
            if (gameData != null && !string.IsNullOrEmpty(gameData.id) && !allGameDataCache.ContainsKey(gameData.id))
            {
                allGameDataCache.Add(gameData.id, gameData);
            }
        }
    }

    /// <summary>
    /// 문자열 값을 주어진 타입으로 변환(파싱)합니다.
    /// </summary>
    private object ParseValue(AssetImportContext ctx, string value, Type type)
    {
        if (string.IsNullOrEmpty(value))
        {
            if (typeof(IList).IsAssignableFrom(type)) return Activator.CreateInstance(type);
            return type.IsValueType ? Activator.CreateInstance(type) : null;
        }

        try
        {
            if (type.IsEnum)
                return Enum.Parse(type, value, true);

            if (typeof(GameData).IsAssignableFrom(type))
            {
                return null;
            }

            if (typeof(UnityEngine.Object).IsAssignableFrom(type))
            {
                var asset = AssetDatabase.LoadAssetAtPath(value, type);
                if (asset != null) { ctx.DependsOnSourceAsset(value); return asset; }
                return null;
            }

            if (type == typeof(Vector2)) { string[] p = value.Split(';'); if (p.Length == 2) return new Vector2(float.Parse(p[0], CultureInfo.InvariantCulture), float.Parse(p[1], CultureInfo.InvariantCulture)); }
            if (type == typeof(Vector3)) { string[] p = value.Split(';'); if (p.Length == 3) return new Vector3(float.Parse(p[0], CultureInfo.InvariantCulture), float.Parse(p[1], CultureInfo.InvariantCulture), float.Parse(p[2], CultureInfo.InvariantCulture)); }
            if (type == typeof(Color)) { if (ColorUtility.TryParseHtmlString(value, out Color c)) return c; }

            if (typeof(IList).IsAssignableFrom(type))
            {
                Type itemType = type.IsArray ? type.GetElementType() : type.GetGenericArguments()[0];

                // 리스트의 아이템 타입이 GameData를 상속하는 경우
                if (typeof(GameData).IsAssignableFrom(itemType))
                {
                    // 여기서는 빈 리스트만 생성하고, 실제 채우는 작업은 PostProcessor에게 넘깁니다.
                    IList list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(itemType));
                    if (type.IsArray) { Array array = Array.CreateInstance(itemType, list.Count); list.CopyTo(array, 0); return array; }
                    return list;
                }

                IList generalList = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(itemType));
                if (string.IsNullOrEmpty(value)) return type.IsArray ? Array.CreateInstance(itemType, 0) : generalList;
                string[] items = value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var item in items)
                {
                    generalList.Add(ParseValue(ctx, item.Trim(), itemType));
                }
                if (type.IsArray) { Array array = Array.CreateInstance(itemType, generalList.Count); generalList.CopyTo(array, 0); return array; }
                return generalList;
            }

            if (typeof(IDictionary).IsAssignableFrom(type))
            {
                Type keyType = type.GetGenericArguments()[0];
                Type valueType = type.GetGenericArguments()[1];
                IDictionary dictionary = (IDictionary)Activator.CreateInstance(type);

                string[] pairs = value.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var pair in pairs)
                {
                    string[] keyValue = pair.Split(new[] { ':' }, 2);
                    if (keyValue.Length == 2)
                    {
                        var key = ParseValue(ctx, keyValue[0].Trim(), keyType);
                        var val = ParseValue(ctx, keyValue[1].Trim(), valueType);
                        dictionary.Add(key, val);
                    }
                }
                return dictionary;
            }

            return Convert.ChangeType(value, type, CultureInfo.InvariantCulture);
        }
        catch (Exception e)
        {
            ctx.LogImportWarning($"Exception while parsing value '{value}' for type '{type.Name}'. Reason: {e.Message}");
            return type.IsValueType ? Activator.CreateInstance(type) : null;
        }
    }
    #endregion

    #region CSV Parser Utility
    /// <summary>
    /// 따옴표로 묶인 값을 고려하여 CSV 문자열을 파싱하는 간단한 유틸리티 클래스입니다.
    /// </summary>
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
                if (string.IsNullOrWhiteSpace(lines[i]) || lines[i].Trim().StartsWith("#")) continue; // 주석 처리된 행 무시
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
            // 정규표현식을 사용하여 쉼표를 기준으로 분리하되, 따옴표 안의 쉼표는 무시합니다.
            return Regex.Split(line, ",(?=(?:[^\"]*\"[^\"]*\")*[^\"]*$)");
        }
    }
    #endregion
}