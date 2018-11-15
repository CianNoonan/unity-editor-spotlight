using System.Collections.Generic;
using System.Linq;
using System;
using UnityEditor;
using UnityEngine;

public static class EditorWindowExtensions
{
    public static Type[] GetAllDerivedTypes(this AppDomain aAppDomain, Type aType)
    {
        var result = new List<Type>();
        var assemblies = aAppDomain.GetAssemblies();
        for (int i = 0; i < assemblies.Length; i++)
        {
            var types = assemblies[i].GetTypes();
            for (int j = 0; j < types.Length; j++)
            {
                var type = types[j];
                if (type.IsSubclassOf(aType))
                    result.Add(type);
            }
        }
        return result.ToArray();
    }

    public static Rect GetEditorMainWindowPos()
    {
        var containerWinType = System.AppDomain.CurrentDomain.GetAllDerivedTypes(typeof(ScriptableObject)).Where(t => t.Name == "ContainerWindow").FirstOrDefault();
        if (containerWinType == null)
            throw new MissingMemberException("Can't find internal type ContainerWindow. Maybe something has changed inside Unity");

        var showModeField = containerWinType.GetField("m_ShowMode", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var positionProperty = containerWinType.GetProperty("position", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        if (showModeField == null || positionProperty == null)
            throw new MissingFieldException("Can't find internal fields 'm_ShowMode' or 'position'. Maybe something has changed inside Unity");

        var windows = Resources.FindObjectsOfTypeAll(containerWinType);
        for (int i = 0; i < windows.Length; i++)
        {
            var window = windows[i];
            var showmode = (int)showModeField.GetValue(window);
            if (showmode == 4) // main window
            {
                var pos = (Rect)positionProperty.GetValue(window, null);
                return pos;
            }
        }
        throw new NotSupportedException("Can't find internal main window. Maybe something has changed inside Unity");
    }

    public static void CenterOnMainWin(this EditorWindow aWin, float yOffset = 0f, float xOffset = 0f)
    {
        var main = GetEditorMainWindowPos();
        var pos = aWin.position;
        var w = ( main.width - pos.width ) * 0.5f;
        var h = ( main.height - pos.height ) * 0.5f;
        pos.x = main.x + w;
        pos.y = main.y + h + yOffset;
        aWin.position = pos;
    }
}