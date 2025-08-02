using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using System.IO;

namespace Doxfen.Systems.AI.Internal
{
    public class DoxfenAITermsAgreementEditorWindow : EditorWindow
    {
        private const string EditorPrefsKey = "Doxfen_AITermsAccepted";
        private const string LogoAssetPath = "Packages/com.doxfen.ai/Runtime/Internal/UI PNGs/Logos/Assisstant Logos/Ai Assisstant With Logo (Verticle) [300].png";

        public static void ShowWindow()
        {
            if (EditorPrefs.GetBool(EditorPrefsKey, false)) return;

            var window = GetWindow<DoxfenAITermsAgreementEditorWindow>("Doxfen AI Terms");
            window.minSize = new Vector2(500, 600);
        }
        public static bool AreTermsAccepted() => EditorPrefs.GetBool(EditorPrefsKey, false);
        private void OnEnable()
        {
            var root = rootVisualElement;
            root.Clear();

            root.style.paddingTop = 20;
            root.style.paddingBottom = 20;
            root.style.paddingLeft = 20;
            root.style.paddingRight = 20;

            ScrollView scrollView = new ScrollView
            {
                style =
                {
                    flexGrow = 1,
                    maxHeight = 500,
                    marginBottom = 20,
                    unityOverflowClipBox = OverflowClipBox.ContentBox
                }
            };

            // Load Logo
            Texture2D logoTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(LogoAssetPath);
            if (logoTexture != null)
            {
                float aspect = (float)logoTexture.height / logoTexture.width;
                Image logoImage = new Image
                {
                    image = logoTexture,
                    scaleMode = ScaleMode.ScaleToFit,
                    style =
                    {
                        width = 256,
                        height = 256 * aspect,
                        alignSelf = Align.Center,
                        marginBottom = 20
                    }
                };
                scrollView.Add(logoImage);
            }

            // Rich Terms Text
            Label termsLabel = new Label(GetTermsText())
            {
                style =
                {
                    whiteSpace = WhiteSpace.Normal,
                    unityTextAlign = TextAnchor.UpperLeft,
                    unityFontStyleAndWeight = FontStyle.Normal,
                    fontSize = 13,
                    color = EditorGUIUtility.isProSkin ? new Color(0.85f, 0.85f, 0.85f) : Color.black,
                    paddingTop = 10,
                    paddingBottom = 10
                }
            };
            scrollView.Add(termsLabel);

            root.Add(scrollView);

            // Accept Button
            Button acceptButton = new Button(() =>
            {
                EditorPrefs.SetBool(EditorPrefsKey, true);
                EditorUtility.DisplayDialog("Thank You", "Thanks for accepting the Terms and Conditions.", "Close");
                Close();
            })
            {
                text = "Accept Terms and Conditions",
                style =
                {
                    alignSelf = Align.Center,
                    paddingTop = 10,
                    paddingBottom = 10,
                    paddingLeft = 20,
                    paddingRight = 20,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    fontSize = 14,
                    backgroundColor = EditorGUIUtility.isProSkin ? new Color(0.2f, 0.4f, 1f) : new Color(0.1f, 0.3f, 0.9f),
                    color = Color.white,
                    borderTopLeftRadius = 8,
                    borderTopRightRadius = 8,
                    borderBottomLeftRadius = 8,
                    borderBottomRightRadius = 8
                }
            };

            root.Add(acceptButton);
        }

        public void OnDisable()
        {
            if (!AreTermsAccepted())
            {
                ChatAIWindow.HideWindow();
            }
        }

        private string GetTermsText()
        {
            return
@"<b>Doxfen AI Assistant Terms and Conditions</b>

By using this Unity Editor tool ('Doxfen AI Assistant'), you agree to the following terms:

<b>Usage License</b>
- You may freely use this package in personal and commercial Unity projects.
- You may include it as a development aid but not as a separate asset.
- Redistribution, repackaging, or reverse engineering is strictly prohibited.

<b>Data Collection</b>
To improve the experience, Doxfen AI Assistant may collect minimal analytics data including:
• Which editor settings are enabled
• Chat messages sent count
• Number of chats created
• Names of chats (for UI reference only)
• Non-sensitive error logs

You can disable analytics anytime in the settings menu of Doxfen AI Assistant. No personal, project, or file-level content is ever collected.

<b>Agreement Scope</b>
- This agreement is machine-based and stored locally. You won’t be asked again on the same computer.
- This tool runs only in the Unity Editor and never affects builds or player runtime environments.

<b>Ownership</b>
All rights are reserved by Doxfen Interactive. You may not claim ownership or remove branding of this tool.

<b>Contact</b>
For support or questions: doxfeninteractive@gmail.com

Thank you for using Doxfen AI!";
        }
    }
}