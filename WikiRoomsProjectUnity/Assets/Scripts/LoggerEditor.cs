using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(Logger))]
public class LoggerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        if (EditorApplication.isPlaying)
        {
            if (GUILayout.Button("Send Logs"))
            {
                ((Logger)target).SendLogs();
            }
        }
    }
}
