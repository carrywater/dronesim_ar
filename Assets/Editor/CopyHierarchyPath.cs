using UnityEngine;
using UnityEditor;

public class CopyHierarchyPath
{
    [MenuItem("GameObject/Copy Hierarchy Path", false, 0)]
    static void CopyPath()
    {
        if (Selection.activeGameObject == null) return;
        string path = Selection.activeGameObject.name;
        Transform t = Selection.activeGameObject.transform.parent;
        while (t != null && t.parent != null) // stop at root
        {
            path = t.name + "/" + path;
            t = t.parent;
        }
        EditorGUIUtility.systemCopyBuffer = path;
        Debug.Log("Copied path: " + path);
    }
} 