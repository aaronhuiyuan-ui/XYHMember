using System;
using System.Security.Cryptography;
using System.Text;

namespace XYHMember
{
    public static class PasswordHelper
    {
        private const string HashPrefix = "SHA256$";

        public static string Hash(string password)
        {
            byte[] salt = new byte[16];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
            }
            byte[] hash = SHA256.Create().ComputeHash(
                Encoding.UTF8.GetBytes(Convert.ToBase64String(salt) + password));
            return HashPrefix + Convert.ToBase64String(salt) + "$" + Convert.ToBase64String(hash);
        }

        public static bool Verify(string password, string stored)
        {
            if (string.IsNullOrEmpty(stored))
                return false;

            if (stored.StartsWith(HashPrefix))
            {
                // 新的哈希格式: SHA256$<salt>$<hash>
                string[] parts = stored.Split('$');
                if (parts.Length != 3) return false;
                byte[] salt = Convert.FromBase64String(parts[1]);
                byte[] expectedHash = SHA256.Create().ComputeHash(
                    Encoding.UTF8.GetBytes(Convert.ToBase64String(salt) + password));
                return parts[2] == Convert.ToBase64String(expectedHash);
            }

            // 兼容旧版明文密码
            return stored == password;
        }
    }
}
