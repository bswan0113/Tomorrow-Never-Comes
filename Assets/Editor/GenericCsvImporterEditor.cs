// C:\Workspace\Tomorrow Never Comes\Assets\Editor\GenericCsvImporterEditor.cs
using UnityEditor;
using UnityEditor.AssetImporters;
using System;
using System.Linq;
using UnityEngine;

[CustomEditor(typeof(GenericCsvImporter))]
public class GenericCsvImporterEditor : ScriptedImporterEditor
{
    private static Type[] gameDataTypes;
    private static string[] gameDataTypeNames;
    private static readonly Type baseDataType = typeof(GameData);

    // SerializedProperty는 직렬화된 객체의 속성을 안전하게 다루기 위한 Wrapper 클래스입니다.
    private SerializedProperty targetTypeAssemblyQualifiedNameProp;

    static GenericCsvImporterEditor()
    {
        // 이 부분은 기존과 동일합니다.
        try
        {
            gameDataTypes = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(assembly => assembly.GetTypes())
                .Where(type => type.IsSubclassOf(baseDataType) && !type.IsAbstract)
                .ToArray();

            if (gameDataTypes.Length == 0)
            {
                Debug.LogWarning($"[GenericCsvImporterEditor] '{baseDataType.Name}'를 상속받는 SO 타입을 찾지 못했습니다.");
            }
            gameDataTypeNames = new[] { "None" }.Concat(gameDataTypes.Select(type => type.Name)).ToArray();
        }
        catch (Exception e)
        {
            Debug.LogError($"[GenericCsvImporterEditor] Static Initializer FAILED: {e.Message}");
        }
    }

    // OnEnable은 Inspector가 활성화될 때마다 호출됩니다.
    // 여기서 SerializedProperty를 찾아 초기화하는 것이 표준 방식입니다.
    public override void OnEnable()
    {
        base.OnEnable(); // 반드시 부모의 OnEnable을 호출해야 합니다.
        // "targetTypeAssemblyQualifiedName" 이라는 이름의 속성을 찾아서 변수에 할당합니다.
        // 이 이름은 GenericCsvImporter.cs에 선언된 public 변수 이름과 정확히 일치해야 합니다.
        targetTypeAssemblyQualifiedNameProp = serializedObject.FindProperty("targetTypeAssemblyQualifiedName");
    }

    public override void OnInspectorGUI()
    {
        // serializedObject.Update() : Inspector를 그리기 전에 최신 데이터로 업데이트합니다.
        serializedObject.Update();

        EditorGUILayout.LabelField("CSV to ScriptableObject Importer", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Select the type of ScriptableObject to generate from this CSV file.", MessageType.Info);

        // 현재 설정된 값을 기반으로 드롭다운 메뉴의 인덱스를 찾습니다.
        int currentIndex = 0;
        var currentTypeValue = targetTypeAssemblyQualifiedNameProp.stringValue;
        if (!string.IsNullOrEmpty(currentTypeValue))
        {
            Type currentType = Type.GetType(currentTypeValue);
            currentIndex = Array.IndexOf(gameDataTypes, currentType) + 1;
        }

        // --- ▼▼▼ 핵심 수정 부분 ▼▼▼ ---
        EditorGUI.BeginChangeCheck();

        int selectedIndex = EditorGUILayout.Popup("Target SO Type", currentIndex, gameDataTypeNames);

        if (EditorGUI.EndChangeCheck())
        {
            // 사용자가 선택을 변경하면, target 객체를 직접 수정하는 대신 SerializedProperty의 값을 변경합니다.
            if (selectedIndex <= 0 || selectedIndex > gameDataTypes.Length)
            {
                targetTypeAssemblyQualifiedNameProp.stringValue = null;
            }
            else
            {
                targetTypeAssemblyQualifiedNameProp.stringValue = gameDataTypes[selectedIndex - 1].AssemblyQualifiedName;
            }
        }

        // serializedObject.ApplyModifiedProperties() : 변경된 속성을 실제 객체에 적용합니다.
        // 이 과정이 내부적으로 Undo 등록 및 Dirty 상태 설정을 처리해 줍니다.
        serializedObject.ApplyModifiedProperties();

        // ApplyRevertGUI()는 변경 사항이 있을 때만 버튼을 활성화합니다.
        // ApplyModifiedProperties()와 함께 사용되어야 합니다.
        ApplyRevertGUI();
        // --- ▲▲▲ 핵심 수정 부분 ▲▲▲ ---
    }
}