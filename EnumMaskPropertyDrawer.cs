/*
Written by: Lucas Antunes (aka ItsaMeTuni), lucasba8@gmail.com
In: 2/15/2018
The only thing that you cannot do with this script is sell it by itself without substantially modifying it.

Updated by Baste Nesse Buanes, baste@rain-games.com (thanks to @lordofduct for GetTargetOfProperty implementation)
06-Sep-2019

Updated to fix undo/dirtying issues and moved to GitHub by @odan-travis 08-Jun-2020
*/

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

using UnityEditor;
[CustomPropertyDrawer(typeof(EnumMaskAttribute))]
public class EnumMaskPropertyDrawer : PropertyDrawer
{
    Dictionary<string, bool> openFoldouts = new Dictionary<string, bool>();

    object theEnum;
    Array enumValues;
    Type enumUnderlyingType;

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        var enumMaskAttribute = ((EnumMaskAttribute) attribute);
        var foldoutOpen = enumMaskAttribute.alwaysFoldOut;

        if (!foldoutOpen)
        {
            if (!openFoldouts.TryGetValue(property.propertyPath, out foldoutOpen))
            {
                openFoldouts[property.propertyPath] = false;
            }
        }
        if (foldoutOpen)
        {
            var layout = ((EnumMaskAttribute) attribute).layout;
            if (layout == EnumMaskLayout.Vertical)
                return EditorGUIUtility.singleLineHeight * (Enum.GetValues(fieldInfo.FieldType).Length + 2);
            else
                return EditorGUIUtility.singleLineHeight * 3;
        }
        else
            return EditorGUIUtility.singleLineHeight;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        object targetObject;
        try
        {
            targetObject = property.serializedObject.targetObject;
            theEnum = fieldInfo.GetValue(targetObject);
        }
        catch (ArgumentException)
        {
            targetObject = GetTargetObjectOfProperty(GetParentProperty(property));
            theEnum = fieldInfo.GetValue(targetObject);
        }

        enumValues = Enum.GetValues(theEnum.GetType());
        enumUnderlyingType = Enum.GetUnderlyingType(theEnum.GetType());

        //We need to convert the enum to its underlying type, if we don't it will be boxed
        //into an object later and then we would need to unbox it like (UnderlyingType)(EnumType)theEnum.
        //If we do this here we can just do (UnderlyingType)theEnum later (plus we can visualize the value of theEnum in VS when debugging)
        theEnum = Convert.ChangeType(theEnum, enumUnderlyingType);

        EditorGUI.BeginProperty(position, label, property);

        var enumMaskAttribute = ((EnumMaskAttribute) attribute);
        var alwaysFoldOut = enumMaskAttribute.alwaysFoldOut;
        var foldoutOpen = alwaysFoldOut;

        if (alwaysFoldOut)
        {
            EditorGUI.LabelField(new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight), label);
        }
        else
        {
            if (!openFoldouts.TryGetValue(property.propertyPath, out foldoutOpen)) {
                openFoldouts[property.propertyPath] = false;
            }

            EditorGUI.BeginChangeCheck();
            foldoutOpen = EditorGUI.Foldout(new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight), foldoutOpen, label);

            if (EditorGUI.EndChangeCheck())
                openFoldouts[property.propertyPath] = foldoutOpen;
        }

        if (foldoutOpen)
        {
            //Draw the All button
            if (GUI.Button(new Rect(position.x + (15f * EditorGUI.indentLevel), position.y + EditorGUIUtility.singleLineHeight * 1, 30, 15), "All"))
            {
                theEnum = DoNotOperator(Convert.ChangeType(0, enumUnderlyingType), enumUnderlyingType);
            }

            //Draw the None button
            if (GUI.Button(new Rect(position.x + 32 + (15f * EditorGUI.indentLevel), position.y + EditorGUIUtility.singleLineHeight * 1, 50, 15), "None"))
            {
                theEnum = Convert.ChangeType(0, enumUnderlyingType);
            }

            var layout = enumMaskAttribute.layout;

            if (layout == EnumMaskLayout.Vertical)
            {
                //Draw the list vertically
                for (int i = 0; i < Enum.GetNames(fieldInfo.FieldType).Length; i++)
                {
                    if (EditorGUI.Toggle(new Rect(position.x, position.y + EditorGUIUtility.singleLineHeight * (2 + i), position.width, EditorGUIUtility.singleLineHeight), Enum.GetNames(fieldInfo.FieldType)[i], IsSet(i)))
                    {
                        ToggleIndex(i, true);
                    }
                    else
                    {
                        ToggleIndex(i, false);
                    }
                }
            }
            else
            {
                var enumNames = Enum.GetNames(fieldInfo.FieldType);

                var style = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleRight,
                    clipping = TextClipping.Overflow
                };

                //Draw the list horizontally
                var labelWidth = 50f;
                for (int i = 0; i < enumNames.Length; i++)
                    labelWidth = Mathf.Max(labelWidth, GUI.skin.label.CalcSize(new GUIContent(enumNames[i])).x);
                var toggleWidth = labelWidth + 20;

                var oldLabelWidth = EditorGUIUtility.labelWidth;
                var oldIndentLevel = EditorGUI.indentLevel; // Toggles kinda are broken at non-zero indent levels, as the indentation eats a part of the clickable rect.

                EditorGUIUtility.labelWidth = labelWidth;
                EditorGUI.indentLevel = 0;

                position.width = toggleWidth;
                position.y += EditorGUIUtility.singleLineHeight * 2;
                var xBase = position.x + oldIndentLevel * 15f;
                for (int i = 0; i < enumNames.Length; i++)
                {
                    position.x = xBase + (i * position.width);
                    var togglePos = EditorGUI.PrefixLabel(position, new GUIContent(enumNames[i]), style);
                    if (EditorGUI.Toggle(togglePos, IsSet(i)))
                    {
                        ToggleIndex(i, true);
                    }
                    else
                    {
                        ToggleIndex(i, false);
                    }
                }

                EditorGUIUtility.labelWidth = oldLabelWidth;
                EditorGUI.indentLevel = oldIndentLevel;
            }
        }

        property.intValue = (int) theEnum;
    }

    /// <summary>
    /// Get the value of an enum element at the specified index (i.e. at the index of the name of the element in the names array)
    /// </summary>
    object GetEnumValue(int _index)
    {
        return Convert.ChangeType(enumValues.GetValue(_index), enumUnderlyingType);
    }

    /// <summary>
    /// Sets or unsets a bit in theEnum based on the index of the enum element (i.e. the index of the element in the names array)
    /// </summary>
    /// <param name="_set">If true the flag will be set, if false the flag will be unset.</param>
    void ToggleIndex(int _index, bool _set)
    {
        if (_set)
        {
            if (IsNoneElement(_index))
            {
                theEnum = Convert.ChangeType(0, enumUnderlyingType);
            }

            //enum = enum | val
            theEnum = DoOrOperator(theEnum, GetEnumValue(_index), enumUnderlyingType);
        }
        else
        {
            if (IsNoneElement(_index) || IsAllElement(_index))
            {
                return;
            }

            object val = GetEnumValue(_index);
            object notVal = DoNotOperator(val, enumUnderlyingType);

            //enum = enum & ~val
            theEnum = DoAndOperator(theEnum, notVal, enumUnderlyingType);
        }

    }

    /// <summary>
    /// Checks if a bit flag is set at the provided index of the enum element (i.e. the index of the element in the names array)
    /// </summary>
    bool IsSet(int _index)
    {
        object val = DoAndOperator(theEnum, GetEnumValue(_index), enumUnderlyingType);

        //We handle All and None elements differently, since they're "special"
        if (IsAllElement(_index))
        {
            //If all other bits visible to the user (elements) are set, the "All" element checkbox has to be checked
            //We don't do a simple AND operation because there might be missing bits.
            //e.g. An enum with 6 elements including the "All" element. If we set all bits visible except the "All" bit,
            //two bits might be unset. Since we want the "All" element checkbox to be checked when all other elements are set
            //we have to make sure those two extra bits are also set.
            bool allSet = true;
            for (int i = 0; i < Enum.GetNames(fieldInfo.FieldType).Length; i++)
            {
                if (i != _index && !IsNoneElement(i) && !IsSet(i))
                {
                    allSet = false;
                    break;
                }
            }

            //Make sure all bits are set if all "visible bits" are set
            if (allSet)
            {
                theEnum = DoNotOperator(Convert.ChangeType(0, enumUnderlyingType), enumUnderlyingType);
            }

            return allSet;
        }
        else if (IsNoneElement(_index))
        {
            //Just check the "None" element checkbox our enum's value is 0
            return Convert.ChangeType(theEnum, enumUnderlyingType).Equals(Convert.ChangeType(0, enumUnderlyingType));
        }

        return !val.Equals(Convert.ChangeType(0, enumUnderlyingType));
    }

    /// <summary>
    /// Call the bitwise OR operator (|) on _lhs and _rhs given their types.
    /// Will basically return _lhs | _rhs
    /// </summary>
    /// <param name="_lhs">Left-hand side of the operation.</param>
    /// <param name="_rhs">Right-hand side of the operation.</param>
    /// <param name="_type">Type of the objects.</param>
    /// <returns>Result of the operation</returns>
    static object DoOrOperator(object _lhs, object _rhs, Type _type)
    {
        if (_type == typeof(int))
        {
            return ((int)_lhs) | ((int)_rhs);
        }
        else if (_type == typeof(uint))
        {
            return ((uint)_lhs) | ((uint)_rhs);
        }
        else if (_type == typeof(short))
        {
            //ushort and short don't have bitwise operators, it is automatically converted to an int, so we convert it back
            return unchecked((short)((short)_lhs | (short)_rhs));
        }
        else if (_type == typeof(ushort))
        {
            //ushort and short don't have bitwise operators, it is automatically converted to an int, so we convert it back
            return unchecked((ushort)((ushort)_lhs | (ushort)_rhs));
        }
        else if (_type == typeof(long))
        {
            return ((long)_lhs) | ((long)_rhs);
        }
        else if (_type == typeof(ulong))
        {
            return ((ulong)_lhs) | ((ulong)_rhs);
        }
        else if (_type == typeof(byte))
        {
            //byte and sbyte don't have bitwise operators, it is automatically converted to an int, so we convert it back
            return unchecked((byte)((byte)_lhs | (byte)_rhs));
        }
        else if (_type == typeof(sbyte))
        {
            //byte and sbyte don't have bitwise operators, it is automatically converted to an int, so we convert it back
            return unchecked((sbyte)((sbyte)_lhs | (sbyte)_rhs));
        }
        else
        {
            throw new System.ArgumentException("Type " + _type.FullName + " not supported.");
        }
    }

    /// <summary>
    /// Call the bitwise AND operator (&) on _lhs and _rhs given their types.
    /// Will basically return _lhs & _rhs
    /// </summary>
    /// <param name="_lhs">Left-hand side of the operation.</param>
    /// <param name="_rhs">Right-hand side of the operation.</param>
    /// <param name="_type">Type of the objects.</param>
    /// <returns>Result of the operation</returns>
    static object DoAndOperator(object _lhs, object _rhs, Type _type)
    {
        if (_type == typeof(int))
        {
            return ((int)_lhs) & ((int)_rhs);
        }
        else if (_type == typeof(uint))
        {
            return ((uint)_lhs) & ((uint)_rhs);
        }
        else if (_type == typeof(short))
        {
            //ushort and short don't have bitwise operators, it is automatically converted to an int, so we convert it back
            return unchecked((short)((short)_lhs & (short)_rhs));
        }
        else if (_type == typeof(ushort))
        {
            //ushort and short don't have bitwise operators, it is automatically converted to an int, so we convert it back
            return unchecked((ushort)((ushort)_lhs & (ushort)_rhs));
        }
        else if (_type == typeof(long))
        {
            return ((long)_lhs) & ((long)_rhs);
        }
        else if (_type == typeof(ulong))
        {
            return ((ulong)_lhs) & ((ulong)_rhs);
        }
        else if (_type == typeof(byte))
        {
            return unchecked((byte)((byte)_lhs & (byte)_rhs));
        }
        else if (_type == typeof(sbyte))
        {
            //byte and sbyte don't have bitwise operators, it is automatically converted to an int, so we convert it back
            return unchecked((sbyte)((sbyte)_lhs & (sbyte)_rhs));
        }
        else
        {
            throw new System.ArgumentException("Type " + _type.FullName + " not supported.");
        }
    }

    /// <summary>
    /// Call the bitwise NOT operator (~) on _lhs given its type.
    /// Will basically return ~_lhs
    /// </summary>
    /// <param name="_lhs">Left-hand side of the operation.</param>
    /// <param name="_type">Type of the object.</param>
    /// <returns>Result of the operation</returns>
    static object DoNotOperator(object _lhs, Type _type)
    {
        if (_type == typeof(int))
        {
            return ~(int)_lhs;
        }
        else if (_type == typeof(uint))
        {
            return ~(uint)_lhs;
        }
        else if (_type == typeof(short))
        {
            //ushort and short don't have bitwise operators, it is automatically converted to an int, so we convert it back
            return unchecked((short)~(short)_lhs);
        }
        else if (_type == typeof(ushort))
        {

            //ushort and short don't have bitwise operators, it is automatically converted to an int, so we convert it back
            return unchecked((ushort)~(ushort)_lhs);
        }
        else if (_type == typeof(long))
        {
            return ~(long)_lhs;
        }
        else if (_type == typeof(ulong))
        {
            return ~(ulong)_lhs;
        }
        else if (_type == typeof(byte))
        {
            //byte and sbyte don't have bitwise operators, it is automatically converted to an int, so we convert it back
            return (byte)~(byte)_lhs;
        }
        else if (_type == typeof(sbyte))
        {
            //byte and sbyte don't have bitwise operators, it is automatically converted to an int, so we convert it back
            return unchecked((sbyte)~(sbyte)_lhs);
        }
        else
        {
            throw new System.ArgumentException("Type " + _type.FullName + " not supported.");
        }
    }

    /// <summary>
    /// Check if the element of specified index is a "None" element (all bits unset, value = 0).
    /// </summary>
    /// <param name="_index">Index of the element.</param>
    /// <returns>If the element has all bits unset or not.</returns>
    bool IsNoneElement(int _index)
    {
        return GetEnumValue(_index).Equals(Convert.ChangeType(0, enumUnderlyingType));
    }

    /// <summary>
    /// Check if the element of specified index is an "All" element (all bits set, value = ~0).
    /// </summary>
    /// <param name="_index">Index of the element.</param>
    /// <returns>If the element has all bits set or not.</returns>
    bool IsAllElement(int _index)
    {
        object elemVal = GetEnumValue(_index);
        return elemVal.Equals(DoNotOperator(Convert.ChangeType(0, enumUnderlyingType), enumUnderlyingType));
    }

    private static object GetTargetObjectOfProperty(SerializedProperty prop)
    {
        var path = prop.propertyPath.Replace(".Array.data[", "[");
        object obj = prop.serializedObject.targetObject;
        var elements = path.Split('.');
        foreach (var element in elements)
        {
            if (element.Contains("["))
            {
                var elementName = element.Substring(0, element.IndexOf("["));
                var index = Convert.ToInt32(element.Substring(element.IndexOf("[")).Replace("[", "").Replace("]", ""));
                obj = GetValue_Imp(obj, elementName, index);
            }
            else
            {
                obj = GetValue_Imp(obj, element);
            }
        }

        return obj;
    }

    private static object GetValue_Imp(object source, string name)
    {
        if (source == null)
            return null;
        var type = source.GetType();

        while (type != null)
        {
            var f = type.GetField(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            if (f != null)
                return f.GetValue(source);

            var p = type.GetProperty(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (p != null)
                return p.GetValue(source, null);

            type = type.BaseType;
        }

        return null;
    }

    private static object GetValue_Imp(object source, string name, int index)
    {
        var enumerable = GetValue_Imp(source, name) as IEnumerable;
        if (enumerable == null)
            return null;
        var enm = enumerable.GetEnumerator();

        for (int i = 0; i <= index; i++)
        {
            if (!enm.MoveNext())
                return null;
        }

        return enm.Current;
    }

    private static SerializedProperty GetParentProperty(SerializedProperty prop)
    {
        var path = prop.propertyPath;
        var parentPathParts = path.Split('.');
        string parentPath = "";
        for (int i = 0; i < parentPathParts.Length - 1; i++)
        {
            parentPath += parentPathParts[i];
            if (i < parentPathParts.Length - 2)
                parentPath += ".";
        }

        var parentProp = prop.serializedObject.FindProperty(parentPath);
        if (parentProp == null)
        {
            Debug.LogError("Couldn't find parent " + parentPath + ", child path is " + prop.propertyPath);
        }

        return parentProp;
    }
}