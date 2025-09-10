using UnityEngine;
using UnityEditor;
using System.Reflection;
using System.Linq;
using System;
using Features.Player;

[CustomEditor(typeof(StatCheckCondition))]
public class StatCheckConditionEditor : Editor
{
    private string[] statNames;
    private SerializedProperty targetStatNameProp;

    private void OnEnable()
    {
        // PlayerStatus 클래스에서 long 또는 int 타입의 public 프로퍼티만 가져옵니다.
        statNames = typeof(PlayerStatsData)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(prop => prop.PropertyType == typeof(long) || prop.PropertyType == typeof(int))
            .Select(prop => prop.Name)
            .ToArray();

        // 우리가 '특별 취급'할 프로퍼티만 찾습니다.
        targetStatNameProp = serializedObject.FindProperty("targetStatName");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // --- 1. 우리가 직접 커스텀으로 그릴 부분 ---
        string currentStatName = targetStatNameProp.stringValue;
        int selectedIndex = Array.IndexOf(statNames, currentStatName);
        if (selectedIndex < 0) selectedIndex = 0;

        int newIndex = EditorGUILayout.Popup("Target Stat", selectedIndex, statNames);

        if (newIndex != selectedIndex)
        {
            targetStatNameProp.stringValue = statNames[newIndex];
        }

        // --- 2. 나머지 모든 필드를 자동으로 그리는 부분 (핵심!) ---
        // "m_Script"는 인스펙터 상단의 회색 처리된 'Script' 필드를 의미합니다.
        // "targetStatName"은 우리가 위에서 직접 그렸으므로 제외합니다.
        DrawPropertiesExcluding(serializedObject, new string[] { "m_Script", "targetStatName" });

        serializedObject.ApplyModifiedProperties();
    }
}