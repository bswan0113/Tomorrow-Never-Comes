// C:\Workspace\Tomorrow Never Comes\Assets\Editor\DataImporterWindow.cs

using UnityEngine;
using UnityEditor;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

public class DataImporterWindow : EditorWindow
{
    private ImporterConfig config;

    [MenuItem("Tools/General Data Importer")]
    public static void ShowWindow() { GetWindow<DataImporterWindow>("General Data Importer"); }

    private void OnGUI()
    {
        GUILayout.Label("General Data Importer", EditorStyles.boldLabel);
        config = (ImporterConfig)EditorGUILayout.ObjectField("Importer Config File", config, typeof(ImporterConfig), false);

        if (config == null)
        {
            EditorGUILayout.HelpBox("Please create and assign an Importer Config file.", MessageType.Info);
            if (GUILayout.Button("Create New Config"))
            {
                CreateNewConfigAsset();
            }
            return;
        }

        EditorGUILayout.Space();
        if (GUILayout.Button("Import All Enabled Profiles"))
        {
            ImportAll();
        }

        EditorGUILayout.Space(20);

        foreach (var profile in config.profiles)
        {
            if (!profile.isEnabled) continue;
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(profile.profileName, EditorStyles.boldLabel);
            if (GUILayout.Button($"Import", GUILayout.Width(80)))
            {
                ProcessProfile(profile);
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space();
        }
    }

    private void CreateNewConfigAsset()
    {
        string path = EditorUtility.SaveFilePanelInProject("Save Importer Config", "ImporterConfig", "asset", "");
        if (string.IsNullOrEmpty(path)) return;
        var newConfig = ScriptableObject.CreateInstance<ImporterConfig>();
        AssetDatabase.CreateAsset(newConfig, path);
        AssetDatabase.SaveAssets();
        config = newConfig;
    }

    private void ImportAll()
    {
        if (config == null) return;
        foreach (var profile in config.profiles)
        {
            if (profile.isEnabled)
            {
                ProcessProfile(profile);
            }
        }
    }

    /// <summary>
    /// 지정된 경로에서 특정 접두사를 가진 파일 중 가장 큰 숫자 ID를 찾습니다.
    /// </summary>
    private int GetLastUsedID(string path, string prefix)
    {
        if (!Directory.Exists(path)) return 0;

        var files = Directory.GetFiles(path, $"{prefix}*.asset");
        int maxId = 0;

        foreach (var file in files)
        {
            string fileName = Path.GetFileNameWithoutExtension(file);
            string numberPart = fileName.Replace(prefix, "");
            if (int.TryParse(numberPart, out int id))
            {
                if (id > maxId)
                {
                    maxId = id;
                }
            }
        }
        return maxId;
    }

    private void ProcessProfile(ImporterProfile profile)
    {
        Type soType = Type.GetType(profile.soTypeFullName);
        if (soType == null || profile.csvFile == null)
        {
            Debug.LogError($"[{profile.profileName}] Profile is not configured correctly.");
            return;
        }

        Directory.CreateDirectory(profile.outputSOPath);

        string prefix = soType.Name.Replace("Data", "").Replace("SO", "") + "_";

        if (soType == typeof(DialogueData))
        {
            // ImportDialogueData(profile, soType, prefix);
        }
        else
        {
            ImportGenericData(profile, soType, prefix);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"<color=cyan>[{profile.profileName}] Import complete.</color>");
    }

    private void ImportGenericData(ImporterProfile profile, Type soType, string prefix)
    {
        var parsedData = CSVParser.ParseFromString(profile.csvFile.text);
        if (parsedData.Count == 0) return;

        int currentId = GetLastUsedID(profile.outputSOPath, prefix);

        foreach(var row in parsedData)
        {
            currentId++;
            string finalId = $"{prefix}{currentId:D4}"; // 예: "Character_0001"
            string assetPath = Path.Combine(profile.outputSOPath, $"{finalId}.asset");

            // GenericData는 CSV에 ID가 없으므로 항상 새로 생성합니다.
            ScriptableObject so = ScriptableObject.CreateInstance(soType);
            AssetDatabase.CreateAsset(so, assetPath);

            Undo.RecordObject(so, "Imported Data");

            FieldInfo idField = soType.GetField("id");
            if (idField != null && idField.FieldType == typeof(string))
            {
                idField.SetValue(so, finalId);
            }

            foreach(var header in row.Keys)
            {
                FieldInfo field = soType.GetField(header);
                if (field != null)
                {
                    try
                    {
                        object convertedValue = Convert.ChangeType(row[header], field.FieldType);
                        field.SetValue(so, convertedValue);
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"Failed to convert value '{row[header]}' for field '{header}' in asset {finalId}. Error: {e.Message}");
                    }
                }
            }
            EditorUtility.SetDirty(so);
        }
    }

    // private void ImportDialogueData(ImporterProfile profile, Type soType, string prefix)
    // {
    //     var parsedData = CSVParser.ParseFromString(profile.csvFile.text);
    //     if (parsedData.Count == 0) return;
    //
    //     int lastId = GetLastUsedID(profile.outputSOPath, prefix);
    //     int newIdCounter = lastId;
    //
    //     var keyToNewIdMap = new Dictionary<string, string>();
    //     var dialogueGroups = parsedData.GroupBy(row => row["dialogueKey"]);
    //
    //     // 1단계: 모든 고유 'dialogueKey'에 대해 새로운 숫자 ID를 미리 할당합니다.
    //     foreach (var group in dialogueGroups)
    //     {
    //         string dialogueKey = group.Key;
    //         if (string.IsNullOrEmpty(dialogueKey) || keyToNewIdMap.ContainsKey(dialogueKey)) continue;
    //
    //         newIdCounter++;
    //         string newFinalId = $"{prefix}{newIdCounter:D4}";
    //         keyToNewIdMap[dialogueKey] = newFinalId;
    //     }
    //
    //     // 2단계: ID 매핑을 사용하여 에셋을 생성하고 데이터를 채웁니다.
    //     foreach (var group in dialogueGroups)
    //     {
    //         string dialogueKey = group.Key;
    //         if (string.IsNullOrEmpty(dialogueKey)) continue;
    //
    //         string finalId = keyToNewIdMap[dialogueKey];
    //         string assetPath = Path.Combine(profile.outputSOPath, $"{finalId}.asset");
    //
    //         DialogueData data = AssetDatabase.LoadAssetAtPath<DialogueData>(assetPath);
    //         if(data == null)
    //         {
    //             data = ScriptableObject.CreateInstance<DialogueData>();
    //             AssetDatabase.CreateAsset(data, assetPath);
    //         }
    //
    //         Undo.RecordObject(data, "Update Dialogue Data");
    //
    //         data.id = finalId;
    //         data.dialogueLines = new List<DialogueLine>();
    //         data.choices = new List<Choice>();
    //
    //         foreach (var row in group)
    //         {
    //             data.dialogueLines.Add(new DialogueLine { speakerID = row["speakerID"], dialogueText = row["dialogueText"].Replace("\\n", "\n") });
    //
    //             if (row.TryGetValue("choices", out string choiceData) && !string.IsNullOrEmpty(choiceData))
    //             {
    //                 var choices = new List<Choice>();
    //                 string[] choicePairs = choiceData.Split(';');
    //                 foreach (var pair in choicePairs)
    //                 {
    //                     if (string.IsNullOrWhiteSpace(pair)) continue;
    //                     string[] textAndId = pair.Split('>');
    //                     if (textAndId.Length < 2) continue;
    //
    //                     string choiceText = textAndId[0];
    //                     string nextDialogueKey = textAndId[1];
    //
    //                     string nextDialogueFullId = "";
    //                     if (nextDialogueKey != "0" && !string.IsNullOrEmpty(nextDialogueKey) && keyToNewIdMap.ContainsKey(nextDialogueKey))
    //                     {
    //                         nextDialogueFullId = keyToNewIdMap[nextDialogueKey];
    //                     }
    //
    //                     choices.Add(new Choice { choiceText = choiceText, nextDialogueID = nextDialogueFullId });
    //                 }
    //                 data.choices = choices;
    //             }
    //         }
    //         EditorUtility.SetDirty(data);
    //     }
    // }
}


public static class CSVParser
{
    public static List<Dictionary<string, string>> ParseFromString(string csvText)
    {
        var data = new List<Dictionary<string, string>>();
        var lines = csvText.Split(new[] { '\r', '\n' }).Where(line => !string.IsNullOrWhiteSpace(line)).ToList();
        if (lines.Count < 2) return data;

        var headers = SplitCsvLine(lines[0]).Select(h => h.Trim()).ToArray();

        for (int i = 1; i < lines.Count; i++)
        {
            var values = SplitCsvLine(lines[i]);
            var entry = new Dictionary<string, string>();
            for (int j = 0; j < headers.Length; j++)
            {
                string value = (j < values.Length) ? values[j].Trim() : "";
                if (value.StartsWith("\"") && value.EndsWith("\""))
                {
                    value = value.Substring(1, value.Length - 2);
                }
                entry[headers[j]] = value;
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