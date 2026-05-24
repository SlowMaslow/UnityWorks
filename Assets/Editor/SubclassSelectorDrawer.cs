using System;
using System.Collections.Generic;
using System.Linq;
using ClimbUp.Tutorial;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(SubclassSelectorAttribute))]
public class SubclassSelectorDrawer : PropertyDrawer
{
    private static readonly Dictionary<Type, Type[]> _subclassCache = new Dictionary<Type, Type[]>();

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (property.propertyType != SerializedPropertyType.ManagedReference)
        {
            EditorGUI.PropertyField(position, property, label, true);
            return;
        }

        // ── 1. Хедер: foldout + label + dropdown с типом ─────────────────────
        var headerRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);

        string currentTypename = property.managedReferenceFullTypename;
        string currentLabel = string.IsNullOrEmpty(currentTypename)
            ? "<null>"
            : ShortTypeName(currentTypename);

        // Foldout + label слева
        var foldoutRect = new Rect(headerRect.x, headerRect.y,
            EditorGUIUtility.labelWidth, headerRect.height);
        var dropdownRect = new Rect(headerRect.x + EditorGUIUtility.labelWidth, headerRect.y,
            headerRect.width - EditorGUIUtility.labelWidth, headerRect.height);

        // Только показываем foldout если значение non-null (иначе нет что разворачивать)
        bool hasValue = !string.IsNullOrEmpty(currentTypename);
        if (hasValue)
            property.isExpanded = EditorGUI.Foldout(foldoutRect, property.isExpanded, label, true);
        else
            EditorGUI.LabelField(foldoutRect, label);

        // Dropdown справа от label
        if (EditorGUI.DropdownButton(dropdownRect, new GUIContent(currentLabel), FocusType.Keyboard))
        {
            ShowMenu(property);
        }

        // ── 2. Дочерние поля при isExpanded ──────────────────────────────────
        if (hasValue && property.isExpanded)
        {
            EditorGUI.indentLevel++;
            float y = position.y + EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            var endProp = property.GetEndProperty();
            var iter = property.Copy();
            bool entered = iter.NextVisible(true);
            while (entered && !SerializedProperty.EqualContents(iter, endProp))
            {
                float h = EditorGUI.GetPropertyHeight(iter, true);
                EditorGUI.PropertyField(new Rect(position.x, y, position.width, h), iter, true);
                y += h + EditorGUIUtility.standardVerticalSpacing;
                entered = iter.NextVisible(false);
            }
            EditorGUI.indentLevel--;
        }
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        if (property.propertyType != SerializedPropertyType.ManagedReference)
            return EditorGUI.GetPropertyHeight(property, label, true);

        float h = EditorGUIUtility.singleLineHeight;
        bool hasValue = !string.IsNullOrEmpty(property.managedReferenceFullTypename);
        if (hasValue && property.isExpanded)
        {
            var endProp = property.GetEndProperty();
            var iter = property.Copy();
            bool entered = iter.NextVisible(true);
            while (entered && !SerializedProperty.EqualContents(iter, endProp))
            {
                h += EditorGUIUtility.standardVerticalSpacing
                   + EditorGUI.GetPropertyHeight(iter, true);
                entered = iter.NextVisible(false);
            }
        }
        return h;
    }

    private void ShowMenu(SerializedProperty property)
    {
        Type fieldType = GetReferenceType();
        if (fieldType == null) return;

        var menu = new GenericMenu();
        menu.AddItem(new GUIContent("<null>"), false, () =>
        {
            property.serializedObject.Update();
            property.managedReferenceValue = null;
            property.serializedObject.ApplyModifiedProperties();
        });

        foreach (var t in GetSubclasses(fieldType))
        {
            var typeRef = t;
            menu.AddItem(new GUIContent(t.Name), false, () =>
            {
                property.serializedObject.Update();
                property.managedReferenceValue = Activator.CreateInstance(typeRef);
                property.serializedObject.ApplyModifiedProperties();
            });
        }

        menu.ShowAsContext();
    }

    private Type GetReferenceType()
    {
        // fieldInfo может быть для массива/листа — нужен тип элемента
        var ft = fieldInfo.FieldType;
        if (ft.IsArray) return ft.GetElementType();
        if (ft.IsGenericType && ft.GetGenericTypeDefinition() == typeof(List<>))
            return ft.GetGenericArguments()[0];
        return ft;
    }

    private static Type[] GetSubclasses(Type baseType)
    {
        if (_subclassCache.TryGetValue(baseType, out var cached)) return cached;
        var list = new List<Type>();
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type[] types;
            try { types = asm.GetTypes(); }
            catch { continue; }
            foreach (var t in types)
            {
                if (t == null || t.IsAbstract || t.IsInterface) continue;
                if (!baseType.IsAssignableFrom(t)) continue;
                if (t == baseType) continue;
                if (t.IsSubclassOf(typeof(UnityEngine.Object))) continue; // только plain classes
                list.Add(t);
            }
        }
        var arr = list.OrderBy(t => t.Name).ToArray();
        _subclassCache[baseType] = arr;
        return arr;
    }

    private static string ShortTypeName(string fullTypename)
    {
        // managedReferenceFullTypename = "Assembly Type" — берём чисто имя класса
        int space = fullTypename.IndexOf(' ');
        string type = space >= 0 ? fullTypename.Substring(space + 1) : fullTypename;
        int dot = type.LastIndexOf('.');
        return dot >= 0 ? type.Substring(dot + 1) : type;
    }
}
