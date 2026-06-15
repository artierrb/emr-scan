namespace EMRScan
{
    public static class Cryptograph
    {
        public static string Encrypt(string userId, string password)
        {
            if (string.IsNullOrEmpty(userId) || password == null) return "";
            var result = new System.Text.StringBuilder();
            int idLen = userId.Length;
            for (int i = 0; i < password.Length; i++)
            {
                char idChar  = userId[i % idLen];
                char pwdChar = password[i];
                result.Append(idChar == pwdChar ? idChar : (char)(idChar ^ pwdChar));
            }
            return result.ToString();
        }

        public static bool Verify(string userId, string plain, string stored)
        {
            if (stored == null) return false;
            // Use userId as-is (same case as stored in DB) — do NOT ToUpper
            return Encrypt(userId, plain) == stored;
        }
    }
}
