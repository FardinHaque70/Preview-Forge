#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace FardinHaque.ImprovedAssetTools.Editor
{

public static class AppliedSettingsUtility
{
    private static readonly HashSet<string> ExcludedSnapshotFieldNames = new HashSet<string>
    {
        "appliedSettingsJson",
        "appliedRevision",
    };

    public static bool EnsureAppliedSnapshot(ScriptableObject asset, ref string appliedSettingsJson, ref int appliedRevision)
    {
        if (asset == null || !string.IsNullOrEmpty(appliedSettingsJson))
            return false;

        appliedSettingsJson = CaptureSnapshotJson(asset);
        appliedRevision = Mathf.Max(1, appliedRevision);
        return true;
    }

    public static void ApplyCurrentToSnapshot(ScriptableObject asset, ref string appliedSettingsJson, ref int appliedRevision)
    {
        if (asset == null)
            return;

        appliedSettingsJson = CaptureSnapshotJson(asset);
        appliedRevision = Mathf.Max(1, appliedRevision + 1);
    }

    public static void RestoreFromSnapshot(ScriptableObject asset, string appliedSettingsJson)
    {
        if (asset == null || string.IsNullOrEmpty(appliedSettingsJson))
            return;

        ScriptableObject snapshot = CreateSnapshotInstance(asset.GetType());
        try
        {
            EditorJsonUtility.FromJsonOverwrite(appliedSettingsJson, snapshot);
            CopySnapshotFields(snapshot, asset);
        }
        finally
        {
            if (snapshot != null)
                UnityEngine.Object.DestroyImmediate(snapshot);
        }
    }

    public static T CreateAppliedClone<T>(T asset, string appliedSettingsJson) where T : ScriptableObject
    {
        if (asset == null)
            return null;

        T clone = ScriptableObject.CreateInstance<T>();
        clone.hideFlags = HideFlags.HideAndDontSave;

        if (!string.IsNullOrEmpty(appliedSettingsJson))
            EditorJsonUtility.FromJsonOverwrite(appliedSettingsJson, clone);
        else
            CopySnapshotFields(asset, clone);

        return clone;
    }

    public static string CaptureSnapshotJson(ScriptableObject asset)
    {
        if (asset == null)
            return string.Empty;

        ScriptableObject snapshot = CreateSnapshotInstance(asset.GetType());
        try
        {
            CopySnapshotFields(asset, snapshot);
            return EditorJsonUtility.ToJson(snapshot, false);
        }
        finally
        {
            if (snapshot != null)
                UnityEngine.Object.DestroyImmediate(snapshot);
        }
    }

    public static bool HasPendingChanges(SerializedObject currentSerializedObject, SerializedObject appliedSerializedObject, IEnumerable<string> propertyNames)
    {
        if (currentSerializedObject == null || appliedSerializedObject == null || propertyNames == null)
            return false;

        foreach (string propertyName in propertyNames)
        {
            if (PropertyDiffers(currentSerializedObject, appliedSerializedObject, propertyName))
                return true;
        }

        return false;
    }

    public static bool PropertyDiffers(SerializedObject currentSerializedObject, SerializedObject appliedSerializedObject, string propertyName)
    {
        if (currentSerializedObject == null || appliedSerializedObject == null || string.IsNullOrEmpty(propertyName))
            return false;

        SerializedProperty currentProperty = currentSerializedObject.FindProperty(propertyName);
        SerializedProperty appliedProperty = appliedSerializedObject.FindProperty(propertyName);
        if (currentProperty == null || appliedProperty == null)
            return false;

        return PropertyDiffers(currentProperty, appliedProperty);
    }

    public static IReadOnlyList<string> GetTrackedPropertyNames(Type assetType)
    {
        List<string> propertyNames = new List<string>();
        if (assetType == null)
            return propertyNames;

        FieldInfo[] fields = assetType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        for (int i = 0; i < fields.Length; i++)
        {
            FieldInfo field = fields[i];
            if (!ShouldTrackField(field))
                continue;

            propertyNames.Add(field.Name);
        }

        return propertyNames;
    }

    private static bool PropertyDiffers(SerializedProperty currentProperty, SerializedProperty appliedProperty)
    {
        if (currentProperty.propertyType != appliedProperty.propertyType)
            return true;

        switch (currentProperty.propertyType)
        {
            case SerializedPropertyType.Boolean:
                return currentProperty.boolValue != appliedProperty.boolValue;
            case SerializedPropertyType.Integer:
                return currentProperty.intValue != appliedProperty.intValue;
            case SerializedPropertyType.Float:
                return !Mathf.Approximately(currentProperty.floatValue, appliedProperty.floatValue);
            case SerializedPropertyType.Color:
                return currentProperty.colorValue != appliedProperty.colorValue;
            case SerializedPropertyType.Vector2:
                return currentProperty.vector2Value != appliedProperty.vector2Value;
            case SerializedPropertyType.Vector3:
                return currentProperty.vector3Value != appliedProperty.vector3Value;
            case SerializedPropertyType.ObjectReference:
                return currentProperty.objectReferenceValue != appliedProperty.objectReferenceValue;
            case SerializedPropertyType.Enum:
                return currentProperty.enumValueIndex != appliedProperty.enumValueIndex;
            case SerializedPropertyType.Generic:
                return !SerializedProperty.DataEquals(currentProperty, appliedProperty);
            default:
                return false;
        }
    }

    private static ScriptableObject CreateSnapshotInstance(Type assetType)
    {
        ScriptableObject snapshot = ScriptableObject.CreateInstance(assetType);
        snapshot.hideFlags = HideFlags.HideAndDontSave;
        return snapshot;
    }

    private static void CopySnapshotFields(ScriptableObject source, ScriptableObject destination)
    {
        if (source == null || destination == null)
            return;

        FieldInfo[] fields = source.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        for (int i = 0; i < fields.Length; i++)
        {
            FieldInfo field = fields[i];
            if (!ShouldTrackField(field))
                continue;

            field.SetValue(destination, field.GetValue(source));
        }
    }

    private static bool ShouldTrackField(FieldInfo field)
    {
        if (field == null || field.IsStatic || field.IsLiteral || field.IsInitOnly)
            return false;

        if (ExcludedSnapshotFieldNames.Contains(field.Name))
            return false;

        if (field.IsDefined(typeof(NonSerializedAttribute), true))
            return false;

        return field.IsPublic || field.IsDefined(typeof(SerializeField), true);
    }
}
}
#endif
