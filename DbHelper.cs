using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;

namespace EMRScan
{
    public static class DbHelper
    {
        public static DataTable Query(string sql, params (string, object)[] parms)
        {
            using var conn = new SqlConnection(AppConfig.ConnStr);
            conn.Open();
            using var cmd = new SqlCommand(sql, conn);
            foreach (var (k, v) in parms) cmd.Parameters.AddWithValue(k, v ?? DBNull.Value);
            var dt = new DataTable();
            new SqlDataAdapter(cmd).Fill(dt);
            return dt;
        }

        public static object Scalar(string sql, params (string, object)[] parms)
        {
            using var conn = new SqlConnection(AppConfig.ConnStr);
            conn.Open();
            using var cmd = new SqlCommand(sql, conn);
            foreach (var (k, v) in parms) cmd.Parameters.AddWithValue(k, v ?? DBNull.Value);
            return cmd.ExecuteScalar();
        }

        public static int Execute(string sql, params (string, object)[] parms)
        {
            using var conn = new SqlConnection(AppConfig.ConnStr);
            conn.Open();
            using var cmd = new SqlCommand(sql, conn);
            foreach (var (k, v) in parms) cmd.Parameters.AddWithValue(k, v ?? DBNull.Value);
            return cmd.ExecuteNonQuery();
        }

        // Login — returns (userid, name, auth) or null
        public static (string userId, string name, string auth)? Login(string userId, string password)
        {
            var dt = Query(
                "SELECT USERID, NAME, ISNULL(AUTH,'0') AS AUTH, ISNULL(PASSWD,'') AS PASSWD " +
                "FROM USERT WHERE USERID=@uid",
                ("@uid", userId.Trim().ToUpper()));

            if (dt.Rows.Count == 0) return null;
            var row    = dt.Rows[0];
            string uid = row["USERID"].ToString().Trim();
            string pwd = row["PASSWD"].ToString();

            if (!Cryptograph.Verify(uid, password, pwd)) return null;
            return (uid, row["NAME"].ToString().Trim(), row["AUTH"].ToString().Trim());
        }

        // Verify OCRPK — returns (hn, formCode) or null
        public static (string hn, string formCode)? VerifyOcrPk(string ocrPk)
        {
            var dt = Query(
                "SELECT p.OCRPK, " +
                "  RTRIM(ISNULL(pt.PATID,'')) AS HN, " +
                "  RTRIM(ISNULL(cp.FORMCODE,'')) AS FORMCODE " +
                "FROM PAGET p " +
                "JOIN CHARTPAGET cp ON cp.PAGENO = p.PAGENO " +
                "JOIN TREATT t ON t.TREATNO = cp.TREATNO " +
                "JOIN PATIENTT pt ON pt.PATID = t.PATID " +
                "WHERE RTRIM(p.OCRPK) = @ocrPk",
                ("@ocrPk", ocrPk.Trim()));

            if (dt.Rows.Count == 0) return null;
            return (dt.Rows[0]["HN"].ToString().Trim(),
                    dt.Rows[0]["FORMCODE"].ToString().Trim());
        }

        // Update PAGET after file saved
        public static void UpdatePagetFile(string ocrPk, string ext, long fileSize)
        {
            Execute(
                "UPDATE PAGET SET EXTENSION=@ext, FILESIZE=@sz WHERE RTRIM(OCRPK)=@ocrPk",
                ("@ext", ext), ("@sz", fileSize), ("@ocrPk", ocrPk.Trim()));
        }

        // Get active PATHT
        public static (string pathId, string localPath)? GetActivePath()
        {
            var dt = Query(
                "SELECT TOP 1 RTRIM(PATHID) AS PATHID, RTRIM(LOCALPATH) AS LOCALPATH " +
                "FROM PATHT WHERE ACTIVE='Y'");
            if (dt.Rows.Count == 0) return null;
            return (dt.Rows[0]["PATHID"].ToString(),
                    dt.Rows[0]["LOCALPATH"].ToString());
        }

        // Get PAGENO from OCRPK
        public static long? GetPageNo(string ocrPk)
        {
            var result = Scalar(
                "SELECT PAGENO FROM PAGET WHERE RTRIM(OCRPK)=@ocrPk",
                ("@ocrPk", ocrPk.Trim()));
            if (result == null || result == DBNull.Value) return null;
            return Convert.ToInt64(result);
        }
    }
}
