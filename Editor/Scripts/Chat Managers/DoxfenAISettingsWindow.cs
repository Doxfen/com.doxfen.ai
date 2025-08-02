using UnityEditor;
using UnityEngine;

namespace Doxfen.Systems.AI
{
    public class DoxfenAISettingsWindow : EditorWindow
    {
        private const string ShowAttachmentKey = "Doxfen_ShowAttachmentContent";
        private const string ShowLogsKey = "Doxfen_ShowLogs";
        private const string HideCodeCommentsKey = "Doxfen_HideCodeComments";
        private const string SendAnalyticsDataKey = "Doxfen_SendAnalyticsData";

        private bool showAttachments;
        private bool showLogs;
        private bool hideCodeComments;
        private bool SendAnalyticsData;

        [MenuItem("Window/Doxfen/AI Assistant/AI Settings", false, 12)]
        public static void ShowWindow()
        {
            var window = GetWindow<DoxfenAISettingsWindow>("AI Settings");
            window.minSize = new Vector2(300, 100);
        }

        private void OnEnable()
        {
            showAttachments = EditorPrefs.GetBool(ShowAttachmentKey, false);
            showLogs = EditorPrefs.GetBool(ShowLogsKey, false);
            hideCodeComments = EditorPrefs.GetBool(HideCodeCommentsKey, false);
            SendAnalyticsData = EditorPrefs.GetBool(SendAnalyticsDataKey, true);
        }

        private void OnGUI()
        {
            EditorGUILayout.BeginVertical();
            GUILayout.Space(10);
            GUILayout.BeginHorizontal();
            GUILayout.Space(10);
            GUILayout.BeginVertical();

            GUILayout.Label("Doxfen AI Assistant Settings", EditorStyles.boldLabel);
            GUILayout.Space(10);

            showAttachments = EditorGUILayout.ToggleLeft("Show Attachment Content", showAttachments);
            showLogs = EditorGUILayout.ToggleLeft("Show Logs", showLogs);
            hideCodeComments = EditorGUILayout.ToggleLeft("Hide Code Comments", hideCodeComments);
            GUILayout.Space(10);
            SendAnalyticsData = EditorGUILayout.ToggleLeft("Send Analytics Data", SendAnalyticsData);

            GUILayout.Space(15);
            if (GUILayout.Button("Save Settings", GUILayout.Height(30)))
            {
                SaveSettings();
            }
            GUILayout.Space(20);
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Contact Support", GUILayout.Width(150), GUILayout.Height(20)))
            {
                Application.OpenURL("mailto:doxfeninteractive@gmail.com?subject=DoxfenAiAssistant AI Assistant Support");
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.Space(10);
            GUILayout.EndVertical();
            GUILayout.Space(10);
            GUILayout.EndHorizontal();
            GUILayout.Space(10);
            EditorGUILayout.EndVertical();
        }

        private void SaveSettings()
        {
            EditorPrefs.SetBool(ShowAttachmentKey, showAttachments);
            EditorPrefs.SetBool(ShowLogsKey, showLogs);
            EditorPrefs.SetBool(HideCodeCommentsKey, hideCodeComments);
            EditorPrefs.SetBool(SendAnalyticsDataKey, SendAnalyticsData);
            Debug.Log("Doxfen AI settings saved.");
        }

        public static bool ShowAttachmentContent => EditorPrefs.GetBool(ShowAttachmentKey, false);
        public static bool ShowLogs => EditorPrefs.GetBool(ShowLogsKey, false);
        public static bool HideCodeComments => EditorPrefs.GetBool(HideCodeCommentsKey, false);
        public static bool SendDataAnalytics => EditorPrefs.GetBool(SendAnalyticsDataKey, true);
    }
}