using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace EMRScan
{
    public static class AppConfig
    {
        public static string DbServer   { get; private set; } = "localhost,1433";
        public static string DbName     { get; private set; } = "IMGEMR";
        public static string DbUser     { get; private set; } = "bit";
        public static string DbPass     { get; private set; } = "bitbit";
        public static string ApiBaseUrl { get; private set; } = "http://localhost:8080";

        public static string ConnStr =>
            $"Server={DbServer};Database={DbName};User Id={DbUser};Password={DbPass};" +
            "TrustServerCertificate=true;Encrypt=false;";

        public static void Load()
        {
            string cfgPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "application.properties");

            if (!File.Exists(cfgPath)) return;

            var props = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var line in File.ReadAllLines(cfgPath))
            {
                var t = line.Trim();
                if (t.StartsWith("#") || !t.Contains("=")) continue;
                int idx = t.IndexOf('=');
                props[t.Substring(0, idx).Trim()] = t.Substring(idx + 1).Trim();
            }

            if (props.TryGetValue("DB_SERVER", out var sv)) DbServer = sv;
            else if (props.TryGetValue("spring.datasource.url", out var url) && !url.Contains("${"))
            {
                var hm = Regex.Match(url, @"sqlserver://([^;]+)");
                if (hm.Success) DbServer = hm.Groups[1].Value;
                var dm = Regex.Match(url, @"databaseName=([^;]+)");
                if (dm.Success) DbName = dm.Groups[1].Value;
            }
            if (props.TryGetValue("DB_NAME",  out var dn)) DbName  = dn;
            if (props.TryGetValue("DB_USER",  out var du) ||
                props.TryGetValue("spring.datasource.username", out du)) DbUser = du;
            if (props.TryGetValue("DB_PASS",  out var dp) ||
                props.TryGetValue("spring.datasource.password", out dp)) DbPass = dp;
            if (props.TryGetValue("EMR_API_URL", out var api)) ApiBaseUrl = api;
        }
    }
}
