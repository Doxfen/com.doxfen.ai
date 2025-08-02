using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;

namespace Doxfen.Systems.AI.Internal
{
    [InitializeOnLoad]
    public static class AnalyticsManager
    {
        private const string MeasurementId = "G-7TZR8QT1JW";
        private const string ApiSecret = "OOmPMenhSraNmNmcWkCILg";
        private const string ClientIdKey = "Doxfen_Client_Id"; // shared key for all Unity projects
        private static string clientId;

        static AnalyticsManager()
        {
            clientId = GetOrCreateClientId();
        }

        private static string GetOrCreateClientId()
        {
            if (EditorPrefs.HasKey(ClientIdKey))
            {
                return EditorPrefs.GetString(ClientIdKey);
            }
            else
            {
                string newGuid = Guid.NewGuid().ToString();
                EditorPrefs.SetString(ClientIdKey, newGuid);
                return newGuid;
            }
        }

        public static void SendEvent(string eventName, Dictionary<string, object> parameters = null)
        {
            if (!DoxfenAISettingsWindow.SendDataAnalytics)
                return;


            string url = $"https://www.google-analytics.com/mp/collect?measurement_id={MeasurementId}&api_secret={ApiSecret}";

            var payload = new Dictionary<string, object>
            {
                { "client_id", clientId },
                { "events", new[] {
                    new Dictionary<string, object> {
                        { "name", eventName },
                        { "params", parameters ?? new Dictionary<string, object>() }
                    }
                }}
            };

            string json = JsonConvert.SerializeObject(payload);
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);

            UnityWebRequest request = new UnityWebRequest(url, "POST");
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            RunRequestInEditor(request, eventName);
        }

        private static void RunRequestInEditor(UnityWebRequest request, string eventName)
        {
            EditorApplication.update += EditorUpdate;

            void EditorUpdate()
            {
                var operation = request.SendWebRequest();
                EditorApplication.update -= EditorUpdate;

                operation.completed += _ =>
                {
                    //if (request.result != UnityWebRequest.Result.Success)
                    //    //Debug.LogWarning($"[Doxfen Analytics] Failed: {request.error}");
                    //else
                    //    //Debug.Log($"[Doxfen Analytics] Event Sent: {eventName}");
                };
            }
        }
    }
}