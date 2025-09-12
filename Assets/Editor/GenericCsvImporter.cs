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
using Core.Logging;

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

    /// <summary>
    /// Unity가 이 에셋을 임포트할 때 호출하는 메인 메소드입니다.
    /// </summary>
    public override void OnImportAsset(AssetImportContext ctx)
    {
        CoreLogger.Log($"<color=lime>--- Starting import for {Path.GetFileName(ctx.assetPath)} ---</color>");

        if (string.IsNullOrEmpty(targetTypeAssemblyQualifiedName))
        {
            ctx.LogImportWarning("Importer configuration needed. Please select this CSV file and set the 'Target Type Assembly Qualified Name' in the Inspector, then click 'Apply'.");
            return;
        }
        CoreLogger.Log($"Target Type: {targetTypeAssemblyQualifiedName}");

        Type soType = Type.GetType(targetTypeAssemblyQualifiedName);
        if (soType == null)
        {
            ctx.LogImportError($"Could not find the specified type: '{targetTypeAssemblyQualifiedName}'.");
            return;
        }

        var parsedData = CSVParser.ParseFromString(File.ReadAllText(ctx.assetPath));
        if (parsedData == null || parsedData.Count == 0)
        {
            CoreLogger.LogWarning($"[{Path.GetFileName(ctx.assetPath)}] CSV file is empty or could not be parsed. Parsed data count: {(parsedData?.Count ?? 0)}");
            return;
        }
        CoreLogger.Log($"Successfully parsed {parsedData.Count} rows from CSV.");

        var container = ScriptableObject.CreateInstance<DataImportContainer>();
        container.name = Path.GetFileNameWithoutExtension(ctx.assetPath) + " Data";
        ctx.AddObjectToAsset("main", container);
        ctx.SetMainObject(container);

        switch (strategy)
        {
            case ImportStrategy.Simple:
                CoreLogger.Log("Using Simple import strategy.");
                ImportSimpleData(ctx, parsedData, container, soType);
                CoreLogger.Log($"<color=cyan>[{Path.GetFileName(ctx.assetPath)}] Simple strategy finished. Created {container.importedObjects.Count} ScriptableObjects.</color>");
                break;
            case ImportStrategy.GroupedById:
                CoreLogger.Log("Using GroupedById import strategy.");
                ImportGroupedData(ctx, parsedData, container, soType);
                CoreLogger.Log($"<color=cyan>[{Path.GetFileName(ctx.assetPath)}] GroupedById strategy finished. Created {container.importedObjects.Count} ScriptableObjects.</color>");
                break;
        }

        CoreLogger.Log($"<color=cyan>[{Path.GetFileName(ctx.assetPath)}] Import process finished. Check for created sub-assets.</color>");
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
            CoreLogger.Log($"  Created simple SO: <color=yellow>{assetId}</color> of type {soType.Name}");

            PopulateFields(ctx, soInstance, soType, row, container);

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
        {   // 첫 열이 비어있는 경우, 이전 ID를 사용하여 그룹화합니다.
            string id = row.ContainsKey("ID") && !string.IsNullOrEmpty(row["ID"]) ? row["ID"] : lastIdForGrouping;
            if (string.IsNullOrEmpty(id))
            {
                CoreLogger.LogWarning($"Skipping row due to missing ID and no previous ID for grouping: {string.Join(", ", row.Values)}");
                continue;
            }
            if (!groupedData.ContainsKey(id))
            {
                groupedData[id] = new List<Dictionary<string, string>>();
            }
            groupedData[id].Add(row);
            lastIdForGrouping = id;
        }

        CoreLogger.Log($"  Grouped {parsedData.Count} rows into {groupedData.Count} distinct IDs.");
        foreach (var group in groupedData)
        {
            string assetId = group.Key;
            var groupRows = group.Value;

            var mainSoInstance = (GameData)ScriptableObject.CreateInstance(soType);
            CoreLogger.Log($"  Created grouped SO: <color=yellow>{assetId}</color> of type {soType.Name} with {groupRows.Count} sub-items.");
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
                    PopulateFields(ctx, listItem, listItemType, row, container); // 리스트 아이템 내부 필드 채우기
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

                        // GameData 타입 (단일 또는 리스트)에 대한 참조는 PendingReference로 넘깁니다.
                        // GameData 단일 참조인 경우
                        if (typeof(GameData).IsAssignableFrom(fieldType))
                        {
                            if (!string.IsNullOrEmpty(value)) // 단일 GameData 참조
                            {
                                container.pendingReferences.Add(new PendingReference(mainSoInstance, field.Name, new List<string> { value.Trim() }, isList: false));
                                CoreLogger.Log($"    Pending single GameData reference for <color=yellow>{mainSoInstance.name}</color>.<color=orange>{field.Name}</color> with ID: {value.Trim()}");
                            }
                            field.SetValue(mainSoInstance, null); // 초기에는 null로 설정
                        }
                        else if (typeof(IList).IsAssignableFrom(fieldType) && typeof(GameData).IsAssignableFrom(fieldType.GetGenericArguments()[0])) // GameData 리스트 참조인 경우
                        {
                            var idsToLink = value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList();
                            if (idsToLink.Any())
                            {
                                container.pendingReferences.Add(new PendingReference(mainSoInstance, field.Name, idsToLink, isList: true)); // 리스트 GameData 참조
                                CoreLogger.Log($"    Pending list GameData reference for <color=yellow>{mainSoInstance.name}</color>.<color=orange>{field.Name}</color> with IDs: {string.Join(", ", idsToLink)}");
                            }
                            field.SetValue(mainSoInstance, Activator.CreateInstance(fieldType)); // 초기에는 빈 리스트로 설정
                        }
                        else
                        {
                            // 그 외의 필드는 ParseValue를 통해 즉시 채웁니다.
                            SetFieldParsedValue(ctx, mainSoInstance, field, value);
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
    private void PopulateFields(AssetImportContext ctx, object targetObject, Type targetType, Dictionary<string, string> rowData, DataImportContainer container)
    {
        foreach (var header in rowData.Keys)
        {
            // CSV 셀이 비어있으면 이 필드는 건너뜠거나, GameData 참조인 경우 null로 설정
            if (string.IsNullOrEmpty(rowData[header]))
            {
                FieldInfo fieldForEmpty = targetType.GetField(header, BindingFlags.Public | BindingFlags.Instance);
                if (fieldForEmpty != null && typeof(GameData).IsAssignableFrom(fieldForEmpty.FieldType))
                {
                    fieldForEmpty.SetValue(targetObject, null);
                }
                continue;
            }

            FieldInfo field = targetType.GetField(header, BindingFlags.Public | BindingFlags.Instance);
            if (field != null)
            {
                Type fieldType = field.FieldType;

                // GameData 타입의 필드는 즉시 파싱하지 않고 PendingReference에 추가
                if (typeof(GameData).IsAssignableFrom(fieldType))
                {
                    string id = rowData[header].Trim();
                    if (!string.IsNullOrEmpty(id) && targetObject is ScriptableObject so)
                    {
                        container.pendingReferences.Add(new PendingReference(so, field.Name, new List<string> { id }, isList: false));
                        CoreLogger.Log($"    Pending single GameData reference for <color=yellow>{so.name}</color>.<color=orange>{field.Name}</color> with ID: {id}");
                    }
                    // GameData 필드는 초기에는 null로 설정, Postprocessor에서 채워집니다.
                    field.SetValue(targetObject, null);
                }
                // GameData 리스트 타입은 PopulateFields에서는 처리하지 않고, GroupedById 전략에서 별도로 처리하거나
                // Simple 전략의 경우 리스트 필드에 대한 CSV 값이 있다면 Postprocessor에서 처리해야 합니다.
                // 여기서는 일반 리스트/배열처럼 값을 파싱하여 채우거나, GameData 리스트인 경우 빈 리스트를 초기화하고 Postprocessor에서 처리하도록 합니다.
                else if (typeof(IList).IsAssignableFrom(fieldType) && typeof(GameData).IsAssignableFrom(fieldType.GetGenericArguments()[0]))
                {
                     // Simple 전략의 GameData 리스트 필드 (이 경우 CSV 셀에 콤마로 구분된 ID 목록이 있을 수 있음)
                     // 이 필드도 PendingReference로 처리하여 Postprocessor에서 해결하도록 합니다.
                     string value = rowData[header];
                     if (!string.IsNullOrEmpty(value) && targetObject is ScriptableObject so)
                     {
                         var idsToLink = value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList();
                         if (idsToLink.Any())
                         {
                             container.pendingReferences.Add(new PendingReference(so, field.Name, idsToLink, isList: true));
                             CoreLogger.Log($"    Pending list GameData reference for <color=yellow>{so.name}</color>.<color=orange>{field.Name}</color> with IDs: {string.Join(", ", idsToLink)}");
                         }
                     }
                     field.SetValue(targetObject, Activator.CreateInstance(fieldType)); // 초기에는 빈 리스트로 설정
                }
                else
                {
                    // 그 외의 필드는 ParseValue를 통해 즉시 채웁니다.
                    SetFieldParsedValue(ctx, targetObject, field, rowData[header]);
                }
            }
        }
    }

    /// <summary>
    /// 주어진 필드에 파싱된 값을 설정하는 헬퍼 메소드 (예외 처리 포함)
    /// </summary>
    private void SetFieldParsedValue(AssetImportContext ctx, object targetObject, FieldInfo field, string valueString)
    {
        try
        {
            var parsedValue = ParseValue(ctx, valueString, field.FieldType);
            field.SetValue(targetObject, parsedValue);
        }
        catch (Exception e)
        {
            ctx.LogImportWarning($"[{targetObject.GetType().Name}.{field.Name}] Parse failed for value '{valueString}' to type '{field.FieldType.Name}': {e.Message}", (UnityEngine.Object)targetObject);
        }
    }

    private object GetDefaultValue(Type t)
    {
        if (t.IsValueType) return Activator.CreateInstance(t);
        return null;
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
            // Enum 타입 파싱
            if (type.IsEnum)
                return Enum.Parse(type, value, true);

            // GameData 타입은 Postprocessor에서 처리하므로, 여기서는 null을 반환합니다.
            // GameData 참조는 이미 PopulateFields에서 PendingReference로 처리되었으므로,
            // 이 ParseValue는 GameData 필드에 대해 호출될 일이 없어야 합니다.
            if (typeof(GameData).IsAssignableFrom(type))
            {
                // CoreLogger.LogWarning($"ParseValue called for GameData type '{type.Name}'. This should not happen if references are handled by PendingReference.");
                return null;
            }

            if (typeof(UnityEngine.Object).IsAssignableFrom(type))
            {
                var asset = AssetDatabase.LoadAssetAtPath(value, type);
                if (asset != null) { ctx.DependsOnSourceAsset(value); CoreLogger.Log($"    Loaded UnityEngine.Object asset: {value} as {type.Name}"); return asset; }
                CoreLogger.LogWarning($"    Could not load UnityEngine.Object asset at path '{value}' for type '{type.Name}'.");
                return null;
            }

            if (type == typeof(Vector2)) { string[] p = value.Split(';'); if (p.Length == 2) return new Vector2(float.Parse(p[0], CultureInfo.InvariantCulture), float.Parse(p[1], CultureInfo.InvariantCulture)); }
            if (type == typeof(Vector3)) { string[] p = value.Split(';'); if (p.Length == 3) return new Vector3(float.Parse(p[0], CultureInfo.InvariantCulture), float.Parse(p[1], CultureInfo.InvariantCulture), float.Parse(p[2], CultureInfo.InvariantCulture)); }
            if (type == typeof(Color)) { if (ColorUtility.TryParseHtmlString(value, out Color c)) return c; }

            if (typeof(IList).IsAssignableFrom(type))
            {
                Type itemType = type.IsArray ? type.GetElementType() : type.GetGenericArguments()[0];

                // 리스트의 아이템 타입이 GameData를 상속하는 경우
                // 이 ParseValue에서는 GameData 리스트를 직접 채우지 않습니다.
                // 이는 PopulateFields에서 PendingReference로 처리되었어야 합니다.
                if (typeof(GameData).IsAssignableFrom(itemType))
                {
                    // CoreLogger.LogWarning($"ParseValue called for GameData list type '{type.Name}'. This should not happen if references are handled by PendingReference.");
                    IList list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(itemType));
                    if (type.IsArray) { Array array = Array.CreateInstance(itemType, list.Count); list.CopyTo(array, 0); return array; }
                    return list; // 빈 리스트 반환
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

            // 기본 타입 (int, float, bool, string 등) 파싱
            return Convert.ChangeType(value, type, CultureInfo.InvariantCulture);
        }
        catch (FormatException e)
        {
            ctx.LogImportWarning($"Failed to parse value '{value}' to type '{type.Name}' due to format error: {e.Message}", ctx.mainObject);
            return type.IsValueType ? Activator.CreateInstance(type) : null;
        }
        catch (InvalidCastException e)
        {
            ctx.LogImportWarning($"Failed to cast value '{value}' to type '{type.Name}': {e.Message}", ctx.mainObject);
            return type.IsValueType ? Activator.CreateInstance(type) : null;
        }
        catch (Exception e)
        {
            ctx.LogImportWarning($"Exception while parsing value '{value}' for type '{type.Name}'. Reason: {e.Message}", ctx.mainObject);
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