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
    private string lastCheckedTargetTypeName;

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

        BuildTypeCache();
    }

    public override void OnDisable()
    {
        base.OnDisable();
    }

    private void BuildTypeCache()
    {
        if (gameDataTypes != null) return;
        gameDataTypes = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(assembly => {
                try { return assembly.GetTypes(); }
                catch (ReflectionTypeLoadException) { return new Type[0]; }
            })
            .Where(t => t.IsClass && !t.IsAbstract && typeof(GameData).IsAssignableFrom(t))
            .OrderBy(t => t.FullName)
            .ToList();
        gameDataTypeNames = gameDataTypes.Select(t => t.FullName.Replace('.', '/')).ToArray();
    }

    private void BuildGroupedFieldCache(Type targetType)
    {
        cachedListFields = targetType?.GetFields(BindingFlags.Public | BindingFlags.Instance)
            .Where(f => typeof(IList).IsAssignableFrom(f.FieldType))
            .ToList() ?? new List<FieldInfo>();
        cachedListFieldNames = cachedListFields.Select(f => f.Name).ToArray();
        lastCheckedTargetTypeName = targetType?.AssemblyQualifiedName;
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
            if (targetTypeProp.stringValue != lastCheckedTargetTypeName)
            {
                BuildGroupedFieldCache(Type.GetType(targetTypeProp.stringValue));
            }
            DrawGroupedStrategySettings();
        }

        serializedObject.ApplyModifiedProperties();
        ApplyRevertGUI();
    }

    private void AutoConfigureGroupedFields()
    {
        Type targetType = Type.GetType(targetTypeProp.stringValue);
        if (targetType == null) return;
        BuildGroupedFieldCache(targetType);

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

    private void DrawMainTypeSelector()
    {
        EditorGUILayout.LabelField("Target ScriptableObject Type", EditorStyles.boldLabel);
        int currentIndex = -1;
        if (!string.IsNullOrEmpty(targetTypeProp.stringValue))
        {
            currentIndex = gameDataTypes.FindIndex(t => t.AssemblyQualifiedName == targetTypeProp.stringValue);
        }
        int newIndex = EditorGUILayout.Popup("Type", currentIndex, gameDataTypeNames);
        if (newIndex != currentIndex)
        {
            // ▼▼▼ 여기가 핵심 수정사항입니다. 객체를 직접 건드리지 않습니다. ▼▼▼
            targetTypeProp.stringValue = gameDataTypes[newIndex].AssemblyQualifiedName;
            listFieldProp.stringValue = null;
            listItemTypeProp.stringValue = null;
        }
        if (GUILayout.Button("Refresh Type List"))
        {
            gameDataTypes = null;
            BuildTypeCache();
        }
    }

    private void DrawGroupedStrategySettings()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Grouped Strategy Settings", EditorStyles.boldLabel);
        if (cachedListFieldNames == null || cachedListFieldNames.Length == 0)
        {
            EditorGUILayout.HelpBox("Selected Target Type has no List or Array fields.", MessageType.Warning);
            return;
        }
        int listFieldIndex = Array.IndexOf(cachedListFieldNames, listFieldProp.stringValue);
        int newListFieldIndex = EditorGUILayout.Popup("List Field", listFieldIndex, cachedListFieldNames);
        if (newListFieldIndex != listFieldIndex)
        {
            FieldInfo selectedField = cachedListFields[newListFieldIndex];
            // ▼▼▼ 여기도 핵심 수정사항입니다. ▼▼▼
            listFieldProp.stringValue = selectedField.Name;

            Type itemType = selectedField.FieldType.IsArray
                ? selectedField.FieldType.GetElementType()
                : selectedField.FieldType.GetGenericArguments()[0];
            listItemTypeProp.stringValue = itemType.AssemblyQualifiedName;
        }
        GUI.enabled = false;
        EditorGUILayout.PropertyField(listItemTypeProp, new GUIContent("List Item Type"));
        GUI.enabled = true;
    }
}