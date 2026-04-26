#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace FardinHaque.ImprovedAssetTools.Editor
{

public static class EditorCompatibilityUtility
{
    private static readonly string[] s_editorAssemblyPreferenceOrder =
    {
        "UnityEditor.CoreModule",
        "UnityEditor",
    };

    private static Type s_gameObjectInspectorType;
    private static Type s_projectBrowserType;
    private static Type s_objectSelectorType;
    private static Type s_advancedObjectSelectorType;
    private static bool s_objectSelectorTypesResolved;
    private static FieldInfo s_projectBrowserSearchFilterField;
    private static PropertyInfo s_searchFilterNameFilterProperty;
    private static FieldInfo s_searchFilterNameFilterField;
    private static PropertyInfo s_searchFilterClassNamesProperty;
    private static FieldInfo s_searchFilterClassNamesField;
    private static PropertyInfo s_searchFilterAssetLabelsProperty;
    private static FieldInfo s_searchFilterAssetLabelsField;
    private static PropertyInfo s_searchFilterFoldersProperty;
    private static FieldInfo s_searchFilterFoldersField;
    private static PropertyInfo s_searchFilterIsSearchingProperty;
    private static MethodInfo s_searchFilterIsSearchingMethod;
    private static PropertyInfo s_expandedProjectWindowItemIdsProperty;
    private static PropertyInfo s_entityIdInstanceIdProperty;
    private static FieldInfo s_projectBrowserViewModeField;
    private static FieldInfo s_projectBrowserListAreaField;
    private static PropertyInfo s_objectListAreaGridSizeProperty;
    private static FieldInfo s_objectListAreaGridSizeField;
    private static int s_projectBrowserListModeCacheFrame = -1;
    private static bool s_projectBrowserListModeCacheValid;
    private static bool s_projectBrowserListModeCacheValue;

    public static void RepaintAllViews()
    {
        InternalEditorUtility.RepaintAllViews();
    }

    public static bool TryGetProjectBrowserClientHeight(out float height)
    {
        height = 0f;
        Type projectBrowserType = GetProjectBrowserType();
        if (projectBrowserType == null)
            return false;

        EditorWindow browser = GetCurrentProjectBrowserWindow(projectBrowserType);
        if (browser == null)
            return false;

        height = browser.position.height;
        return height > 0f;
    }

    public static bool IsProjectBrowserFocusedOrHovered()
    {
        Type projectBrowserType = GetProjectBrowserType();
        if (projectBrowserType == null)
            return false;

        EditorWindow focused = EditorWindow.focusedWindow;
        if (focused != null && projectBrowserType.IsInstanceOfType(focused))
            return true;

        EditorWindow hovered = EditorWindow.mouseOverWindow;
        return hovered != null && projectBrowserType.IsInstanceOfType(hovered);
    }

    public static bool IsProjectWindowSearchActive()
    {
        if (!TryGetProjectBrowserSearchFilter(out object searchFilter))
            return false;

        return IsSearchFilterActive(searchFilter);
    }

    public static bool TryGetProjectWindowSearchCriteria(out string nameFilter, out string[] classNames, out string[] assetLabels)
    {
        nameFilter = string.Empty;
        classNames = Array.Empty<string>();
        assetLabels = Array.Empty<string>();

        if (!TryGetProjectBrowserSearchFilter(out object searchFilter) || !IsSearchFilterActive(searchFilter))
            return false;

        nameFilter = GetSearchFilterString(searchFilter, ref s_searchFilterNameFilterProperty, ref s_searchFilterNameFilterField, "nameFilter");
        classNames = GetSearchFilterStringArray(searchFilter, ref s_searchFilterClassNamesProperty, ref s_searchFilterClassNamesField, "classNames");
        assetLabels = GetSearchFilterStringArray(searchFilter, ref s_searchFilterAssetLabelsProperty, ref s_searchFilterAssetLabelsField, "assetLabels");
        return true;
    }

    public static bool TryGetProjectWindowFolderScopes(out string[] folders)
    {
        folders = Array.Empty<string>();

        if (!TryGetProjectBrowserSearchFilter(out object searchFilter))
            return false;

        folders = GetSearchFilterStringArray(searchFilter, ref s_searchFilterFoldersProperty, ref s_searchFilterFoldersField, "folders");
        return folders.Length > 0;
    }

    private static bool TryGetSearchFilterIsSearching(object searchFilter, out bool isSearching)
    {
        isSearching = false;
        if (searchFilter == null)
            return false;

        Type filterType = searchFilter.GetType();

        if (s_searchFilterIsSearchingProperty == null || s_searchFilterIsSearchingProperty.DeclaringType != filterType)
        {
            s_searchFilterIsSearchingProperty = filterType.GetProperty(
                "isSearching",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        }

        if (s_searchFilterIsSearchingProperty != null)
        {
            object value = s_searchFilterIsSearchingProperty.GetValue(searchFilter);
            if (TryConvertToBool(value, out isSearching))
                return true;
        }

        if (s_searchFilterIsSearchingMethod == null || s_searchFilterIsSearchingMethod.DeclaringType != filterType)
        {
            s_searchFilterIsSearchingMethod = filterType.GetMethod(
                "IsSearching",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                Type.EmptyTypes,
                null);
        }

        if (s_searchFilterIsSearchingMethod == null)
            return false;

        object methodValue = s_searchFilterIsSearchingMethod.Invoke(searchFilter, null);
        return TryConvertToBool(methodValue, out isSearching);
    }

    private static bool HasNonEmptySearchFilterString(object searchFilter, ref PropertyInfo propertyCache, ref FieldInfo fieldCache, string memberName)
    {
        if (!TryGetSearchFilterMemberValue(searchFilter, ref propertyCache, ref fieldCache, memberName, out object value))
            return false;

        return value is string text && !string.IsNullOrWhiteSpace(text);
    }

    private static string GetSearchFilterString(object searchFilter, ref PropertyInfo propertyCache, ref FieldInfo fieldCache, string memberName)
    {
        if (!TryGetSearchFilterMemberValue(searchFilter, ref propertyCache, ref fieldCache, memberName, out object value))
            return string.Empty;

        return value as string ?? string.Empty;
    }

    private static bool HasNonEmptySearchFilterCollection(object searchFilter, ref PropertyInfo propertyCache, ref FieldInfo fieldCache, string memberName)
    {
        if (!TryGetSearchFilterMemberValue(searchFilter, ref propertyCache, ref fieldCache, memberName, out object value))
            return false;

        return HasAnyItem(value);
    }

    private static string[] GetSearchFilterStringArray(object searchFilter, ref PropertyInfo propertyCache, ref FieldInfo fieldCache, string memberName)
    {
        if (!TryGetSearchFilterMemberValue(searchFilter, ref propertyCache, ref fieldCache, memberName, out object value))
            return Array.Empty<string>();

        List<string> results = new List<string>();
        switch (value)
        {
            case string singleValue:
                if (!string.IsNullOrWhiteSpace(singleValue))
                    results.Add(singleValue);
                break;
            case IEnumerable enumerable:
                foreach (object item in enumerable)
                {
                    if (item is string text && !string.IsNullOrWhiteSpace(text))
                        results.Add(text);
                }
                break;
        }

        return results.Count > 0 ? results.ToArray() : Array.Empty<string>();
    }

    private static bool TryGetSearchFilterMemberValue(object searchFilter, ref PropertyInfo propertyCache, ref FieldInfo fieldCache, string memberName, out object value)
    {
        value = null;
        if (searchFilter == null)
            return false;

        Type filterType = searchFilter.GetType();
        if (propertyCache == null || propertyCache.DeclaringType != filterType)
        {
            propertyCache = filterType.GetProperty(
                memberName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        }

        if (propertyCache != null)
        {
            value = propertyCache.GetValue(searchFilter);
            return true;
        }

        if (fieldCache == null || fieldCache.DeclaringType != filterType)
        {
            fieldCache = filterType.GetField(
                memberName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (fieldCache == null)
            {
                string backingFieldName = $"m_{char.ToUpperInvariant(memberName[0])}{memberName.Substring(1)}";
                fieldCache = filterType.GetField(
                    backingFieldName,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            }
        }

        if (fieldCache == null)
            return false;

        value = fieldCache.GetValue(searchFilter);
        return true;
    }

    private static bool HasAnyItem(object value)
    {
        switch (value)
        {
            case null:
                return false;
            case string text:
                return !string.IsNullOrWhiteSpace(text);
            case ICollection collection:
                return collection.Count > 0;
            case IEnumerable enumerable:
                foreach (object _ in enumerable)
                    return true;

                return false;
            default:
                return false;
        }
    }

    private static bool TryGetProjectBrowserSearchFilter(out object searchFilter)
    {
        searchFilter = null;

        Type projectBrowserType = GetProjectBrowserType();
        if (projectBrowserType == null)
            return false;

        EditorWindow browser = GetCurrentProjectBrowserWindow(projectBrowserType);
        if (browser == null)
            return false;

        if (s_projectBrowserSearchFilterField == null)
        {
            s_projectBrowserSearchFilterField = projectBrowserType.GetField(
                "m_SearchFilter",
                BindingFlags.Instance | BindingFlags.NonPublic);
        }

        if (s_projectBrowserSearchFilterField == null)
            return false;

        searchFilter = s_projectBrowserSearchFilterField.GetValue(browser);
        return searchFilter != null;
    }

    private static bool IsSearchFilterActive(object searchFilter)
    {
        if (searchFilter == null)
            return false;

        if (TryGetSearchFilterIsSearching(searchFilter, out bool isSearching))
            return isSearching;

        if (HasNonEmptySearchFilterString(searchFilter, ref s_searchFilterNameFilterProperty, ref s_searchFilterNameFilterField, "nameFilter"))
            return true;

        if (HasNonEmptySearchFilterCollection(searchFilter, ref s_searchFilterClassNamesProperty, ref s_searchFilterClassNamesField, "classNames"))
            return true;

        if (HasNonEmptySearchFilterCollection(searchFilter, ref s_searchFilterAssetLabelsProperty, ref s_searchFilterAssetLabelsField, "assetLabels"))
            return true;

        return false;
    }

    public static void RepaintObjectSelectorWindows()
    {
        if (!s_objectSelectorTypesResolved)
        {
            s_objectSelectorTypesResolved = true;
            s_objectSelectorType = ResolveEditorTypeAcrossAssemblies("UnityEditor.ObjectSelector");
            s_advancedObjectSelectorType = ResolveEditorTypeAcrossAssemblies("UnityEditor.AdvancedObjectSelector");
        }

        bool repainted = false;
        if (s_objectSelectorType != null)
        {
            foreach (UnityEngine.Object obj in Resources.FindObjectsOfTypeAll(s_objectSelectorType))
            {
                if (obj is EditorWindow w) { w.Repaint(); repainted = true; }
            }
        }

        if (!repainted && s_advancedObjectSelectorType != null)
        {
            foreach (UnityEngine.Object obj in Resources.FindObjectsOfTypeAll(s_advancedObjectSelectorType))
            {
                if (obj is EditorWindow w) w.Repaint();
            }
        }
    }

    public static bool IsObjectSelectorWindow(EditorWindow window)
    {
        if (window == null)
            return false;

        if (!s_objectSelectorTypesResolved)
        {
            s_objectSelectorTypesResolved = true;
            s_objectSelectorType = ResolveEditorTypeAcrossAssemblies("UnityEditor.ObjectSelector");
            s_advancedObjectSelectorType = ResolveEditorTypeAcrossAssemblies("UnityEditor.AdvancedObjectSelector");
        }

        if (s_objectSelectorType != null && s_objectSelectorType.IsInstanceOfType(window))
            return true;

        if (s_advancedObjectSelectorType != null && s_advancedObjectSelectorType.IsInstanceOfType(window))
            return true;

        // Fallback: string comparison for unknown Unity versions
        string typeName = window.GetType().Name;
        return typeName == "ObjectSelector" || typeName == "AdvancedObjectSelector";
    }

    public static Type GetBuiltInGameObjectInspectorType()
    {
        if (s_gameObjectInspectorType != null)
            return s_gameObjectInspectorType;

        s_gameObjectInspectorType = ResolveEditorTypeAcrossAssemblies("UnityEditor.GameObjectInspector");
        return s_gameObjectInspectorType;
    }

    public static bool IsProjectBrowserOneColumnLayout()
    {
        Type projectBrowserType = GetProjectBrowserType();
        if (projectBrowserType == null)
            return false;

        // Only read state from a focused or hovered ProjectBrowser. Skipping the
        // Resources.FindObjectsOfTypeAll fallback prevents reading from a background
        // ProjectBrowser when the ObjectSelector is the active window, which would
        // produce the wrong offset for the ObjectSelector's icon layout.
        EditorWindow focused = EditorWindow.focusedWindow;
        EditorWindow browser = focused != null && projectBrowserType.IsInstanceOfType(focused) ? focused : null;
        if (browser == null)
        {
            EditorWindow hovered = EditorWindow.mouseOverWindow;
            browser = hovered != null && projectBrowserType.IsInstanceOfType(hovered) ? hovered : null;
        }

        if (browser == null)
            return false;

        FieldInfo viewModeField = GetProjectBrowserViewModeField(projectBrowserType);
        if (viewModeField == null)
            return false;

        object rawViewMode = viewModeField.GetValue(browser);
        return rawViewMode != null && System.Convert.ToInt32(rawViewMode) == 0;
    }

    public static bool TryGetProjectBrowserListMode(out bool isListMode)
    {
        isListMode = false;

        Type projectBrowserType = GetProjectBrowserType();
        if (projectBrowserType == null)
            return false;

        EditorWindow projectBrowser = GetCurrentProjectBrowserWindow(projectBrowserType);
        if (projectBrowser == null)
            return false;

        FieldInfo viewModeField = GetProjectBrowserViewModeField(projectBrowserType);
        if (viewModeField == null)
            return false;

        object rawViewMode = viewModeField.GetValue(projectBrowser);
        if (rawViewMode == null)
            return false;

        int viewMode = System.Convert.ToInt32(rawViewMode);
        if (viewMode == 0)
        {
            isListMode = true;
            return true;
        }

        if (viewMode != 1)
            return false;

        FieldInfo listAreaField = GetProjectBrowserListAreaField(projectBrowserType);
        if (listAreaField == null)
            return false;

        object listArea = listAreaField.GetValue(projectBrowser);
        if (listArea == null)
            return false;

        if (!TryGetObjectListAreaGridSize(listArea, out float gridSize))
            return false;

        isListMode = gridSize <= 16.1f;
        return true;
    }

    public static bool TryGetProjectBrowserListModeCached(out bool isListMode)
    {
        int frameCount = Time.frameCount;
        if (s_projectBrowserListModeCacheFrame == frameCount)
        {
            isListMode = s_projectBrowserListModeCacheValue;
            return s_projectBrowserListModeCacheValid;
        }

        s_projectBrowserListModeCacheFrame = frameCount;
        s_projectBrowserListModeCacheValid = TryGetProjectBrowserListMode(out s_projectBrowserListModeCacheValue);
        isListMode = s_projectBrowserListModeCacheValue;
        return s_projectBrowserListModeCacheValid;
    }

    public static void GetExpandedProjectWindowItemInstanceIds(ICollection<int> results)
    {
        if (results == null)
            return;

        PropertyInfo property = GetExpandedProjectWindowItemIdsProperty();
        if (property == null)
            return;

        if (!(property.GetValue(null) is IEnumerable expandedItems))
            return;

        foreach (object expandedItem in expandedItems)
        {
            if (TryGetExpandedProjectItemInstanceId(expandedItem, out int instanceId))
                results.Add(instanceId);
        }
    }

    private static PropertyInfo GetExpandedProjectWindowItemIdsProperty()
    {
        if (s_expandedProjectWindowItemIdsProperty != null)
            return s_expandedProjectWindowItemIdsProperty;

        s_expandedProjectWindowItemIdsProperty = typeof(InternalEditorUtility).GetProperty(
            "expandedProjectWindowItemIds",
            BindingFlags.Public | BindingFlags.Static);

        return s_expandedProjectWindowItemIdsProperty;
    }

    private static Type GetProjectBrowserType()
    {
        if (s_projectBrowserType != null)
            return s_projectBrowserType;

        s_projectBrowserType = ResolveEditorTypeAcrossAssemblies("UnityEditor.ProjectBrowser");
        return s_projectBrowserType;
    }

    private static Type ResolveEditorTypeAcrossAssemblies(string fullTypeName)
    {
        if (string.IsNullOrEmpty(fullTypeName))
            return null;

        // Fast path: known assembly names that differ across Unity versions.
        for (int i = 0; i < s_editorAssemblyPreferenceOrder.Length; i++)
        {
            Type type = Type.GetType($"{fullTypeName}, {s_editorAssemblyPreferenceOrder[i]}");
            if (type != null)
                return type;
        }

        // Fallback: scan loaded assemblies for unusual/editor-internal relocation.
        Assembly[] loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies();
        for (int i = 0; i < loadedAssemblies.Length; i++)
        {
            Type type = loadedAssemblies[i].GetType(fullTypeName, false);
            if (type != null)
                return type;
        }

        return null;
    }

    private static EditorWindow GetCurrentProjectBrowserWindow(Type projectBrowserType)
    {
        EditorWindow focused = EditorWindow.focusedWindow;
        if (focused != null && projectBrowserType.IsInstanceOfType(focused))
            return focused;

        EditorWindow hovered = EditorWindow.mouseOverWindow;
        if (hovered != null && projectBrowserType.IsInstanceOfType(hovered))
            return hovered;

        UnityEngine.Object[] projectBrowsers = Resources.FindObjectsOfTypeAll(projectBrowserType);
        return projectBrowsers != null && projectBrowsers.Length > 0
            ? projectBrowsers[0] as EditorWindow
            : null;
    }

    private static FieldInfo GetProjectBrowserViewModeField(Type projectBrowserType)
    {
        if (s_projectBrowserViewModeField != null)
            return s_projectBrowserViewModeField;

        s_projectBrowserViewModeField = projectBrowserType.GetField(
            "m_ViewMode",
            BindingFlags.Instance | BindingFlags.NonPublic);

        return s_projectBrowserViewModeField;
    }

    private static FieldInfo GetProjectBrowserListAreaField(Type projectBrowserType)
    {
        if (s_projectBrowserListAreaField != null)
            return s_projectBrowserListAreaField;

        s_projectBrowserListAreaField = projectBrowserType.GetField(
            "m_ListArea",
            BindingFlags.Instance | BindingFlags.NonPublic);

        return s_projectBrowserListAreaField;
    }

    private static bool TryGetObjectListAreaGridSize(object listArea, out float gridSize)
    {
        gridSize = 0f;
        if (listArea == null)
            return false;

        Type listAreaType = listArea.GetType();
        if (s_objectListAreaGridSizeProperty == null || s_objectListAreaGridSizeProperty.DeclaringType != listAreaType)
        {
            s_objectListAreaGridSizeProperty = listAreaType.GetProperty(
                "gridSize",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        }

        if (s_objectListAreaGridSizeProperty != null)
        {
            object rawValue = s_objectListAreaGridSizeProperty.GetValue(listArea);
            if (TryConvertToFloat(rawValue, out gridSize))
                return true;
        }

        if (s_objectListAreaGridSizeField == null || s_objectListAreaGridSizeField.DeclaringType != listAreaType)
        {
            s_objectListAreaGridSizeField = listAreaType.GetField(
                "gridSize",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        }

        if (s_objectListAreaGridSizeField == null)
            return false;

        return TryConvertToFloat(s_objectListAreaGridSizeField.GetValue(listArea), out gridSize);
    }

    private static bool TryConvertToFloat(object value, out float result)
    {
        result = 0f;
        if (value == null)
            return false;

        switch (value)
        {
            case float floatValue:
                result = floatValue;
                return true;
            case int intValue:
                result = intValue;
                return true;
            case double doubleValue:
                result = (float)doubleValue;
                return true;
            default:
                return false;
        }
    }

    private static bool TryConvertToBool(object value, out bool result)
    {
        result = false;
        if (value == null)
            return false;

        switch (value)
        {
            case bool boolValue:
                result = boolValue;
                return true;
            case int intValue:
                result = intValue != 0;
                return true;
            default:
                return false;
        }
    }

    private static bool TryGetExpandedProjectItemInstanceId(object expandedItem, out int instanceId)
    {
        instanceId = 0;
        if (expandedItem == null)
            return false;

        if (expandedItem is int rawInstanceId)
        {
            instanceId = rawInstanceId;
            return true;
        }

        PropertyInfo entityIdProperty = GetEntityIdInstanceIdProperty(expandedItem.GetType());
        if (entityIdProperty == null)
            return false;

        object value = entityIdProperty.GetValue(expandedItem);
        if (value is int reflectedInstanceId)
        {
            instanceId = reflectedInstanceId;
            return true;
        }

        return false;
    }

    private static PropertyInfo GetEntityIdInstanceIdProperty(Type expandedItemType)
    {
        if (s_entityIdInstanceIdProperty != null)
            return s_entityIdInstanceIdProperty;

        if (expandedItemType == null)
            return null;

        s_entityIdInstanceIdProperty = expandedItemType.GetProperty(
            "instanceId",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        return s_entityIdInstanceIdProperty;
    }
}
}
#endif
