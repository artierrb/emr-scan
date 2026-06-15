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
            // Search case-insensitive — USERT.USERID may be stored in any case
            var dt = Query(
                "SELECT USERID, NAME, ISNULL(AUTH,'0') AS AUTH, ISNULL(PASSWD,'') AS PASSWD " +
                "FROM USERT WHERE UPPER(USERID)=@uid",
                ("@uid", userId.Trim().ToUpper()));

            if (dt.Rows.Count == 0) return null;
            var row    = dt.Rows[0];
            string uid = row["USERID"].ToString().Trim(); // exact case from DB e.g. "admin"
            string pwd = row["PASSWD"].ToString();

            // Cryptograph.Verify internally calls ToUpper() on userId as salt
            // Must pass uid exactly as stored so salt matches what was used during Encrypt
            // Normal encrypted verify
            bool ok = Cryptograph.Verify(uid, password, pwd);

            // Fallback: admin only — allow plain text password stored in DB
            if (!ok && string.Equals(uid, "admin", StringComparison.OrdinalIgnoreCase))
                ok = (pwd == password);

            if (!ok) return null;
            return (uid, row["NAME"].ToString().Trim(), row["AUTH"].ToString().Trim());
        }

        // Verify OCRPK — returns (hn, formCode) or null
        // Query from OCRPRINT (source of truth) instead of PAGET (may not exist yet)
        public static (string hn, string formCode)? VerifyOcrPk(string ocrPk)
        {
            var dt = Query(
                "SELECT o.OCRPK, " +
                "  RTRIM(ISNULL(pt.PATID,'')) AS HN, " +
                "  RTRIM(ISNULL(o.FORMCODE,'')) AS FORMCODE " +
                "FROM OCRPRINT o " +
                "JOIN PATIENTT pt ON pt.PATID = o.PATID " +
                "WHERE RTRIM(o.OCRPK) = @ocrPk",
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
        // เพิ่ม IPADDRESS เพื่อใช้ประกอบ UNC path (เครื่องรันโปรแกรมกับ storage คนละเครื่อง)
        public static (string pathId, string localPath, string ipAddress)? GetActivePath()
        {
            var dt = Query(
                "SELECT TOP 1 RTRIM(PATHID) AS PATHID, RTRIM(LOCALPATH) AS LOCALPATH, " +
                "RTRIM(IPADDRESS) AS IPADDRESS " +
                "FROM PATHT WHERE ACTIVE='Y'");
            if (dt.Rows.Count == 0) return null;
            return (dt.Rows[0]["PATHID"].ToString(),
                    dt.Rows[0]["LOCALPATH"].ToString(),
                    dt.Rows[0]["IPADDRESS"].ToString());
        }

        // แปลง LOCALPATH (มี drive letter เช่น "H:\IMAGE") เป็น UNC จาก IPADDRESS
        //   "192.168.1.40" + "H:\IMAGE"      ->  "\\192.168.1.40\IMAGE"
        //   "192.168.1.40" + "H:\IMAGE\SUB"  ->  "\\192.168.1.40\IMAGE\SUB"
        // หมายเหตุ: สมมติว่าโฟลเดอร์บนสุด (IMAGE) ถูก share ด้วยชื่อเดียวกันบนเครื่องปลายทาง
        public static string BuildStoragePath(string ipAddress, string localPath)
        {
            string ip = (ipAddress ?? "").Trim();
            string p  = (localPath ?? "").Trim();

            // ถอด drive letter "X:" ออก ถ้ามี
            if (p.Length >= 2 && p[1] == ':')
                p = p.Substring(2);          // "H:\IMAGE" -> "\IMAGE"

            // กัน leading slash ซ้ำ แล้วประกอบ UNC
            p = p.TrimStart('\\', '/');      // "\IMAGE" -> "IMAGE"

            return $@"\\{ip}\{p}";           // -> "\\192.168.1.40\IMAGE"
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


        // Update OCRPRINT after scan confirmed
        public static void UpdateOcrPrint(string ocrPk, string userId)
        {
            Execute(
                "UPDATE OCRPRINT SET SCANYN='Y', SCANUSERID=@uid, SCANDATE=@dt " +
                "WHERE RTRIM(OCRPK)=@ocrPk",
                ("@uid",   userId.Length >= 10 ? userId.Substring(0, 10) : userId.PadRight(10)),
                ("@dt",    DateTime.Now),
                ("@ocrPk", ocrPk.Trim()));
        }

        // Get form page info for OCRPK
        // Returns (treatNo, formCode, pageCount, scannedPages) or null
        public static (decimal treatNo, string formCode, int pageCount, List<int> scannedPages)? GetOcrPkPageInfo(string ocrPk)
        {
            var dt = Query(@"
                SELECT
                    o.FORMCODE,
                    f.PAGECOUNT,
                    ISNULL(t.TREATNO, 0) AS TREATNO
                FROM OCRPRINT o
                JOIN FORMT f ON f.FORMCODE = o.FORMCODE
                LEFT JOIN TREATT t ON LEFT(t.OCMNUM, 10) = LEFT(o.OCMNUM, 10)
                WHERE RTRIM(o.OCRPK) = @ocrPk",
                ("@ocrPk", ocrPk.Trim()));

            if (dt.Rows.Count == 0) return null;

            string  formCode  = dt.Rows[0]["FORMCODE"].ToString().Trim();
            int     pageCount = Convert.ToInt32(dt.Rows[0]["PAGECOUNT"]);
            decimal treatNo   = dt.Rows[0]["TREATNO"] == DBNull.Value ? 0
                              : Convert.ToDecimal(dt.Rows[0]["TREATNO"]);

            var scannedDt = Query(@"
                SELECT cp.PAGE
                FROM PAGET p
                JOIN CHARTPAGET cp ON cp.PAGENO = p.PAGENO
                WHERE RTRIM(p.OCRPK) = @ocrPk
                ORDER BY cp.PAGE",
                ("@ocrPk", ocrPk.Trim()));

            var scannedPages = new List<int>();
            foreach (System.Data.DataRow row in scannedDt.Rows)
                if (row["PAGE"] != DBNull.Value)
                    scannedPages.Add(Convert.ToInt32(row["PAGE"]));

            return (treatNo, formCode, pageCount, scannedPages);
        }

        // Insert new PAGET + CHARTPAGET for a specific page sequence
        public static long? InsertPageRecord(string ocrPk, string pathId, string userId,
                                              decimal treatNo, string formCode, int pageSeq)
        {
            string cdate = DateTime.Now.ToString("yyyyMMdd");
            var result = Scalar(@"
                INSERT INTO PAGET (PATHID, CDATE, CUSERID, FILESIZE, OCRPK)
                VALUES (@pathId, @cdate, @userId, 0, @ocrPk);
                SELECT SCOPE_IDENTITY();",
                ("@pathId", pathId),
                ("@cdate",  cdate),
                ("@userId", userId.Length >= 10 ? userId.Substring(0, 10) : userId.PadRight(10)),
                ("@ocrPk",  ocrPk.Trim()));

            if (result == null || result == DBNull.Value) return null;
            long pageNo = Convert.ToInt64(result);

            Execute(@"
                INSERT INTO CHARTPAGET
                    (PAGENO, TREATNO, FORMCODE, PAGE, CDNO, CDATE, CUSERID, GRPMID, INSDATE)
                VALUES
                    (@pageNo, @treatNo, @formCode, @page, '00000', @cdate, @userId, '000', @insDate)",
                ("@pageNo",   pageNo),
                ("@treatNo",  treatNo),
                ("@formCode", formCode),
                ("@page",     pageSeq),
                ("@cdate",    cdate),
                ("@userId",   userId.Length >= 10 ? userId.Substring(0, 10) : userId.PadRight(10)),
                ("@insDate",  DateTime.Now));

            return pageNo;
        }

        // Get PAGENO for a specific page sequence of an OCRPK
        public static long? GetPageNoBySeq(string ocrPk, int pageSeq)
        {
            var result = Scalar(@"
                SELECT p.PAGENO
                FROM PAGET p
                JOIN CHARTPAGET cp ON cp.PAGENO = p.PAGENO
                WHERE RTRIM(p.OCRPK) = @ocrPk AND cp.PAGE = @page",
                ("@ocrPk", ocrPk.Trim()),
                ("@page",  pageSeq));

            if (result == null || result == DBNull.Value) return null;
            return Convert.ToInt64(result);
        }

    }
}