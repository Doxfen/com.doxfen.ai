using Doxfen.Systems.AI;
using Doxfen.Systems.AI.Internal;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Drawing.Printing;
using System.IO;
using System.Runtime.Remoting.Messaging;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Doxfen.Systems.AI
{
    public class ChatAIWindow : EditorWindow
    {
        #region Variales
        VisualElement messagesContainer;
        TextField inputField;
        Button sendButton;
        VisualElement topBar;
        Button newChatButton;

        bool isAwaitingResponse = false;
        bool responseWasCanceled = false;
        string currentPromptId = string.Empty;

        VisualElement currentTypingPlaceholder;
        TypingAnimation currentTypingAnimation;
        double requestStartTime;
        const double TIMEOUT_SECONDS = 90;

        private const string USER_NAME = "You";
        private const string AI_NAME = "Assistant";

        private static readonly Color USER_TITLE_COLOR = new Color(0.4f, 0.6f, 1.0f);
        private static readonly Color AI_TITLE_COLOR = new Color(1.0f, 0.85f, 0.4f);
        private static readonly Color USER_BG_DARK = new Color(0.25f, 0.30f, 0.42f);
        private static readonly Color USER_BG_LIGHT = new Color(0.85f, 0.90f, 1.0f);
        private static readonly Color AI_BG_DARK = new Color(0.32f, 0.34f, 0.38f);
        private static readonly Color AI_BG_LIGHT = new Color(0.95f, 0.95f, 0.92f);

        private const float USER_MAX_WIDTH = 400f;
        private const float AI_MAX_WIDTH = 550f;

        private Image logoImage;

        private VisualElement logoContainer;

        private VisualElement chatSidePanel;
        private VisualElement chatListContainer;
        private VisualElement chatOverlayBlocker;
        private bool isChatPanelVisible = false;
        private Button deleteChatButton;

        private List<string> attachedFilePaths = new List<string>();
        private Action RefreshAttachmentUI;
        private VisualElement attachmentBar;  // Our dynamic island container

        private bool ShowAttachmentsContent = false;

        #endregion
        [MenuItem("Window/Doxfen/AI Assistant/Chat AI", false, 10)]
        public static void ShowWindow()
        {
            var window = GetWindow<ChatAIWindow>(false, "AI Assistant", true);
            window.minSize = new Vector2(400, 600);
            if (!DoxfenAITermsAgreementEditorWindow.AreTermsAccepted())
            {
                DoxfenAITermsAgreementEditorWindow.ShowWindow();
            }
            AnalyticsManager.SendEvent("chat_ai_opened", new Dictionary<string, object>
            {
                { "show_attachments_enabled", DoxfenAISettingsWindow.ShowAttachmentContent },
                { "show_logs_enabled", DoxfenAISettingsWindow.ShowLogs },
                { "hide_comments_enabled", DoxfenAISettingsWindow.HideCodeComments }
            });
        }

        public static void HideWindow()
        {
            var window = GetWindow<ChatAIWindow>(false, "AI Assistant", true);
            window.Close();
            window = null;
        }

        void CheckAllSettings()
        {
            ShowAttachmentsContent = DoxfenAISettingsWindow.ShowAttachmentContent;
        }

        private void OnEnable()
        {
            CheckAllSettings();
            ChatManager.currentChat = null;
            ChatManager.Initialize();
            var root = rootVisualElement;
            root.style.position = Position.Relative;
            root.Clear();
            root.style.flexDirection = FlexDirection.Column;
            root.style.flexGrow = 1;

            // --- TOP BAR ---
            topBar = new VisualElement();
            topBar.style.flexDirection = FlexDirection.Row;
            topBar.style.justifyContent = Justify.SpaceBetween;
            topBar.style.alignItems = Align.Center;
            topBar.style.height = 40;
            topBar.style.paddingLeft = 10;
            topBar.style.paddingRight = 10;
            topBar.style.backgroundColor = new Color(0.15f, 0.15f, 0.15f);
            topBar.style.flexShrink = 0;

            var menuButton = new Button
            {
                text = "≡"
            };
            menuButton.style.fontSize = 28;
            menuButton.style.width = 24;
            menuButton.style.height = 24;
            menuButton.style.unityFontStyleAndWeight = FontStyle.Bold;
            menuButton.style.backgroundColor = Color.clear;
            menuButton.style.borderBottomWidth = 0;
            menuButton.style.borderTopWidth = 0;
            menuButton.style.borderLeftWidth = 0;
            menuButton.style.borderRightWidth = 0;
            menuButton.style.color = new Color(0.8f, 0.8f, 0.8f); // Light gray for dark theme

            topBar.Add(menuButton);

            var logoHolder = new VisualElement();
            logoHolder.style.flexDirection = FlexDirection.Row;
            logoHolder.style.alignItems = Align.Center;
            logoHolder.style.justifyContent = Justify.FlexStart;
            logoHolder.style.flexGrow = 1;

            var logoTexture = AssetDatabase.LoadAssetAtPath<Texture2D>("Packages/com.doxfen.ai/Runtime/Internal/UI PNGs/Logos/Assisstant Logos/Ai Assisstant With Logo (Horizontal) [128] Text Large.png");

            if (logoTexture != null)
            {
                var logoImg = new Image();
                logoImg.image = logoTexture;
                logoImg.scaleMode = ScaleMode.ScaleToFit;
                logoImg.style.height = 28;
                logoImg.style.maxWidth = 90;
                logoImg.style.marginRight = 6;
                logoHolder.Add(logoImg);
            }
            else
            {
                var fallbackLabel = new Label("🧠 DOXFEN");
                fallbackLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                fallbackLabel.style.fontSize = 14;
                fallbackLabel.style.color = Color.white;
                fallbackLabel.style.marginRight = 6;
                logoHolder.Add(fallbackLabel);
            }

            topBar.Add(logoHolder);

            newChatButton = new Button(() =>
            {
                OnCreateNewChat();
            })
            { text = "New Chat" };

            newChatButton.style.height = 28;
            newChatButton.style.marginLeft = 6;
            newChatButton.style.backgroundColor = new Color(0.3f, 0.6f, 1f);
            newChatButton.style.color = Color.white;
            newChatButton.style.paddingLeft = 10;
            newChatButton.style.paddingRight = 10;
            newChatButton.style.unityFontStyleAndWeight = FontStyle.Bold;
            newChatButton.style.borderTopLeftRadius = 8;
            newChatButton.style.borderTopRightRadius = 8;
            newChatButton.style.borderBottomLeftRadius = 8;
            newChatButton.style.borderBottomRightRadius = 8;

            deleteChatButton = new Button(() =>
            {
                if (ChatManager.currentChat != null && ChatManager.currentChat.messages.Count > 0)
                {
                    bool confirm = EditorUtility.DisplayDialog(
                        "Delete Chat?",
                        $"Are you sure you want to delete \"{ChatManager.currentChat.title}\"?",
                        "Yes, Delete",
                        "Cancel"
                    );

                    if (confirm)
                    {
                        ChatManager.DeleteChat(ChatManager.currentChat); // Implemented in your ChatManager
                        OnCreateNewChat(); // Reset UI to new chat
                    }
                }
            })
            {
                text = "Delete Chat"
            };

            deleteChatButton.style.height = 28;
            deleteChatButton.style.marginLeft = 6;
            deleteChatButton.style.backgroundColor = new Color(1f, 0.4f, 0.4f);
            deleteChatButton.style.color = Color.white;
            deleteChatButton.style.paddingLeft = 10;
            deleteChatButton.style.paddingRight = 10;
            deleteChatButton.style.unityFontStyleAndWeight = FontStyle.Bold;
            deleteChatButton.style.borderTopLeftRadius = 8;
            deleteChatButton.style.borderTopRightRadius = 8;
            deleteChatButton.style.borderBottomLeftRadius = 8;
            deleteChatButton.style.borderBottomRightRadius = 8;
            deleteChatButton.visible = false; // 👈 Start hidden
            topBar.Add(deleteChatButton);

            topBar.Add(newChatButton);
            root.Add(topBar);
            menuButton.clicked += ToggleChatPanelVisibility;
            // --- CHAT SIDE PANEL ---
            chatSidePanel = new VisualElement();
            chatSidePanel.style.width = 240;
            chatSidePanel.style.flexShrink = 0;
            chatSidePanel.style.flexGrow = 0;
            chatSidePanel.style.flexDirection = FlexDirection.Column;
            chatSidePanel.style.backgroundColor = EditorGUIUtility.isProSkin ? new Color(0.1f, 0.1f, 0.1f) : new Color(0.95f, 0.95f, 0.95f);
            chatSidePanel.style.paddingTop = 6;
            chatSidePanel.style.paddingBottom = 6;
            chatSidePanel.style.borderRightWidth = 1;
            chatSidePanel.style.borderRightColor = new Color(0.25f, 0.25f, 0.25f);

            // Add title
            var titleLabel = new Label("Chats");
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.fontSize = 14;
            titleLabel.style.marginLeft = 10;
            titleLabel.style.marginBottom = 8;
            titleLabel.style.color = EditorGUIUtility.isProSkin ? Color.white : Color.black;
            chatSidePanel.Add(titleLabel);

            // Chat list container
            chatListContainer = new VisualElement();
            chatListContainer.style.flexDirection = FlexDirection.Column;
            chatListContainer.style.flexGrow = 1;
            //chatListContainer.style.overflow = Overflow.Auto;
            chatSidePanel.Add(chatListContainer);
            var chatButton = new Button(() =>
            {
                //LoadChat(chatId); // You'll define LoadChat later
            })
            {
                text = "Chat 1" // Replace with dynamic title
            };
            chatButton.style.marginBottom = 4;
            chatButton.style.paddingLeft = 10;
            chatButton.style.paddingRight = 10;
            chatButton.style.height = 28;
            //chatButton.style.borderRadius = 8;
            chatButton.style.backgroundColor = new Color(0.25f, 0.25f, 0.25f);
            chatButton.style.color = Color.white;
            chatListContainer.Add(chatButton);

            // Add to root



            var mainArea = new VisualElement();
            mainArea.style.flexGrow = 1;
            mainArea.style.flexDirection = FlexDirection.Column;
            // --- MESSAGE SCROLL AREA (flexes) ---
            messagesContainer = new ScrollView();
            messagesContainer.style.flexGrow = 1;
            messagesContainer.style.flexShrink = 1;
            //messagesContainer.style.overflow = Overflow.Auto;
            mainArea.Add(messagesContainer);

            CreateResponsiveLogo(root);
            ShowLogoIfEmpty();

            attachmentBar = new ScrollView(ScrollViewMode.Horizontal);
            attachmentBar.style.flexDirection = FlexDirection.Row;
            attachmentBar.style.marginLeft = 10;
            attachmentBar.style.marginRight = 10;
            attachmentBar.style.marginBottom = 6;
            attachmentBar.style.flexGrow = 0;
            attachmentBar.style.flexShrink = 0;
            attachmentBar.style.maxHeight = 32;
            attachmentBar.style.overflow = Overflow.Visible;
            attachmentBar.style.display = DisplayStyle.None; // Hidden until files added

            mainArea.Add(attachmentBar);
            // --- INPUT FIELD AREA ---
            var inputContainer = new VisualElement();
            inputContainer.style.flexDirection = FlexDirection.Row;
            inputContainer.style.alignItems = Align.FlexEnd; // Crucial!
            inputContainer.style.marginLeft = 10;
            inputContainer.style.marginRight = 10;
            inputContainer.style.marginBottom = 10;
            inputContainer.style.flexShrink = 0;
            inputContainer.style.flexGrow = 0;
            inputContainer.style.minHeight = 36;
            inputContainer.style.maxHeight = 100;

            var isDark = EditorGUIUtility.isProSkin;

            var attachButton = new Button()
            {
                text = "➕"
            };

            attachButton.clicked += () =>
            {
                string path = EditorUtility.OpenFilePanel("Attach File", "", "*");

                if (!string.IsNullOrEmpty(path) && !attachedFilePaths.Contains(path))
                {
                    attachedFilePaths.Add(path);
                    SessionState.SetString("DoxfenAttachedFiles", JsonConvert.SerializeObject(attachedFilePaths));
                    RefreshAttachmentBar();
                }
            };

            // Button dimensions
            attachButton.style.width = 32;
            attachButton.style.height = 32;
            attachButton.style.marginRight = 4;
            attachButton.style.unityFontStyleAndWeight = FontStyle.Bold;
            attachButton.style.fontSize = 16;

            // Background color based on theme
            attachButton.style.backgroundColor = isDark
                ? new Color(0.25f, 0.25f, 0.25f)
                : new Color(0.9f, 0.9f, 0.9f);

            // Text/icon color for visibility
            attachButton.style.color = isDark ? Color.white : Color.black;

            // Rounded left corners
            attachButton.style.borderTopLeftRadius = 12;
            attachButton.style.borderBottomLeftRadius = 12;
            attachButton.style.borderTopRightRadius = 0;
            attachButton.style.borderBottomRightRadius = 0;

            // Optional: border
            attachButton.style.borderBottomWidth = 0;
            attachButton.style.borderTopWidth = 0;
            attachButton.style.borderLeftWidth = 0;
            attachButton.style.borderRightWidth = 0;

            // Add attach button
            inputContainer.Add(attachButton);

            // 💡 Inner wrapper for the TextField
            var textFieldWrapper = new VisualElement();
            textFieldWrapper.style.flexGrow = 1;
            textFieldWrapper.style.flexShrink = 1;
            textFieldWrapper.style.flexDirection = FlexDirection.Column;

            inputField = new TextField { multiline = true };
            inputField.style.flexGrow = 1;
            inputField.style.minHeight = 32;
            inputField.style.maxHeight = 800;
            inputField.style.marginRight = 4;
            inputField.style.borderBottomLeftRadius = 0;
            inputField.style.borderTopLeftRadius = 0;
            inputField.style.borderBottomRightRadius = 0;
            inputField.style.borderTopRightRadius = 0;
            inputField.style.paddingLeft = 10;
            inputField.style.paddingRight = 10;
            inputField.style.unityFontStyleAndWeight = FontStyle.Normal;
            inputField.style.borderBottomWidth = 0;
            inputField.style.borderTopWidth = 0;
            inputField.style.borderLeftWidth = 0;
            inputField.style.borderRightWidth = 0;
            inputField.style.backgroundColor = EditorGUIUtility.isProSkin
                ? new Color(0.18f, 0.18f, 0.18f)
                : new Color(0.87f, 0.87f, 0.87f);
            inputField.style.overflow = Overflow.Visible; // Prevent clipping scroll
            inputField.style.whiteSpace = WhiteSpace.Normal; // Allow text wrap
            inputField.style.flexGrow = 1; // Let it expand
            inputField.style.flexShrink = 1;

            // Optional: remove padding if still misaligned
            inputField.style.paddingTop = 4;
            inputField.style.paddingBottom = 0;
            var inputFieldInput = inputField.Q("unity-text-input");
            if (inputFieldInput != null)
            {
                inputFieldInput.style.backgroundColor = Color.clear;
                inputFieldInput.style.borderBottomWidth = 0;
                inputFieldInput.style.borderTopWidth = 0;
                inputFieldInput.style.borderLeftWidth = 0;
                inputFieldInput.style.borderRightWidth = 0;
            }
            var textInput = inputField.Q("unity-text-input");
            if (textInput != null)
            {
                textInput.style.unityTextAlign = TextAnchor.MiddleLeft; // 👈 Center vertical + left
                textInput.style.paddingTop = 6;  // Optional: fine-tune alignment
                textInput.style.paddingBottom = 6;
            }

            textFieldWrapper.Add(inputField);
            inputContainer.Add(textFieldWrapper);

            // 🔘 SEND BUTTON
            sendButton = new Button { text = "▶" };
            sendButton.style.width = 40;
            sendButton.style.height = 32;
            sendButton.style.borderTopRightRadius = 12;
            sendButton.style.borderBottomRightRadius = 12;
            sendButton.style.borderTopLeftRadius = 0;
            sendButton.style.borderBottomLeftRadius = 0;
            sendButton.style.backgroundColor = new Color(0.25f, 0.6f, 1f);
            sendButton.style.color = Color.white;
            sendButton.clicked += OnSendOrCancelClicked;

            inputContainer.Add(sendButton);
            mainArea.Add(inputContainer);
            root.Add(mainArea);

            chatOverlayBlocker = new VisualElement();
            chatOverlayBlocker.style.position = Position.Absolute;
            chatOverlayBlocker.style.top = 40; // below top bar
            chatOverlayBlocker.style.bottom = 0;
            chatOverlayBlocker.style.left = 0;
            chatOverlayBlocker.style.right = 0;
            chatOverlayBlocker.style.backgroundColor = new Color(0, 0, 0, 0); // transparent
            chatOverlayBlocker.visible = false;

            chatOverlayBlocker.RegisterCallback<ClickEvent>((evt) =>
            {
                HideChatPanel();
                evt.StopPropagation(); // Prevent it from triggering other UI
            });

            root.Add(chatOverlayBlocker);
            root.Add(chatSidePanel);
            chatSidePanel.style.position = Position.Absolute;
            chatSidePanel.style.top = 40; // below top bar
            chatSidePanel.style.bottom = 0;
            chatSidePanel.style.left = 0;
            chatSidePanel.style.maxWidth = 300;
            chatSidePanel.style.width = 240;
            chatSidePanel.style.display = DisplayStyle.None;

            if (chatListContainer.childCount == 0)
            {
                var noChatsLabel = new Label("No chats available");
                noChatsLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
                noChatsLabel.style.flexGrow = 1;
                noChatsLabel.style.marginTop = 100;
                noChatsLabel.style.color = EditorGUIUtility.isProSkin ? Color.gray : Color.black;
                noChatsLabel.style.fontSize = 13;
                chatListContainer.Add(noChatsLabel);
            }
            inputField.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (!isAwaitingResponse && (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter))
                {
                    evt.StopImmediatePropagation();
                    if (!string.IsNullOrWhiteSpace(inputField.value))
                    {
                        TrySendMessage();
                    }
                }
            });
            // Drag-and-drop support for .cs files
            rootVisualElement.RegisterCallback<DragUpdatedEvent>(evt =>
            {
                if (DragAndDrop.paths != null && DragAndDrop.paths.Length > 0)
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                    evt.StopPropagation();
                }
            });

            rootVisualElement.RegisterCallback<DragPerformEvent>(evt =>
            {
                if (DragAndDrop.paths != null && DragAndDrop.paths.Length > 0)
                {
                    foreach (string path in DragAndDrop.paths)
                    {
                        if (!attachedFilePaths.Contains(path))
                        {
                            attachedFilePaths.Add(path);
                        }
                    }

                    SessionState.SetString("DoxfenAttachedFiles", JsonConvert.SerializeObject(attachedFilePaths));
                    RefreshAttachmentBar();

                    DragAndDrop.AcceptDrag();
                    evt.StopPropagation();

                    // Optional: auto-attach
                    TryAutoAttachScriptsFromDraggedPaths(DragAndDrop.paths);
                }
            });


            LoadAllChatButtons();
            if (ChatManager.currentChat != null && ChatManager.currentChat.messages.Count > 0)
            {
                LoadChatIntoMainView(ChatManager.currentChat);
            }
            else
            {
                ShowLogoIfEmpty();
            }

            string saved = SessionState.GetString("DoxfenAttachedFiles", "");
            if (!string.IsNullOrEmpty(saved))
            {
                try
                {
                    attachedFilePaths = JsonConvert.DeserializeObject<List<string>>(saved);
                    RefreshAttachmentBar();
                }
                catch { attachedFilePaths = new List<string>(); }
            }
            else
            {
                attachedFilePaths = new List<string>();
            }
        }
        private void TryAutoAttachScriptsFromDraggedPaths(string[] paths)
        {
            GameObject selectedObj = Selection.activeGameObject;
            if (selectedObj == null) return;

            foreach (string path in paths)
            {
                if (!path.EndsWith(".cs")) continue;

                // Convert absolute path to relative Unity path
                string assetPath = "Assets" + path.Replace(Application.dataPath, "").Replace("\\", "/");
                MonoScript scriptAsset = AssetDatabase.LoadAssetAtPath<MonoScript>(assetPath);
                if (scriptAsset != null && scriptAsset.GetClass() != null)
                {
                    Type scriptType = scriptAsset.GetClass();
                    if (selectedObj.GetComponent(scriptType) == null)
                    {
                        Undo.AddComponent(selectedObj, scriptType);
                        Debug.Log($"[Doxfen AI] Attached {scriptType.Name} to {selectedObj.name}");
                    }
                }
            }
        }

        private void RefreshAttachmentBar()
        {
            attachmentBar.Clear();

            if (attachedFilePaths.Count == 0)
            {
                attachmentBar.style.display = DisplayStyle.None;
                return;
            }

            attachmentBar.style.display = DisplayStyle.Flex;

            foreach (string path in attachedFilePaths)
            {
                string fileName = System.IO.Path.GetFileName(path);

                var pill = new VisualElement();
                pill.style.flexDirection = FlexDirection.Row;
                pill.style.marginRight = 6;
                pill.style.paddingLeft = 10;
                pill.style.paddingRight = 6;
                pill.style.paddingTop = 3;
                pill.style.paddingBottom = 3;
                pill.style.borderTopLeftRadius = 10;
                pill.style.borderTopRightRadius = 10;
                pill.style.borderBottomLeftRadius = 10;
                pill.style.borderBottomRightRadius = 10;
                pill.style.backgroundColor = EditorGUIUtility.isProSkin
                    ? new Color(0.25f, 0.25f, 0.25f)
                    : new Color(0.85f, 0.85f, 0.85f);

                var label = new Label(fileName);
                label.style.unityFontStyleAndWeight = FontStyle.Normal;
                label.style.fontSize = 11;
                label.style.marginRight = 4;

                var removeBtn = new Button(() =>
                {
                    attachedFilePaths.Remove(path);
                    SessionState.SetString("DoxfenAttachedFiles", JsonConvert.SerializeObject(attachedFilePaths));
                    RefreshAttachmentBar();
                })
                {
                    text = "✖"
                };
                removeBtn.style.fontSize = 10;
                removeBtn.style.width = 18;
                removeBtn.style.height = 18;
                removeBtn.style.unityFontStyleAndWeight = FontStyle.Bold;
                removeBtn.style.alignSelf = Align.Center;
                removeBtn.style.marginLeft = 2;
                removeBtn.style.marginTop = -2;

                pill.Add(label);
                pill.Add(removeBtn);
                attachmentBar.Add(pill);
            }
        }

        private void LoadAllChatButtons()
        {
            chatListContainer.Clear();

            if (ChatManager.allChats.Count == 0)
            {
                var noChatsLabel = new Label("No chats available.")
                {
                    style =
            {
                unityTextAlign = TextAnchor.MiddleCenter,
                fontSize = 14,
                flexGrow = 1,
                color = new Color(0.7f, 0.7f, 0.7f)
            }
                };
                chatListContainer.Add(noChatsLabel);
                return;
            }

            foreach (var chat in ChatManager.allChats)
            {
                Button chatButton = new Button(() =>
                {
                    ChatManager.SetActiveChat(chat);
                    LoadChatIntoMainView(chat); // function you'll implement next
                })
                {
                    text = chat.title,
                    style =
            {
                unityTextAlign = TextAnchor.MiddleLeft,
                marginTop = 2,
                marginBottom = 2,
                paddingLeft = 10,
                fontSize = 12
            }
                };

                chatListContainer.Add(chatButton);
            }
        }
        private void LoadChatIntoMainView(ChatData chat)
        {
            messagesContainer.Clear();

            foreach (var message in chat.messages)
            {
                AddMessage(message.content, message.role == "user" ? true : false);
            }

            if (chat.messages.Count > 0)
                HideLogoIfVisible();
            else
                ShowLogoIfEmpty();

            if (isChatPanelVisible)
                ToggleChatPanelVisibility();

            messagesContainer.schedule.Execute(() =>
            {
                if (messagesContainer.parent is ScrollView scroll)
                {
                    scroll.scrollOffset = new Vector2(0, float.MaxValue);
                }
            }).ExecuteLater(100);

            deleteChatButton.visible = ChatManager.currentChat != null && ChatManager.currentChat.messages.Count > 0;
        }
        private void OnCreateNewChat()
        {
            deleteChatButton.visible = false;
            LoadAllChatButtons(); // refresh chat buttons
            messagesContainer.Clear(); // clear main area
            ChatManager.currentChat = null;
            SessionState.SetString(ChatManager.CurrentChatKey, "");
            ShowLogoIfEmpty();
        }
        private void ToggleChatPanelVisibility()
        {
            if (isChatPanelVisible)
            {
                chatSidePanel.style.display = DisplayStyle.None;
                chatOverlayBlocker.visible = false;
                isChatPanelVisible = false;
            }
            else
            {
                chatSidePanel.style.display = DisplayStyle.Flex;
                chatOverlayBlocker.visible = true;
                isChatPanelVisible = true;
            }
        }
        private void HideChatPanel()
        {
            chatSidePanel.style.display = DisplayStyle.None;
            chatOverlayBlocker.visible = false;
            isChatPanelVisible = false;
        }
        private void CreateResponsiveLogo(VisualElement root)
        {
            logoContainer = new VisualElement();
            logoContainer.style.flexGrow = 1;
            logoContainer.style.justifyContent = Justify.Center;
            logoContainer.style.alignItems = Align.Center;
            logoContainer.style.position = Position.Absolute;
            logoContainer.style.top = 0;
            logoContainer.style.bottom = 32;
            logoContainer.style.left = 0;
            logoContainer.style.right = 0;

            logoContainer.pickingMode = PickingMode.Ignore; // ✅ THIS LINE FIXES THE BUG

            logoImage = new Image();
            logoImage.image = AssetDatabase.LoadAssetAtPath<Texture2D>("Packages/com.doxfen.ai/Runtime/Internal/UI PNGs/Logos/Assisstant Logos/Ai Assisstant With Logo (Horizontal) [300].png");
            logoImage.scaleMode = ScaleMode.ScaleToFit;
            logoImage.style.width = 300;
            logoImage.style.height = 300;

            logoContainer.Add(logoImage);
            root.Add(logoContainer);

            root.RegisterCallback<GeometryChangedEvent>(_ => UpdateLogoLayout());
        }

        private void UpdateLogoLayout()
        {
            if (logoImage == null) return;

            var size = position.size;
            string path = size.x >= size.y
                ? "Packages/com.doxfen.ai/Runtime/Internal/UI PNGs/Logos/Assisstant Logos/Ai Assisstant With Logo (Horizontal) [300].png"
                : "Packages/com.doxfen.ai/Runtime/Internal/UI PNGs/Logos/Assisstant Logos/Ai Assisstant With Logo (Verticle) [300].png";

            logoImage.image = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private void ShowLogoIfEmpty()
        {
            if (messagesContainer.childCount == 0 && logoContainer != null)
            {
                logoContainer.visible = true;
            }
        }

        private void HideLogoIfVisible()
        {
            if (logoContainer != null)
            {
                logoContainer.visible = false;
            }
        }

        private void  TrySendMessage()
        {
            bool isFirstMessageInChat = (ChatManager.currentChat == null || ChatManager.currentChat.messages.Count == 0);
            HideLogoIfVisible();
            string attachmentPrefix = "";

            if (attachedFilePaths.Count > 0)
            {
                attachmentPrefix += "**Attachments:**\n";

                foreach (var path in attachedFilePaths)
                {
                    string fileName = System.IO.Path.GetFileName(path);
                    attachmentPrefix += $"- {fileName}\n";
                }

                attachmentPrefix += "\n---\n\n";

                foreach (var path in attachedFilePaths)
                {
                    string fileName = System.IO.Path.GetFileName(path);
                    string content = "";

                    try
                    {
                        content = File.ReadAllText(path);
                    }
                    catch (Exception e)
                    {
                        content = $"[Error reading file: {e.Message}]";
                    }

                    // 👇 Wrap file content in %%HIDDEN%% markers so it's excluded from UI
                    attachmentPrefix += "%%HIDDEN%%\n";
                    attachmentPrefix += $"**File: {fileName}**\n";
                    attachmentPrefix += $"**Path: {path}**\n";
                    attachmentPrefix += "```\n" + content + "\n```\n";
                    attachmentPrefix += "%%HIDDEN%%\n\n";
                }
                attachmentPrefix += "%%HIDDEN%%\n";
                attachmentPrefix += "---\n\n";
                attachmentPrefix += "%%HIDDEN%%\n\n";
            }

            string userMessage = attachmentPrefix + inputField.value.Trim();



            inputField.value = string.Empty;

            attachedFilePaths.Clear();
            SessionState.EraseString("DoxfenAttachedFiles");
            RefreshAttachmentBar();
            if (userMessage.Length > 6000)
            {
                AddMessage("❗ Message is too long. Try again with something shorter.", false);
                return;
            }

            isAwaitingResponse = true;
            responseWasCanceled = false;
            sendButton.text = "✖";
            inputField.SetEnabled(false);

            if (isFirstMessageInChat)
            {
                ChatManager.CreateNewChat();
                ChatManager.SaveChat(ChatManager.currentChat);
                AnalyticsManager.SendEvent("new_chat_created", new Dictionary<string, object>
                {
                    { "chat_title", ChatManager.currentChat.title }
                });
            }

            currentPromptId = Guid.NewGuid().ToString();
            AddMessage(userMessage, true, true); // ✅ Now this is safe after chat exists
            ChatManager.SaveChat(ChatManager.currentChat);

            requestStartTime = EditorApplication.timeSinceStartup;
            EditorApplication.update += CheckTimeout;

            currentTypingPlaceholder = AddMessage("", false);
            Label typingLabel = currentTypingPlaceholder.Q<Label>();
            string colorHex = ColorUtility.ToHtmlStringRGB(AI_TITLE_COLOR);
            currentTypingAnimation = new TypingAnimation(typingLabel, "<color=#" + colorHex + ">" + AI_NAME + " is typing</color>");
            currentTypingAnimation.Start();

            string promptId = currentPromptId;
            GeminiAI.GetAIResponse(userMessage, (response) =>
            {
                if (promptId != currentPromptId || responseWasCanceled) return;

                FinishResponse();
                AddMessage(response.Text, false, true);
                ChatManager.SaveChat(ChatManager.currentChat); // Save after adding user message

                // 🔥 If it’s the first chat and has no real title, get a title from AI
                if (isFirstMessageInChat && ChatManager.currentChat.title == "New Chat")
                {
                    GeminiAI.GetChatTitle(userMessage, (title) =>
                    {
                        ChatManager.currentChat.title = title;
                        ChatManager.SaveChat(ChatManager.currentChat);
                        LoadAllChatButtons(); // Refresh side panel
                        deleteChatButton.visible = true;
                    }, (err) =>
                    {
                        Debug.LogWarning("AI failed to generate chat title: " + err);
                    });
                }

            }, (error) =>
            {
                if (promptId != currentPromptId || responseWasCanceled) return;

                FinishResponse();
                AddMessage("Error: " + error, false);
            });
        }

        private void OnSendOrCancelClicked()
        {
            if (isAwaitingResponse)
            {
                responseWasCanceled = true;
                FinishResponse();
                CoroutineRunner.Cancel();
                AddMessage("❗ Assistant response was canceled by user.", false);
            }
            else
            {
                if(inputField.value != string.Empty)
                    TrySendMessage();
            }
        }

        private void CheckTimeout()
        {
            if (isAwaitingResponse && (EditorApplication.timeSinceStartup - requestStartTime > TIMEOUT_SECONDS))
            {
                responseWasCanceled = true;
                FinishResponse();
                AddMessage("⚠️ Assistant took too long to respond. Request timed out.", false);
            }
        }

        private void FinishResponse()
        {


            isAwaitingResponse = false;
            inputField.SetEnabled(true);
            sendButton.text = "▶";

            currentTypingAnimation?.Stop();
            if (currentTypingPlaceholder?.parent != null)
                currentTypingPlaceholder.parent.Remove(currentTypingPlaceholder);

            currentTypingPlaceholder = null;
            currentTypingAnimation = null;

            EditorApplication.update -= CheckTimeout;
        }

        private VisualElement AddMessage(string message, bool isUser, bool AddToChat = false)
        {
            bool isDark = EditorGUIUtility.isProSkin;

            var outerContainer = new VisualElement
            {
                style = {
                    marginLeft = 12,
                    marginRight = 12,
                    marginTop = 12,
                    marginBottom = 20,
                    alignItems = isUser ? Align.FlexStart : Align.FlexEnd
                }
            };

            var messageCard = new VisualElement();
            messageCard.style.maxWidth = isUser ? USER_MAX_WIDTH : AI_MAX_WIDTH;
            messageCard.style.paddingTop = 6;
            messageCard.style.paddingBottom = 6;
            messageCard.style.paddingLeft = 10;
            messageCard.style.paddingRight = 10;
            messageCard.style.borderTopLeftRadius = 6;
            messageCard.style.borderTopRightRadius = 6;
            messageCard.style.borderBottomLeftRadius = 6;
            messageCard.style.borderBottomRightRadius = 6;
            messageCard.style.backgroundColor = isUser ? (isDark ? USER_BG_DARK : USER_BG_LIGHT) : (isDark ? AI_BG_DARK : AI_BG_LIGHT);

            var senderLabel = new Label(isUser ? USER_NAME : AI_NAME);
            senderLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            senderLabel.style.fontSize = 11;
            senderLabel.style.color = isUser ? USER_TITLE_COLOR : AI_TITLE_COLOR;
            senderLabel.style.marginBottom = 2;
            messageCard.Add(senderLabel);

            var parsedElements = ParseMessageContent(message);
            foreach (var element in parsedElements)
            {
                messageCard.Add(element);
            }


            outerContainer.Add(messageCard);
            messagesContainer.Add(outerContainer);
            ((ScrollView)messagesContainer).ScrollTo(outerContainer);
            messagesContainer.schedule.Execute(() =>
            {
                if (messagesContainer.parent is ScrollView scroll)
                {
                    scroll.scrollOffset = new Vector2(0, float.MaxValue);
                }
            }).ExecuteLater(100);
            if (AddToChat)
            {
                ChatMessage msg = new ChatMessage();
                msg.role = isUser ? "user" : "model";
                msg.content = message;

                ChatManager.currentChat.messages.Add(msg);
            }
            return outerContainer;
        }
        private TextField CreateSelectableLabel(string text)
        {
            var tf = new TextField
            {
                value = text,
                isReadOnly = true,
                multiline = true
            };

            tf.style.whiteSpace = WhiteSpace.Normal;
            tf.style.fontSize = 13;
            tf.style.unityTextAlign = TextAnchor.UpperLeft;
            tf.style.backgroundColor = Color.clear;
            tf.style.borderBottomWidth = 0;
            tf.style.borderTopWidth = 0;
            tf.style.borderLeftWidth = 0;
            tf.style.borderRightWidth = 0;
            tf.style.marginTop = 0;
            tf.style.marginBottom = 0;
            tf.style.paddingTop = 0;
            tf.style.paddingBottom = 0;
            tf.style.paddingLeft = 0;
            tf.style.paddingRight = 0;
            tf.style.unityFontStyleAndWeight = FontStyle.Normal;
            tf.pickingMode = PickingMode.Position;

            var textInput = tf.Q("unity-text-input");
            if (textInput != null)
            {
                textInput.style.backgroundColor = Color.clear;
                textInput.style.borderBottomWidth = 0;
                textInput.style.borderTopWidth = 0;
                textInput.style.borderLeftWidth = 0;
                textInput.style.borderRightWidth = 0;
                textInput.style.marginTop = 0;
                textInput.style.marginBottom = 0;
                textInput.style.paddingTop = 0;
                textInput.style.paddingBottom = 0;
                textInput.style.paddingLeft = 0;
                textInput.style.paddingRight = 0;
            }

            return tf;
        }
        //Parsing the Response Message
        // Add or replace this method in your ChatAIWindow class
        private List<VisualElement> ParseMessageContent(string message)
        {
            ShowAttachmentsContent = DoxfenAISettingsWindow.ShowAttachmentContent;
            List<VisualElement> elements = new List<VisualElement>();
            bool insideCodeBlock = false;
            bool insideHiddenBlock = false;
            string codeLang = "Code";
            List<string> codeLines = new();
            string[] lines = message.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

            foreach (string line in lines)
            {
                string trimmed = line.Trim();

                if (trimmed == "%%HIDDEN%%")
                {
                    insideHiddenBlock = !insideHiddenBlock;
                    continue;
                }

                bool skipContent = insideHiddenBlock && !ShowAttachmentsContent;


                if (trimmed.StartsWith("```"))
                {
                    if (!insideCodeBlock)
                    {
                        insideCodeBlock = true;
                        codeLang = trimmed.Length > 3 ? trimmed.Substring(3).Trim() : "Code";
                        codeLines.Clear();
                    }
                    else
                    {
                        insideCodeBlock = false;
                        if (!skipContent)
                        {
                            string code = string.Join("\n", codeLines);
                            elements.Add(CreateCodeBlock(code, codeLang));
                        }

                    }
                    continue;
                }

                if (insideCodeBlock)
                {
                    if (!skipContent && insideCodeBlock)
                    {
                        codeLines.Add(line);
                    }
                }
                else
                {
                    if (!skipContent)
                    {
                        VisualElement parsed = TryParseFormattedLine(line);
                        if (parsed != null)
                        {
                            elements.Add(parsed);
                            elements.Add(CreateSpacer()); // inject spacing
                        }
                    }
                }
            }

            // If unclosed code block
            if (insideCodeBlock && codeLines.Count > 0)
            {
                elements.Add(CreateCodeBlock(string.Join("\n", codeLines), codeLang));
            }

            return elements;
        }
        private VisualElement TryParseFormattedLine(string line)
        {
            line = line.Trim();

            if (string.IsNullOrWhiteSpace(line)) return null;

            if (line.StartsWith("### "))
                return CreateFormattedLabel(line.Substring(4), 13, FontStyle.Bold);
            if (line.StartsWith("## "))
                return CreateFormattedLabel(line.Substring(3), 14, FontStyle.Bold);
            if (line.StartsWith("# "))
                return CreateFormattedLabel(line.Substring(2), 15, FontStyle.Bold);

            if (line.StartsWith("> "))
                return CreateBlockQuote(line.Substring(2));
            
            if (line.StartsWith("- ") || line.StartsWith("* "))
                return CreateBulletPoint(line.Substring(2));

            if (line == "---" || line == "___" || line == "***")
                return CreateHorizontalRule();

            // inline **bold**, *italic*, `inline code`
            var container = new VisualElement();
            container.style.flexDirection = FlexDirection.Row;
            container.style.flexWrap = Wrap.Wrap;

            var richLabels = ParseInlineRichText(ParseInlineStyles(line));
            foreach (var label in richLabels)
            {
                container.Add(label);
            }

            return container;
        }

        private Label CreateFormattedLabel(string text, int size, FontStyle style)
        {
            var label = new Label(text);
            label.style.fontSize = size;
            label.style.unityFontStyleAndWeight = style;
            label.style.marginTop = 4;
            label.style.marginBottom = 4;
            return label;
        }

        private VisualElement CreateBlockQuote(string text)
        {
            var container = new VisualElement();
            container.style.flexDirection = FlexDirection.Row;
            container.style.marginTop = 4;
            container.style.marginBottom = 4;

            var sideBar = new VisualElement();
            sideBar.style.width = 4;
            sideBar.style.backgroundColor = new Color(0.6f, 0.6f, 0.6f);
            sideBar.style.marginRight = 6;
            container.Add(sideBar);

            var quote = CreateSelectableLabel(text);
            container.Add(quote);
            return container;
        }

        private VisualElement CreateBulletPoint(string text)
        {
            var container = new VisualElement();
            container.style.flexDirection = FlexDirection.Row;
            container.style.alignItems = Align.FlexStart;
            container.style.marginTop = 2;
            container.style.marginBottom = 2;

            var bullet = new Label("•");
            bullet.style.marginRight = 6;
            bullet.style.fontSize = 13;
            bullet.style.marginTop = 3;
            container.Add(bullet);

            var textContainer = new VisualElement();
            textContainer.style.flexDirection = FlexDirection.Column;
            textContainer.style.flexGrow = 1;

            string styled = ParseInlineStyles(text);
            var line = new VisualElement();
            line.style.flexDirection = FlexDirection.Row;
            line.style.flexWrap = Wrap.Wrap;
            line.style.flexGrow = 1;

            foreach (var label in ParseInlineRichText(styled))
            {
                label.pickingMode = PickingMode.Position;
                line.Add(label);
            }

            textContainer.Add(line);
            container.Add(textContainer);
            return container;
        }

        private VisualElement CreateHorizontalRule()
        {
            var rule = new VisualElement();
            rule.style.height = 1;
            rule.style.marginTop = 6;
            rule.style.marginBottom = 6;
            rule.style.backgroundColor = new Color(0.5f, 0.5f, 0.5f);
            return rule;
        }

        private string ParseInlineStyles(string text)
        {
            // Bold: **text**
            text = System.Text.RegularExpressions.Regex.Replace(text, "\\*\\*(.+?)\\*\\*", "<b>$1</b>", System.Text.RegularExpressions.RegexOptions.Singleline);
            // Italic: *text*
            text = System.Text.RegularExpressions.Regex.Replace(text, "(?<!\\*)\\*(?!\\*)(.+?)(?<!\\*)\\*(?!\\*)", "<i>$1</i>");
            // Inline code: `code`
            text = System.Text.RegularExpressions.Regex.Replace(text, "`([^`]+)`", "<color=#AAAAAA><i>$1</i></color>");
            return text;
        }

        private List<Label> ParseInlineRichText(string line)
        {
            List<Label> labels = new();
            string pattern = @"(<\/?b>|<\/?i>|<color=#[0-9a-fA-F]{6}>|<\/color>)";
            var parts = System.Text.RegularExpressions.Regex.Split(line, pattern);

            FontStyle currentStyle = FontStyle.Normal;
            Color? currentColor = null;

            foreach (string part in parts)
            {
                if (string.IsNullOrEmpty(part)) continue;

                if (part == "<b>") currentStyle = FontStyle.Bold;
                else if (part == "</b>") currentStyle = FontStyle.Normal;
                else if (part == "<i>") currentStyle = FontStyle.Italic;
                else if (part == "</i>") currentStyle = FontStyle.Normal;
                else if (part.StartsWith("<color="))
                {
                    string hex = part.Substring(7, 7).TrimEnd('>');
                    if (ColorUtility.TryParseHtmlString(hex, out var parsedColor))
                        currentColor = parsedColor;
                }
                else if (part == "</color>")
                {
                    currentColor = null;
                }
                else
                {
                    var label = new Label(part);
                    label.style.unityFontStyleAndWeight = currentStyle;
                    if (currentColor.HasValue)
                        label.style.color = currentColor.Value;
                    label.style.whiteSpace = WhiteSpace.Normal;
                    label.style.fontSize = 13;
                    labels.Add(label);
                }
            }

            return labels;
        }
        private VisualElement CreateSpacer()
        {
            return new VisualElement
            {
                style = {
            height = 6
        }
            };
        }

        /// ////////////////////
        private VisualElement CreateCodeBlock(string code, string language)
        {
            bool isDark = EditorGUIUtility.isProSkin;
            string replaceablePath = null;

            if (language.Contains("||"))
            {
                var parts = language.Split(new[] { "||" }, StringSplitOptions.RemoveEmptyEntries);
                language = parts[0].Trim();

                foreach (var part in parts)
                {
                    if (part.TrimStart().StartsWith("ReplaceableFor:", StringComparison.OrdinalIgnoreCase))
                    {
                        replaceablePath = part.Replace("ReplaceableFor:", "").Trim();
                    }
                }
            }

            // Outer container with rounded border and padding
            var container = new VisualElement();
            container.style.marginTop = 8;
            container.style.marginBottom = 8;
            container.style.paddingTop = 4;
            container.style.paddingBottom = 6;
            container.style.paddingLeft = 8;
            container.style.paddingRight = 8;
            container.style.borderTopLeftRadius = 10;
            container.style.borderTopRightRadius = 10;
            container.style.borderBottomLeftRadius = 10;
            container.style.borderBottomRightRadius = 10;
            container.style.backgroundColor = isDark
                ? new Color(0.15f, 0.15f, 0.15f)
                : new Color(0.94f, 0.94f, 0.94f);
            container.style.borderBottomWidth = 0;
            container.style.borderTopWidth = 0;
            container.style.borderLeftWidth = 0;
            container.style.borderRightWidth = 0;

            // Header bar with language label + copy button
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.justifyContent = Justify.SpaceBetween;
            header.style.alignItems = Align.Center;
            header.style.paddingBottom = 2;

            var langLabel = new Label(language.ToUpperInvariant());
            langLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            langLabel.style.fontSize = 10;
            langLabel.style.color = isDark ? new Color(0.7f, 0.7f, 0.7f) : new Color(0.2f, 0.2f, 0.2f);

            Button copyBtn;
            if (!string.IsNullOrEmpty(replaceablePath))
            {
                // Create "Apply" button
                copyBtn = new Button(() =>
                {
                    try
                    {
                        File.WriteAllText(replaceablePath, code);
                        AssetDatabase.Refresh();
                        Debug.Log($"✅ Applied code directly to: {replaceablePath}");
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"❌ Failed to apply code: {ex.Message}");
                    }
                })
                {
                    text = $"Apply to {Path.GetFileName(replaceablePath)}"
                };
            }
            else
            {
                // Default Copy button
                copyBtn = new Button(() =>
                {
                    EditorGUIUtility.systemCopyBuffer = code;
                    Debug.Log("Copied code to clipboard.");
                })
                {
                    text = "Copy"
                };
            }

            copyBtn.style.fontSize = 11;
            copyBtn.style.height = 22;
            copyBtn.style.paddingLeft = 10;
            copyBtn.style.paddingRight = 10;
            copyBtn.style.paddingTop = 2;
            copyBtn.style.paddingBottom = 2;
            copyBtn.style.unityFontStyleAndWeight = FontStyle.Normal;
            copyBtn.style.borderTopLeftRadius = 4;
            copyBtn.style.borderTopRightRadius = 4;
            copyBtn.style.borderBottomLeftRadius = 4;
            copyBtn.style.borderBottomRightRadius = 4;

            // Match "New chat" color scheme
            copyBtn.style.backgroundColor = isDark
                ? new Color(0.25f, 0.55f, 0.95f)  // bright bluish for dark mode
                : new Color(0.3f, 0.5f, 0.9f);    // slightly darker blue for light mode

            copyBtn.style.color = Color.white;
            copyBtn.style.borderTopRightRadius = 3;


            var moreBtn = new Button(() =>
            {
                var menu = new GenericMenu();

                // Basic generate script option
                menu.AddItem(new GUIContent("Generate Script"), false, () =>
                {
                    string ext = language.ToLowerInvariant() switch
                    {
                        "c#" or "cs" or "csharp" => "cs",
                        "javascript" or "js" => "js",
                        "python" or "py" => "py",
                        "cpp" or "c++" => "cpp",
                        "java" or "kotlin" => "java",
                        "html" or "css" => "html",
                        "xml" or "json" => "json",
                        "yaml" or "yml" => "yaml",
                        "markdown" or "md" => "md",
                        "sql" => "sql",
                        "bash" or "sh" => "sh",
                        "ruby" => "rb",
                        _ => "txt"
                    };

                    string className = ExtractClassName(code);
                    string defaultName = !string.IsNullOrEmpty(className) ? $"{className}" : "GeneratedScript";

                    string path = EditorUtility.SaveFilePanel(
                        "Save Script File",
                        Application.dataPath,
                        defaultName,
                        ext
                    );

                    if (!string.IsNullOrEmpty(path))
                    {
                        try
                        {
                            System.IO.File.WriteAllText(path, code);
                            AssetDatabase.Refresh();
                            Debug.Log($"Script saved to: {path}");
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError($"Failed to save script: {ex.Message}");
                        }
                    }
                });

                // Conditional: attach to selected GameObject if one is selected
                if (Selection.activeGameObject != null)
                {
                    string objectName = Selection.activeGameObject.name;

                    menu.AddItem(new GUIContent($"Generate and Attach Script to \"{objectName}\""), false, () =>
                    {
                        string className = ExtractClassName(code);
                        string defaultName = !string.IsNullOrEmpty(className) ? $"{className}.cs" : "GeneratedScript.cs";
                        string path = EditorUtility.SaveFilePanel("Save and Attach Script", Application.dataPath, defaultName, "cs");

                        if (string.IsNullOrEmpty(path))
                            return;

                        try
                        {
                            // Save the file
                            System.IO.File.WriteAllText(path, code);
                            AssetDatabase.Refresh();

                            string relativePath = "Assets" + path.Replace(Application.dataPath, "").Replace("\\", "/");
                            GameObject target = Selection.activeGameObject;

                            if (target != null)
                            {
                                // 🔥 Store pending attach data to survive domain reload
#if UNITY_EDITOR
                                AutoAttachGeneratedScript.SetPending(relativePath, target.name);
#endif

                                Debug.Log($"💾 Script saved. Will attach to '{target.name}' after compile.");
                            }
                            else
                            {
                                Debug.LogWarning("⚠️ No object selected. Script saved but won't be auto-attached.");
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError($"❌ Failed to generate/attach script: {ex.Message}");
                        }
                    });

                }

                if (!string.IsNullOrEmpty(replaceablePath))
                {
                    menu.AddItem(new GUIContent("Copy Code"), false, () =>
                    {
                        EditorGUIUtility.systemCopyBuffer = code;
                        Debug.Log("Copied code to clipboard.");
                    });
                }

                menu.ShowAsContext();
            })
            {
                text = "⋯"
            };
            // Style the button to look minimal — no background
            moreBtn.style.fontSize = 14;
            moreBtn.style.unityFontStyleAndWeight = FontStyle.Bold;
            moreBtn.style.backgroundColor = Color.clear;
            moreBtn.style.borderBottomWidth = 0;
            moreBtn.style.borderTopWidth = 0;
            moreBtn.style.borderLeftWidth = 0;
            moreBtn.style.borderRightWidth = 0;
            moreBtn.style.marginLeft = 4;
            moreBtn.style.color = langLabel.style.color; // match theme


            header.Add(langLabel);

            var rightGroup = new VisualElement();
            rightGroup.style.flexDirection = FlexDirection.Row;
            rightGroup.Add(moreBtn);
            rightGroup.Add(copyBtn);

            header.Add(rightGroup);

            // Code label (non-editable, selectable)
            var codeField = new TextField
            {
                value = code,
                isReadOnly = true,
                multiline = true
            };

            //codeField.style.whiteSpace = WhiteSpace.Pre;
            codeField.style.fontSize = 12;
            codeField.style.unityFontStyleAndWeight = FontStyle.Normal;
            codeField.style.unityTextAlign = TextAnchor.UpperLeft;
            codeField.style.backgroundColor = Color.clear;
            codeField.style.color = isDark ? new Color(0.95f, 0.95f, 0.95f) : new Color(0.1f, 0.1f, 0.1f);
            codeField.style.marginTop = 4;
            codeField.style.marginBottom = 0;
            codeField.style.paddingTop = 0;
            codeField.style.paddingBottom = 0;
            codeField.style.borderBottomWidth = 0;
            codeField.style.borderTopWidth = 0;
            codeField.style.borderLeftWidth = 0;
            codeField.style.borderRightWidth = 0;
            codeField.style.borderRightColor = Color.clear;
            codeField.style.borderTopColor = Color.clear;
            codeField.style.borderLeftColor = Color.clear;
            codeField.style.borderBottomColor = Color.clear;

            var input = codeField.Q("unity-text-input");
            if (input != null)
            {
                input.style.backgroundColor = Color.clear;
                input.style.color = codeField.style.color;

                // Add these to remove all border + radius
                input.style.borderBottomWidth = 0;
                input.style.borderTopWidth = 0;
                input.style.borderLeftWidth = 0;
                input.style.borderRightWidth = 0;
                input.style.borderBottomLeftRadius = 0;
                input.style.borderBottomRightRadius = 0;
                input.style.borderTopLeftRadius = 0;
                input.style.borderTopRightRadius = 0;

                // Also reset any box-shadow or extra margin if needed
                input.style.marginTop = 0;
                input.style.marginBottom = 0;
                input.style.marginLeft = 0;
                input.style.marginRight = 0;
            }

            container.Add(header);
            container.Add(codeField);

            return container;
        }
        private static string ExtractClassName(string code)
        {
            var match = System.Text.RegularExpressions.Regex.Match(code, @"\bclass\s+([A-Za-z_][A-Za-z0-9_]*)");
            return match.Success ? match.Groups[1].Value : null;
        }
    }

    public class TypingAnimation
    {
        private readonly Label label;
        private readonly string baseText;
        private int dotCount;
        private double lastUpdateTime;
        private bool isActive;

        public TypingAnimation(Label targetLabel, string baseText = "Assistant is typing")
        {
            label = targetLabel;
            this.baseText = baseText;
        }

        public void Start()
        {
            dotCount = 0;
            isActive = true;
            lastUpdateTime = EditorApplication.timeSinceStartup;
            EditorApplication.update += OnUpdate;
        }

        public void Stop()
        {
            isActive = false;
            EditorApplication.update -= OnUpdate;
        }

        private void OnUpdate()
        {
            if (!isActive || label == null || label.panel == null)
            {
                Stop();
                return;
            }

            double now = EditorApplication.timeSinceStartup;
            if (now - lastUpdateTime >= 0.4)
            {
                label.text = baseText + new string('.', dotCount % 4);
                dotCount++;
                lastUpdateTime = now;
            }
        }
    }
}