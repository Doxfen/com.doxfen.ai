using System;
using UnityEditor;
using UnityEngine;

namespace Doxfen.Systems.AI
{
    [InitializeOnLoad]
    public static class AutoAttachGeneratedScript
    {
        private const string KeyPath = "DoxfenAI_PendingAttachPath";
        private const string KeyObject = "DoxfenAI_PendingAttachObject";

        static AutoAttachGeneratedScript()
        {
            EditorApplication.delayCall += TryAttachAfterDomainReload;
        }

        public static void SetPending(string scriptPath, string gameObjectName)
        {
            SessionState.SetString(KeyPath, scriptPath);
            SessionState.SetString(KeyObject, gameObjectName);
        }

        private static void TryAttachAfterDomainReload()
        {
            string path = SessionState.GetString(KeyPath, null);
            string objName = SessionState.GetString(KeyObject, null);

            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(objName))
                return;

            // Clear the keys immediately to avoid duplicate attempts
            SessionState.EraseString(KeyPath);
            SessionState.EraseString(KeyObject);

            MonoScript scriptAsset = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
            if (scriptAsset == null)
            {
                Debug.LogError("⚠️ Could not find generated script after reload.");
                return;
            }

            Type scriptClass = scriptAsset.GetClass();
            if (scriptClass == null)
            {
                Debug.LogError("⚠️ Script compiled, but no class found. Is it a public MonoBehaviour?");
                return;
            }

            GameObject target = GameObject.Find(objName);
            if (target == null)
            {
                Debug.LogError("⚠️ Could not find GameObject to attach script.");
                return;
            }

            Undo.AddComponent(target, scriptClass);
            Debug.Log($"✅ Successfully attached generated script to: {target.name}");
        }
    }
}