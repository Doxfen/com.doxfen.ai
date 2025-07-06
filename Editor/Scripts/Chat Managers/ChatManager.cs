using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Doxfen.Systems.AI
{
    [Serializable]
    public class ChatMessage
    {
        public string role;  // "user" or "model"
        public string content;
    }

    [Serializable]
    public class ChatData
    {
        public string chatId;
        public string title;
        public List<ChatMessage> messages = new List<ChatMessage>();

        public static string SaveDirectory => Path.Combine("Packages/com.doxfen.ai/Runtime/Internal/Chats History/");

        public string GetFilePath()
        {
            return Path.Combine(SaveDirectory, $"chat_{chatId}.json");
        }
    }

    public static class ChatManager
    {
        public const string CurrentChatKey = "Doxfen_AI_CurrentChatId";

        public static List<ChatData> allChats = new List<ChatData>();
        public static ChatData currentChat;

        public static void Initialize()
        {
            Directory.CreateDirectory(ChatData.SaveDirectory);
            LoadAllChats();

            string savedId = SessionState.GetString(CurrentChatKey, "");
            if (!string.IsNullOrEmpty(savedId))
            {
                currentChat = allChats.Find(c => c.chatId == savedId);
            }
        }

        public static void LoadAllChats()
        {
            allChats.Clear();

            string[] files = Directory.GetFiles(ChatData.SaveDirectory, "chat_*.json");
            foreach (var file in files)
            {
                string json = File.ReadAllText(file);
                ChatData chat = JsonUtility.FromJson<ChatData>(json);
                allChats.Add(chat);
            }
        }

        public static void CreateNewChat()
        {
            ChatData newChat = new ChatData
            {
                chatId = Guid.NewGuid().ToString(),
                title = "New Chat",
                messages = new List<ChatMessage>()
            };
            currentChat = newChat;
            allChats.Add(newChat);
            SaveChat(newChat);

            SetActiveChat(newChat);
        }

        public static void SaveChat(ChatData chat)
        {
            string json = JsonUtility.ToJson(chat, true);
            File.WriteAllText(chat.GetFilePath(), json);
        }

        public static void DeleteChat(ChatData chat)
        {
            if (File.Exists(chat.GetFilePath()))
            {
                File.Delete(chat.GetFilePath());
            }
            allChats.Remove(chat);
            if (currentChat == chat)
                currentChat = null;
        }

        public static void SetActiveChat(ChatData chat)
        {
            currentChat = chat;
            SessionState.SetString(CurrentChatKey, chat?.chatId ?? "");
        }

        public static void AddMessage(string role, string content)
        {
            if (currentChat == null)
                return;

            currentChat.messages.Add(new ChatMessage { role = role, content = content });
            SaveChat(currentChat);
        }
    }
}