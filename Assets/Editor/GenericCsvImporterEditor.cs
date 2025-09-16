using UnityEngine;
using UnityEditor;
#if UNITY_2020_2_OR_NEWER
using UnityEditor.AssetImporters;
#else
using UnityEditor.Experimental.AssetImporters;
#endif
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ScriptableObjects.Abstract;

/// <summary>
/// GenericCsvImporter의 인스펙터 UI를 대폭 개선합니다.
/// SerializedObject를 사용하여 값을 안전하게 변경함으로써 Apply 시 설정이 초기화되는 문제를 해결합니다.
/// </summary>
[CustomEditor(typeof(GenericCsvImporter))]
public class GenericCsvImporterEditor : ScriptedImporterEditor
{
    private List<Type> gameDataTypes;
    private string[] gameDataTypeNames;
    private List<FieldInfo> cachedListFields;
    private string[] cachedListFieldNames;

    // 현재 선택된 타겟 SO 타입을 캐싱하여 Type.GetType 호출을 줄입니다.
    private Type currentTargetType;
    private string lastCheckedTargetTypeName; // currentTargetType이 마지막으로 갱신된 AssemblyQualifiedName

    private SerializedProperty targetTypeProp;
    private SerializedProperty strategyProp;
    private SerializedProperty listFieldProp;
    private SerializedProperty listItemTypeProp;

    public override void OnEnable()
    {
        base.OnEnable();

        targetTypeProp = serializedObject.FindProperty("targetTypeAssemblyQualifiedName");
        strategyProp = serializedObject.FindProperty("strategy");
        listFieldProp = serializedObject.FindProperty("groupedListField");
        listItemTypeProp = serializedObject.FindProperty("groupedListItemTypeAssemblyQualifiedName");

        // GameData 타입 캐시 빌드 (OnEnable 시 한 번만)
        BuildTypeCache();

        // 현재 선택된 타겟 타입에 따라 Grouped 필드 캐시 초기화
        UpdateCurrentTargetTypeAndGroupedFieldCache(targetTypeProp.stringValue);
    }

    // OnDisable은 현재 특별히 할 일이 없으므로 제거 (필요 시 다시 추가)
    // public override void OnDisable() { base.OnDisable(); }

    /// <summary>
    /// 프로젝트 내의 모든 GameData 상속 타입을 찾아 캐싱합니다.
    /// </summary>
    private void BuildTypeCache()
    {
        if (gameDataTypes != null) return; // 이미 캐시된 경우 재빌드하지 않음

        gameDataTypes = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(assembly => {
                try { return assembly.GetTypes(); }
                catch (ReflectionTypeLoadException) { return new Type[0]; } // 로드 실패 시 빈 배열 반환
            })
            .Where(t => t.IsClass && !t.IsAbstract && typeof(GameData).IsAssignableFrom(t))
            .OrderBy(t => t.FullName)
            .ToList();
        gameDataTypeNames = gameDataTypes.Select(t => t.FullName.Replace('.', '/')).ToArray();
    }

    /// <summary>
    /// 타겟 SO 타입이 변경되면 관련 리스트 필드 캐시를 재빌드합니다.
    /// </summary>
    private void BuildGroupedFieldCache(Type targetType)
    {
        if (targetType == null)
        {
            cachedListFields = new List<FieldInfo>();
            cachedListFieldNames = new string[0];
            return;
        }

        cachedListFields = targetType.GetFields(BindingFlags.Public | BindingFlags.Instance)
            .Where(f => typeof(IList).IsAssignableFrom(f.FieldType))
            .ToList();
        cachedListFieldNames = cachedListFields.Select(f => f.Name).ToArray();
    }

    /// <summary>
    /// 현재 타겟 타입을 업데이트하고 필요한 경우 그룹화된 필드 캐시를 재빌드합니다.
    /// </summary>
    private void UpdateCurrentTargetTypeAndGroupedFieldCache(string newTargetTypeAssemblyQualifiedName)
    {
        if (newTargetTypeAssemblyQualifiedName != lastCheckedTargetTypeName)
        {
            currentTargetType = Type.GetType(newTargetTypeAssemblyQualifiedName);
            lastCheckedTargetTypeName = newTargetTypeAssemblyQualifiedName;
            BuildGroupedFieldCache(currentTargetType);
        }
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawMainTypeSelector();
        EditorGUILayout.Space(10);

        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(strategyProp);
        if (EditorGUI.EndChangeCheck())
        {
            if ((GenericCsvImporter.ImportStrategy)strategyProp.enumValueIndex == GenericCsvImporter.ImportStrategy.GroupedById)
            {
                AutoConfigureGroupedFields();
            }
        }

        if ((GenericCsvImporter.ImportStrategy)strategyProp.enumValueIndex == GenericCsvImporter.ImportStrategy.GroupedById)
        {
            // currentTargetType이 변경되었을 수 있으므로 항상 최신 상태를 유지
            UpdateCurrentTargetTypeAndGroupedFieldCache(targetTypeProp.stringValue);
            DrawGroupedStrategySettings();
        }

        serializedObject.ApplyModifiedProperties();
        ApplyRevertGUI();
    }

    /// <summary>
    /// GroupedById 전략 선택 시 리스트 필드를 자동으로 설정합니다.
    /// </summary>
    private void AutoConfigureGroupedFields()
    {
        if (currentTargetType == null) return;

        // AutoConfigure 시에도 캐시가 최신인지 확인
        BuildGroupedFieldCache(currentTargetType);

        if (cachedListFields != null && cachedListFields.Count > 0)
        {
            FieldInfo defaultField = cachedListFields[0];
            listFieldProp.stringValue = defaultField.Name;

            Type itemType = defaultField.FieldType.IsArray
                ? defaultField.FieldType.GetElementType()
                : defaultField.FieldType.GetGenericArguments()[0];
            listItemTypeProp.stringValue = itemType.AssemblyQualifiedName;
        }
    }

    /// <summary>
    /// 메인 ScriptableObject 타입 선택 UI를 그립니다.
    /// </summary>
    private void DrawMainTypeSelector()
    {
        EditorGUILayout.LabelField("Target ScriptableObject Type", EditorStyles.boldLabel);
        int currentIndex = -1;
        if (!string.IsNullOrEmpty(targetTypeProp.stringValue) && gameDataTypes != null)
        {
            currentIndex = gameDataTypes.FindIndex(t => t.AssemblyQualifiedName == targetTypeProp.stringValue);
        }

        EditorGUI.BeginChangeCheck();
        int newIndex = EditorGUILayout.Popup("Type", currentIndex, gameDataTypeNames);
        if (EditorGUI.EndChangeCheck())
        {
            // 선택이 변경되면 SerializedProperty 값을 업데이트하고 관련 캐시를 무효화
            if (newIndex >= 0 && newIndex < gameDataTypes.Count)
            {
                targetTypeProp.stringValue = gameDataTypes[newIndex].AssemblyQualifiedName;
                listFieldProp.stringValue = null; // 타겟 타입이 바뀌면 리스트 필드 설정 초기화
                listItemTypeProp.stringValue = null; // 타겟 타입이 바뀌면 리스트 아이템 타입 설정 초기화

                // 캐싱된 타겟 타입을 즉시 업데이트하고 관련 그룹화 필드 캐시도 재빌드
                UpdateCurrentTargetTypeAndGroupedFieldCache(targetTypeProp.stringValue);
            }
            else // "None" 또는 유효하지 않은 선택 시
            {
                targetTypeProp.stringValue = "";
                listFieldProp.stringValue = null;
                listItemTypeProp.stringValue = null;
                UpdateCurrentTargetTypeAndGroupedFieldCache(""); // 캐시 초기화
            }
        }

        if (GUILayout.Button("Refresh Type List"))
        {
            gameDataTypes = null; // 캐시 무효화
            BuildTypeCache(); // 캐시 재빌드
            UpdateCurrentTargetTypeAndGroupedFieldCache(targetTypeProp.stringValue); // 현재 선택된 타입으로 그룹화 필드 캐시도 갱신
        }
    }

    /// <summary>
    /// 그룹화 전략에 대한 추가 설정 UI를 그립니다.
    /// </summary>
    private void DrawGroupedStrategySettings()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Grouped Strategy Settings", EditorStyles.boldLabel);

        if (currentTargetType == null)
        {
            EditorGUILayout.HelpBox("Please select a 'Target ScriptableObject Type' first.", MessageType.Warning);
            return;
        }

        if (cachedListFieldNames == null || cachedListFieldNames.Length == 0)
        {
            EditorGUILayout.HelpBox($"Selected Target Type '{currentTargetType.Name}' has no public List or Array fields.", MessageType.Warning);
            return;
        }

        int listFieldIndex = Array.IndexOf(cachedListFieldNames, listFieldProp.stringValue);
        EditorGUI.BeginChangeCheck();
        int newListFieldIndex = EditorGUILayout.Popup("List Field", listFieldIndex, cachedListFieldNames);
        if (EditorGUI.EndChangeCheck())
        {
            if (newListFieldIndex >= 0 && newListFieldIndex < cachedListFields.Count)
            {
                FieldInfo selectedField = cachedListFields[newListFieldIndex];
                listFieldProp.stringValue = selectedField.Name;

                Type itemType = selectedField.FieldType.IsArray
                    ? selectedField.FieldType.GetElementType()
                    : selectedField.FieldType.GetGenericArguments()[0];
                listItemTypeProp.stringValue = itemType.AssemblyQualifiedName;
            }
            else // "None" 또는 유효하지 않은 선택 시
            {
                listFieldProp.stringValue = null;
                listItemTypeProp.stringValue = null;
            }
        }

        // List Item Type은 자동으로 설정되므로 편집 불가능하게 표시
        GUI.enabled = false;
        EditorGUILayout.PropertyField(listItemTypeProp, new GUIContent("List Item Type"));
        GUI.enabled = true;
    }
}