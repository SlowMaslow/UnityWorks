using ClimbUp.Tutorial;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

[CustomEditor(typeof(TutorialSequence))]
public class TutorialSequenceEditor : Editor
{
    private ReorderableList _list;
    private SerializedProperty _stepsProp;

    private void OnEnable()
    {
        _stepsProp = serializedObject.FindProperty("steps");
        _list = new ReorderableList(serializedObject, _stepsProp, true, true, true, true)
        {
            drawHeaderCallback = rect =>
            {
                EditorGUI.LabelField(rect, $"Steps ({_stepsProp.arraySize})");
            },
            elementHeightCallback = idx =>
            {
                var el = _stepsProp.GetArrayElementAtIndex(idx);
                return EditorGUI.GetPropertyHeight(el, GUIContent.none, true)
                     + EditorGUIUtility.standardVerticalSpacing * 2f;
            },
            drawElementCallback = (rect, idx, active, focused) =>
            {
                var el = _stepsProp.GetArrayElementAtIndex(idx);
                var idProp = el.FindPropertyRelative("id");
                string label = string.IsNullOrEmpty(idProp.stringValue)
                    ? $"Step {idx}"
                    : $"{idx}: {idProp.stringValue}";

                rect.y     += EditorGUIUtility.standardVerticalSpacing;
                rect.height = EditorGUI.GetPropertyHeight(el, new GUIContent(label), true);
                EditorGUI.PropertyField(rect, el, new GUIContent(label), true);
            },
            onAddCallback = l =>
            {
                int newIdx = _stepsProp.arraySize;
                _stepsProp.InsertArrayElementAtIndex(newIdx);
                var added = _stepsProp.GetArrayElementAtIndex(newIdx);
                ResetStep(added);
                serializedObject.ApplyModifiedProperties();
            },
        };
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(serializedObject.FindProperty("completeFlag"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("additionalCompleteFlags"), true);
        EditorGUILayout.Space(8f);

        _list.DoLayoutList();

        serializedObject.ApplyModifiedProperties();
    }

    /// <summary>Сбрасывает только что добавленный шаг к чистым дефолтам.</summary>
    private static void ResetStep(SerializedProperty step)
    {
        step.FindPropertyRelative("id").stringValue                  = "";
        step.FindPropertyRelative("hintText").stringValue            = "";
        step.FindPropertyRelative("subText").stringValue             = "";
        step.FindPropertyRelative("autoAdvanceDelay").floatValue     = 0f;
        step.FindPropertyRelative("setFlagOnEnter").stringValue      = "";
        step.FindPropertyRelative("setFlagOnComplete").stringValue   = "";
        step.FindPropertyRelative("skipIfFlagSet").stringValue       = "";
        step.FindPropertyRelative("stopAfterComplete").boolValue     = false;

        // Очистка массивов (highlights и actions)
        var hl = step.FindPropertyRelative("highlights");
        hl.arraySize = 0;
        var act = step.FindPropertyRelative("actions");
        act.arraySize = 0;

        // Свежий condition по умолчанию
        var cond = step.FindPropertyRelative("condition");
        cond.managedReferenceValue = new ManualAdvanceCondition();
    }
}
