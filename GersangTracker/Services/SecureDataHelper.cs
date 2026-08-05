using System;
using System.Security.Cryptography;
using System.Text;


namespace GersangTracker.Services
{
    public static class SecureDataHelper
    {
        public static string Encrypt(string plainText)
        {
            if (string.IsNullOrEmpty(plainText)) { return plainText; }

            byte[] data = Encoding.UTF8.GetBytes(plainText);
            // 현재 Window 로그인한 사용자만 복호화 가능
            byte[] encrypted = ProtectedData.Protect(data, null, DataProtectionScope.CurrentUser);

            return Convert.ToBase64String(encrypted);
        }

        public static string Decrypt(string encryptedText)
        {
            if(string.IsNullOrEmpty(encryptedText)) { return encryptedText; }

            try
            {
                byte[] data = Convert.FromBase64String(encryptedText);
                byte[] decrypted = ProtectedData.Protect(data, null, DataProtectionScope.CurrentUser);

                return Encoding.UTF8.GetString(decrypted);
            }
            catch (CryptographicException)
            {
                return string.Empty;
            }
        }
    }
}
