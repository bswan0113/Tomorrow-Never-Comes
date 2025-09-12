using UnityEngine;
using UnityEditor;
using System.IO;

// 이제 GenericCsvImporter가 아닌, DataImportContainer를 타겟으로 합니다.
[CustomEditor(typeof(DataImportContainer))]
public class DataImportContainerEditor : Editor
{
    private SerializedProperty importedObjectsProp;
    private SerializedProperty pendingReferencesProp;

    private void OnEnable()
    {
        // SerializedProperty를 사용하여 필드를 안전하게 참조합니다.
        importedObjectsProp = serializedObject.FindProperty("importedObjects");
        pendingReferencesProp = serializedObject.FindProperty("pendingReferences");
    }

    public override void OnInspectorGUI()
    {
        // target은 현재 인스펙터에서 보고 있는 DataImportContainer 객체입니다.
        var container = (DataImportContainer)target;

        // 변경 사항을 추적하기 시작합니다.
        serializedObject.Update();

        EditorGUILayout.LabelField("Imported Data Overview", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("This container holds the ScriptableObject assets imported from a CSV file. You can view them below.", MessageType.Info);

        // "importedObjects" 리스트를 인스펙터에 그립니다.
        // true를 전달하여 리스트의 요소들을 확장하여 볼 수 있게 합니다.
        EditorGUILayout.PropertyField(importedObjectsProp, true);

        EditorGUILayout.Space();

        // --- 추가된 부분: Pending References 목록 표시 ---
        EditorGUILayout.LabelField("Pending References (Post-processing)", EditorStyles.boldLabel);
        if (pendingReferencesProp.arraySize > 0)
        {
            EditorGUILayout.HelpBox($"This container has {pendingReferencesProp.arraySize} references pending resolution. They will be processed after all .tncd files are imported.", MessageType.Warning);
            EditorGUILayout.PropertyField(pendingReferencesProp, true); // pendingReferences 리스트 표시
        }
        else
        {
            EditorGUILayout.HelpBox("No pending references. All links should be resolved.", MessageType.Info);
        }
        // --- 추가된 부분 끝 ---

        // 변경된 사항이 있다면 적용합니다. (Undo/Redo 지원)
        serializedObject.ApplyModifiedProperties();

        // --- 추가 기능: 원본 CSV 파일 열기 버튼 ---
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Source File", EditorStyles.boldLabel);
        // 현재 선택된 DataImportContainer 에셋의 경로를 가져옵니다.
        string assetPath = AssetDatabase.GetAssetPath(container);
        if (!string.IsNullOrEmpty(assetPath))
        {
            // ".tncd" 확장자로 변경하여 원본 파일 경로를 추측합니다.
            string csvPath = Path.ChangeExtension(assetPath, ".tncd");
            if (File.Exists(csvPath))
            {
                if (GUILayout.Button("Open Original CSV File"))
                {
                    // 원본 CSV 파일을 시스템 기본 프로그램으로 엽니다.
                    EditorUtility.OpenWithDefaultApp(csvPath);
                }
            }
            else
            {
                EditorGUILayout.HelpBox($"Original CSV file not found at '{csvPath}'. It might have been moved or deleted.", MessageType.Warning);
            }
        }
    }
}