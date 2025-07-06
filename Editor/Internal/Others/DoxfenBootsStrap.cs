using System;
using System.Security.Cryptography;
using System.Text;

namespace Doxfen.Systems.AI.Internal
{
    internal static class DoxfenBootsStrap
    {
        private const string EncryptedKey = "tcj4XR/8OWpTloS4Dk8hGvkYl/w5ZQ+j7cmJudbuPOULlGjb0z2eC1hZ+diYapKr";
        private const string CDD = "@DX-GeminiKEY_Unity" + "EditorTool_Unlocker-198#";

        public static string GetApiKey()
        {
            return Decrypt(EncryptedKey, CDD);
        }

        private static string Decrypt(string cipherText, string hlper)
        {
            byte[] salt = Encoding.UTF8.GetBytes("SomeFixedSaltValue");
            using var aes = Aes.Create();
            var key = new Rfc2898DeriveBytes(hlper, salt, 10000);
            aes.Key = key.GetBytes(32);
            aes.IV = key.GetBytes(16);

            byte[] inputBytes = Convert.FromBase64String(cipherText);
            using var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
            byte[] decrypted = decryptor.TransformFinalBlock(inputBytes, 0, inputBytes.Length);
            return Encoding.UTF8.GetString(decrypted);
        }
    }
}