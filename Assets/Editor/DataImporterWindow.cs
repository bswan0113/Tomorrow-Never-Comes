// C:\Workspace\Tomorrow Never Comes\Assets\Editor\DataImporterWindow.cs

using UnityEngine;
using UnityEditor;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions; // ▼▼▼ 정규식을 사용하기 위해 추가 ▼▼▼

public class DataImporterWindow : EditorWindow
{
    // ... OnGUI, CreateNewConfigAsset, ImportAll, ProcessProfile, ImportGenericData 등 다른 함수는 이전과 완전히 동일합니다 ...
    // ... 수정을 위해 전체 코드를 다시 올립니다 ...

    private ImporterConfig config;

    [MenuItem("Tools/General Data Importer")]
    public static void ShowWindow() { GetWindow<DataImporterWindow>("General Data Importer"); }

    private void OnGUI()
    {
        GUILayout.Label("General Data Importer", EditorStyles.boldLabel);
        config = (ImporterConfig)EditorGUILayout.ObjectField("Importer Config File", config, typeof(ImporterConfig), false);
        if (config == null) { EditorGUILayout.HelpBox("Please create and assign an Importer Config file.", MessageType.Info); if (GUILayout.Button("Create New Config")) { CreateNewConfigAsset(); } return; }
        EditorGUILayout.Space();
        if (GUILayout.Button("Import All Enabled Profiles")) { ImportAll(); }
        EditorGUILayout.Space(20);
        foreach (var profile in config.profiles)
        {
            if (!profile.isEnabled) continue;
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(profile.profileName, EditorStyles.boldLabel);
            if (GUILayout.Button($"Import", GUILayout.Width(80))) { ProcessProfile(profile); }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space();
        }
    }
    private void CreateNewConfigAsset() { string path = EditorUtility.SaveFilePanelInProject("Save Importer Config", "ImporterConfig", "asset", ""); if (string.IsNullOrEmpty(path)) return; var newConfig = ScriptableObject.CreateInstance<ImporterConfig>(); AssetDatabase.CreateAsset(newConfig, path); AssetDatabase.SaveAssets(); config = newConfig; }
    private void ImportAll() { if (config == null) return; foreach (var profile in config.profiles) { if (profile.isEnabled) { ProcessProfile(profile); } } }
    private void ProcessProfile(ImporterProfile profile)
    {
        Type soType = Type.GetType(profile.soTypeFullName);
        if (soType == null || profile.csvFile == null) { Debug.LogError($"[{profile.profileName}] Profile is not configured correctly."); return; }
        Directory.CreateDirectory(profile.outputSOPath);
        if (soType == typeof(DialogueData)) { ImportDialogueData(profile, soType); } else { ImportGenericData(profile, soType); }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"<color=cyan>[{profile.profileName}] Import complete.</color>");
    }
    private void ImportGenericData(ImporterProfile profile, Type soType)
    {
        var parsedData = CSVParser.ParseFromString(profile.csvFile.text);
        if (parsedData.Count == 0) return;
        string idColumnHeader = parsedData[0].Keys.First();
        foreach(var row in parsedData)
        {
            string idValue = row[idColumnHeader];
            string assetPath = Path.Combine(profile.outputSOPath, $"{soType.Name}_{idValue}.asset");
            ScriptableObject so = AssetDatabase.LoadAssetAtPath(assetPath, soType) as ScriptableObject;
            if (so == null) { so = ScriptableObject.CreateInstance(soType); AssetDatabase.CreateAsset(so, assetPath); }
            Undo.RecordObject(so, "Imported Data");
            foreach(var header in row.Keys)
            {
                FieldInfo field = soType.GetField(header);
                if (field != null)
                {
                    try { object convertedValue = Convert.ChangeType(row[header], field.FieldType); field.SetValue(so, convertedValue); }
                    catch (Exception e) { Debug.LogError($"Failed to convert value '{row[header]}' for field '{header}' in asset {idValue}. Error: {e.Message}"); }
                }
            }
            EditorUtility.SetDirty(so);
        }
    }
    private void ImportDialogueData(ImporterProfile profile, Type soType)
    {
        var parsedData = CSVParser.ParseFromString(profile.csvFile.text);
        if (parsedData.Count == 0) return;
        var dialogueGroups = parsedData.GroupBy(row => int.Parse(row["id"]));
        foreach (var group in dialogueGroups)
        {
            int dialogueId = group.Key;
            string assetPath = Path.Combine(profile.outputSOPath, $"Dialogue_{dialogueId}.asset");
            DialogueData data = AssetDatabase.LoadAssetAtPath<DialogueData>(assetPath);
            if(data == null) { data = ScriptableObject.CreateInstance<DialogueData>(); AssetDatabase.CreateAsset(data, assetPath); }
            Undo.RecordObject(data, "Update Dialogue Data");
            data.id = dialogueId;
            data.dialogueLines = new List<DialogueLine>();
            data.choices = new List<Choice>();
            foreach (var row in group)
            {
                data.dialogueLines.Add(new DialogueLine { speakerID = int.Parse(row["speakerID"]), dialogueText = row["dialogueText"].Replace("\\n", "\n") });
                if (row.TryGetValue("choices", out string choiceData) && !string.IsNullOrEmpty(choiceData))
                {
                    var choices = new List<Choice>();
                    string[] choicePairs = choiceData.Split(';');
                    foreach (var pair in choicePairs)
                    {
                        if (string.IsNullOrWhiteSpace(pair)) continue;
                        string[] textAndId = pair.Split('>');
                        if (textAndId.Length < 2) continue; // 안전장치 추가
                        string[] choiceTextParts = textAndId[0].Split(':');
                        if (choiceTextParts.Length < 2) continue; // 안전장치 추가
                        string choiceText = choiceTextParts[1];
                        int nextDialogueId = int.Parse(textAndId[1]);
                        choices.Add(new Choice { choiceText = choiceText, nextDialogueID = nextDialogueId });
                    }
                    data.choices = choices;
                }
            }
            EditorUtility.SetDirty(data);
        }
    }
}

public static class CSVParser
{
    // ▼▼▼ 핵심 수정 ▼▼▼: 정규식을 사용하여 따옴표 안의 쉼표를 무시하는 파서로 변경
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
                // 따옴표로 감싸여 있었다면 앞뒤 따옴표 제거
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
        // 이 정규식은 따옴표로 묶인 필드 안의 쉼표를 무시합니다.
        return Regex.Split(line, ",(?=(?:[^\"]*\"[^\"]*\")*[^\"]*$)");
    }
}