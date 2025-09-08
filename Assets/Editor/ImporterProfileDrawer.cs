using System;
using System.Linq;
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
            .Where(type => type.IsSubclassOf(typeof(GameData)) && !type.IsAbstract)
            .ToArray();

        gameDataTypeNames = gameDataTypes.Select(type => type.Name).ToArray();
    }

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
                    // 경로를 최종 합의된 구조로 수정합니다.
                    outputSOPath.stringValue = $"Assets/Resources/GameData/{typeFolderName}";
                }

                // ▼▼▼ [핵심] 변경 로직 시작 ▼▼▼
                // 프로필 이름이 비어있고, 프로필이 비활성화 상태일 때만 자동 완성을 실행합니다.
                if (string.IsNullOrEmpty(profileName.stringValue) && !isEnabled.boolValue)
                {
                    // 1. SO 타입 이름을 기반으로 프로필 이름을 자동으로 생성합니다.
                    // 예: DialogueData -> "DialogueData Importer"
                    profileName.stringValue = $"{selectedType.Name} Importer";

                    // 2. isEnabled 체크박스를 자동으로 활성화합니다.
                    isEnabled.boolValue = true;
                }
                // ▲▲▲ [핵심] 변경 로직 끝 ▲▲▲
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