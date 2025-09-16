// C:\Workspace\Tomorrow Never Comes\Assets\Editor\ImporterProfileDrawer.cs

using System;
using System.Linq;
using ScriptableObjects.Abstract;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(ImporterProfile))]
public class ImporterProfileDrawer : PropertyDrawer
{
    private static Type[] gameDataTypes;
    private static string[] gameDataTypeNames;

    static ImporterProfileDrawer()
    {
        gameDataTypes = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(assembly => assembly.GetTypes())
            // ▼▼▼ [핵심 수정] GameData -> GameDataSO ▼▼▼
            .Where(type => type.IsSubclassOf(typeof(GameData)) && !type.IsAbstract)
            .ToArray();

        gameDataTypeNames = gameDataTypes.Select(type => type.Name).ToArray();
    }

    // ... (OnGUI와 GetPropertyHeight 함수는 기존과 완전히 동일) ...
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);
        var profileName = property.FindPropertyRelative("profileName");
        var isEnabled = property.FindPropertyRelative("isEnabled");
        var soTypeFullName = property.FindPropertyRelative("soTypeFullName");
        var csvFile = property.FindPropertyRelative("csvFile");
        var outputSOPath = property.FindPropertyRelative("outputSOPath");
        position.height = EditorGUIUtility.singleLineHeight;
        profileName.stringValue = EditorGUI.TextField(new Rect(position.x, position.y, position.width - 20, position.height), profileName.stringValue);
        isEnabled.boolValue = EditorGUI.Toggle(new Rect(position.x + position.width - 20, position.y, 20, position.height), isEnabled.boolValue);
        position.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
        EditorGUI.BeginChangeCheck();
        int currentIndex = Array.IndexOf(gameDataTypes, Type.GetType(soTypeFullName.stringValue));
        int selectedIndex = EditorGUI.Popup(position, "SO Type", currentIndex, gameDataTypeNames);
        if (EditorGUI.EndChangeCheck())
        {
            if (selectedIndex >= 0)
            {
                Type selectedType = gameDataTypes[selectedIndex];
                soTypeFullName.stringValue = selectedType.AssemblyQualifiedName;
                if (string.IsNullOrEmpty(outputSOPath.stringValue))
                {
                    string typeFolderName = selectedType.Name.Replace("Data", "") + "s";
                    outputSOPath.stringValue = $"Assets/Resources/ScriptableObject/{typeFolderName}";
                }
                if (string.IsNullOrEmpty(profileName.stringValue) && !isEnabled.boolValue)
                {
                    profileName.stringValue = $"{selectedType.Name} Importer";
                    isEnabled.boolValue = true;
                }
            }
        }
        position.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
        EditorGUI.PropertyField(position, csvFile);
        position.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
        EditorGUI.PropertyField(position, outputSOPath);
        EditorGUI.EndProperty();
    }
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return (EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing) * 4;
    }
}