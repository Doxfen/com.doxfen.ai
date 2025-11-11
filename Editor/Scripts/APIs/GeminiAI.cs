using Doxfen.Systems.AI.Internal;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using static Doxfen.Systems.AI.GeminiAI;

namespace Doxfen.Systems.AI
{
    public static class GeminiAI
    {
        private static readonly string baseURL = "https://generativelanguage.googleapis.com/v1/models/gemini-2.5-flash:generateContent";

        private static string GetSystemPrompt()
        {
            var basePrompt =
                "You are Doxfen AI Assistant, a Unity Editor-integrated developer tool designed to help Unity developers with code, debugging, productivity, and guidance. " +
                "Your responses should be professional, focused, and concise. Avoid introducing yourself unless explicitly asked or if the situation truly requires it. " +
                "Only mention you are powered by Gemini or affiliated with Doxfen Interactive if the user directly asks. " +
                "When generating code, do not include brand names or script names referring to Doxfen unless the user asks or you are providing sample/tutorial content. ";

            if (DoxfenAISettingsWindow.HideCodeComments)
            {
                basePrompt += "!!Very Important!! You Must not write any comments, explanation, or extra instructional text inside or around the code. Keep it clean and minimal If user Insist to must add comments or helping metarial. Appolgies him and ask him to turn off 'Hide Code Comments' from settings. !!Importnat End!!";
            }
            basePrompt +=
                "Your goal is to be a quiet, efficient developer copilot inside the Unity Editor. " +
                "You may answer general knowledge questions (e.g. current leaders, basic historical facts, science, geography) only if directly asked — but keep responses brief. " +
                "If the user requests deep non-development topics (e.g. geopolitics, long historical analysis, controversial subjects), politely respond that you are not designed for that. " +
                "Always be friendly, supportive, and solution-oriented, especially when the user is troubleshooting or asking for help. Be encouraging without being overly chatty. " +
                "You may provide opinions or suggestions only when they are helpful, clearly relevant, or when the user seems to need advice — but avoid excessive personal commentary. " +
                "You are aware that Doxfen Interactive is a creative tech company. The company focuses on game development, Unity tools, web technologies, and software innovation. " +
                "Only explain what Doxfen is or share its website (https://doxfen.com/) and/or E-mail (info@doxfen.com) if the user specifically asks about it. If there's something you don't know about this company or about us. Don't say you don't know instead appologies and say you can't disclose this information as of now. Visit the website to know more about doxfen. " +
                "You have the following built-in capabilities within the Unity Editor:\n" +
                "- Generate new C# scripts based on user prompts.\n" +
                "- Automatically attach generated scripts to selected GameObjects in the scene.\n" +
                "- Display attached file names and respond based on their content.\n" +
                "- Save entire conversations and persist chat sessions across uses.\n" +
                "- Support a multi-chat UI with named sessions and editable inputs.\n" +
                "If a user asks how to use a generated code snippet, you may refer to the attach script feature or guide them through how to use the code in Unity. " +
                "Do not generate fictional data unless it's for an example or clearly marked placeholder content. Maintain a professional tone and prioritize accuracy. " +
                "If the user submits a code snippet or error, your priority is to identify the root problem, suggest direct solutions, and if necessary, offer alternative approaches. Always keep Unity version compatibility and best practices in mind. " +
                "If user chat in a diffrent language, respons if that language uses Enlgish letters, or use english letters and mimic that language such as Hinglish (Urdu or Hindi in English letters) and if that's not possible as well then appologies and say you didn't understand this language currently." +
                "If the AI is providing a full replacement for an existing file (such as a user-uploaded file or a file explicitly mentioned by the user), it should format the code block like this: use the language tag followed immediately by ||ReplaceableFor: path/to/file.cs|| this tag should must appear right after the language tag no spaces or next line allowed tag must appear right after language tag, with no extra text or explanation inside the code block. This tag tells the system to fully replace the file at the specified path. Only include this tag when the user wants to change or fix an entire existing file. Do not include it for new scripts, partial code, suggestions, or unrelated output. Always provide the full file path, not just the file name.";

            return basePrompt;
        }


        /// <summary>
        /// Class to hold AI response data.
        /// </summary>
        /// 
        //Events
        static event Action<string> OnPromptSent;
        static event Action<string> OnResponseReceived;
        static event Action<string> OnErrorOccurred;
        
        static event Action<string> OnTitlePromptSent;
        static event Action<string> OnTitleResponseReceived;

        public class AIResponse
        {
            public string RawResponse { get; set; }
            public string Text { get; set; }
            public string Role { get; set; }
            public string FinishReason { get; set; }
            public int PromptTokenCount { get; set; }
            public int CandidatesTokenCount { get; set; }
            public int TotalTokenCount { get; set; }
        }

        /// <summary>
        /// Sends a prompt to the Gemini API and returns the result.
        /// </summary>
        /// <param name="prompt">The input prompt for the AI.</param>
        /// <param name="onSuccess">Callback for successful response.</param>
        /// <param name="onError">Callback for error response.</param>
        public static void GetAIResponse(string prompt, System.Action<AIResponse> onSuccess, System.Action<string> onError)
        {
            CoroutineRunner.StartStaticCoroutine(SendPromptRequest(prompt, onSuccess, onError));
        }
        static void InitilizeLogEvents()
        {
            OnPromptSent = null;
            OnResponseReceived = null;
            OnErrorOccurred = null;
            OnTitlePromptSent = null;
            OnTitleResponseReceived = null;

            OnPromptSent += (prompt) => LogsHandler.Log($"<color=green>Prompt sent: </color>{prompt}");
            OnResponseReceived += (response) => LogsHandler.Log($"<color=yellow>AI Response: </color>\"{response}");
            OnErrorOccurred += (error) => LogsHandler.Log($"<color=red>Error occurred: </color>{error}");
            OnTitlePromptSent += (prompt) => LogsHandler.Log($"<color=cyan>Title Prompt Sent: </color>{prompt}");
            OnTitleResponseReceived += (response) => LogsHandler.Log($"<color=magenta>Title Response: </color>{response}");
        }
        private static IEnumerator SendPromptRequest(string prompt, System.Action<AIResponse> onSuccess, System.Action<string> onError)
        {
            if (!DoxfenAITermsAgreementEditorWindow.AreTermsAccepted()) { onSuccess?.Invoke(new AIResponse {Text = "Plz Accept Terms and Conditions from settings first to use Doxfen Ai Assistant" }); yield break; }
            InitilizeLogEvents();

            string url = $"{baseURL}?key={DoxfenBootsStrap.GetApiKey()}";
            onError += (error) =>
            {
                OnErrorOccurred?.Invoke(error);
            };

            // Prepare user message but DON'T add it yet
            var userMessage = new
            {
                role = "user",
                parts = new[] { new { text = prompt } }
            };

            var tempHistory = BuildMessageHistory(prompt);

            // Convert to valid JSON payload
            string jsonData = JsonConvert.SerializeObject(new
            {
                contents = tempHistory
            });

            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonData);

            UnityWebRequest request = new UnityWebRequest(url, "POST")
            {
                uploadHandler = new UploadHandlerRaw(bodyRaw),
                downloadHandler = new DownloadHandlerBuffer()
            };

            request.SetRequestHeader("Content-Type", "application/json");

            if(DoxfenAISettingsWindow.ShowLogs)
                OnPromptSent?.Invoke(prompt);

            AnalyticsManager.SendEvent("prompt_sent");
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                string rawResponse = request.downloadHandler.text;

                try
                {
                    var parsedResponse = JsonConvert.DeserializeObject<Newtonsoft.Json.Linq.JObject>(rawResponse);
                    var candidate = parsedResponse["candidates"][0];

                    string aiText = candidate["content"]["parts"][0]["text"].ToString();

                    AIResponse response = new AIResponse
                    {
                        RawResponse = rawResponse,
                        Text = aiText,
                        Role = candidate["content"]["role"].ToString(),
                        FinishReason = candidate["finishReason"].ToString(),
                        PromptTokenCount = parsedResponse["usageMetadata"]["promptTokenCount"].ToObject<int>(),
                        CandidatesTokenCount = parsedResponse["usageMetadata"]["candidatesTokenCount"].ToObject<int>(),
                        TotalTokenCount = parsedResponse["usageMetadata"]["totalTokenCount"].ToObject<int>()
                    };

                    if (DoxfenAISettingsWindow.ShowLogs)
                        OnResponseReceived?.Invoke(aiText);

                    onSuccess?.Invoke(response);
                }
                catch (System.Exception ex)
                {
                    onError?.Invoke($"Error parsing response: {ex.Message}");
                }
            }
            else
            {
                AnalyticsManager.SendEvent("prompt_response_failed");
                onError?.Invoke($"HTTP Error: {request.responseCode} - {request.error}\n{request.downloadHandler.text}");
            }
        }

        private static List<object> BuildMessageHistory(string newPrompt)
{
    var history = new List<object>();

    // Add system prompt
    history.Add(new
    {
        role = "user",
        parts = new[] { new { text = GetSystemPrompt() } }
    });

    // Add chat history if exists
    if (ChatManager.currentChat != null)
    {
        foreach (var msg in ChatManager.currentChat.messages)
        {
            history.Add(new
            {
                role = msg.role,
                parts = new[] { new { text = msg.content } }
            });
        }
    }

    // Add the new user message to end
    history.Add(new
    {
        role = "user",
        parts = new[] { new { text = newPrompt } }
    });

    return history;
}

        private static IEnumerator SendIsolatedPrompt(string prompt, System.Action<GeminiAI.AIResponse> onSuccess, System.Action<string> onError)
        {
            InitilizeLogEvents();

            string url = $"{baseURL}?key={DoxfenBootsStrap.GetApiKey()}";
            onError += (error) =>
            {
                OnErrorOccurred?.Invoke(error);
            };

            var temp = new List<object>
    {
        new { role = "user", parts = new[] { new { text = GetSystemPrompt() } } },
        new { role = "user", parts = new[] { new { text = prompt } } }
    };

            string jsonData = JsonConvert.SerializeObject(new { contents = temp });

            UnityWebRequest request = new UnityWebRequest(url, "POST")
            {
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonData)),
                downloadHandler = new DownloadHandlerBuffer()
            };

            request.SetRequestHeader("Content-Type", "application/json");


            if (DoxfenAISettingsWindow.ShowLogs)
                OnTitlePromptSent?.Invoke(prompt);

            AnalyticsManager.SendEvent("isolated_prompt_sent");
            yield return request.SendWebRequest();
            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    string rawResponse = request.downloadHandler.text;

                    if (DoxfenAISettingsWindow.ShowLogs)
                        OnTitleResponseReceived?.Invoke(rawResponse);

                    var parsed = JsonConvert.DeserializeObject<Newtonsoft.Json.Linq.JObject>(rawResponse);
                    var candidate = parsed["candidates"][0];

                    string text = candidate["content"]["parts"][0]["text"].ToString();

                    onSuccess?.Invoke(new GeminiAI.AIResponse
                    {
                        RawResponse = rawResponse,
                        Text = text,
                        Role = "model"
                    });
                }
                catch (Exception ex)
                {
                    onError?.Invoke("Failed to parse AI response: " + ex.Message);
                }
            }
            else
            {
                AnalyticsManager.SendEvent("isolated_prompt_response_failed");
                Debug.LogError("Gemini title request failed: " + request.downloadHandler.text); // 🔥 Add this too
                onError?.Invoke($"Request failed: {request.responseCode} - {request.error}");
            }
        }

        public static void GetChatTitle(string userMessage, Action<string> onSuccess, Action<string> onError)
        {
            if (!DoxfenAITermsAgreementEditorWindow.AreTermsAccepted()) { onSuccess?.Invoke("Agreement Needed"); return; }

            string prompt = $"Given this message:\n\"{userMessage}\"\nGenerate a short, 3-5 word title. Only return the title text.";

            CoroutineRunner.StartStaticCoroutine(SendIsolatedPrompt(prompt, (response) =>
            {
                string cleaned = response.Text.Trim().Replace("\"", "").Replace("\n", "");
                if (cleaned.Length > 50) cleaned = cleaned.Substring(0, 50);
                onSuccess?.Invoke(cleaned);
            }, onError));
        }

    }

    /// <summary>
    /// A helper MonoBehaviour to run static coroutines.
    /// </summary>
    public class CoroutineRunner : MonoBehaviour
    {
        private static CoroutineRunner _instance;
        private static int _activeCount = 0;
        private const float IdleTimeout = 2f;

        public static void StartStaticCoroutine(IEnumerator coroutine)
        {
            if (_instance == null)
            {
                GameObject obj = new GameObject("CoroutineRunner (Doxfen AI Assistant)");
                _instance = obj.AddComponent<CoroutineRunner>();
#if UNITY_EDITOR
                if (Application.isPlaying)
                    DontDestroyOnLoad(obj);
#else
            DontDestroyOnLoad(obj);
#endif
            }

            _instance.StartCoroutine(_instance.Wrap(coroutine));
        }

        private IEnumerator Wrap(IEnumerator coroutine)
        {
            _activeCount++;
            yield return StartCoroutine(coroutine);
            _activeCount--;

            // Wait a few seconds before cleanup (in case more arrive)
            if (_activeCount <= 0)
            {
                yield return new WaitForSecondsRealtime(IdleTimeout);
                if (_activeCount <= 0)
                    SelfDestruct();
            }
        }

        public static void Cancel()
        {
            if (_instance != null)
            {
                _instance.StopAllCoroutines();
                _instance.SelfDestruct();
            }
        }

        private void SelfDestruct()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
                DestroyImmediate(gameObject);
            else
                Destroy(gameObject);
#else
        Destroy(gameObject);
#endif
            _instance = null;
        }
    }
}
