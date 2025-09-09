using UnityEngine;
using UnityEditor;
using System.IO;

// 이제 GenericCsvImporter가 아닌, DataImportContainer를 타겟으로 합니다.
[CustomEditor(typeof(DataImportContainer))]
public class DataImportContainerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        // target은 현재 인스펙터에서 보고 있는 DataImportContainer 객체입니다.
        var container = (DataImportContainer)target;

        // 변경 사항을 추적하기 시작합니다.
        serializedObject.Update();

        EditorGUILayout.LabelField("Imported Data", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("You can view and modify the imported ScriptableObjects here.", MessageType.Info);

        // "importedObjects"라는 이름의 리스트 프로퍼티를 찾습니다.
        SerializedProperty listProperty = serializedObject.FindProperty("importedObjects");

        // 리스트를 인스펙터에 그립니다.
        EditorGUILayout.PropertyField(listProperty, true);

        // 변경된 사항이 있다면 적용합니다. (Undo/Redo 지원)
        serializedObject.ApplyModifiedProperties();

        // --- 추가 기능: 원본 CSV 파일 열기 버튼 ---
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
        }
    }
}