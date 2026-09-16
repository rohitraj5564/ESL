using log4net;
using System;
using System.Reflection;

namespace ESL_Api.Global
{
    public static class Logger
    {
        private static readonly ILog Log =
        LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        public static void Info(string message)
        {
            Log.Info(message);
        }
        public static void Warn(string message)
        {
            Log.Warn(message);
        }
        public static void Error(string message, Exception ex = null)
        {
            if (ex != null)
                Log.Error(message, ex);
            else
                Log.Error(message);
        }
        public static void Fatal(string message, Exception ex = null)
        {
            if (ex != null)
                Log.Fatal(message, ex);
            else
                Log.Fatal(message);
        }
        public static void LogRequest(string endpoint, string userID, string details = "")
        {
            Log.Info($"[REQUEST] {endpoint} | UserID: {userID} | {details}");
        }
        public static void LogResponse(string endpoint, bool status, string message)
        {
            if (status)
                Log.Info($"[RESPONSE] {endpoint} | Status: SUCCESS | {message}");
            else
                Log.Warn($"[RESPONSE] {endpoint} | Status: FAILED | {message}");
        }
        public static void LogDBError(string methodName, Exception ex)
        {
            Log.Error($"[DB_ERROR] {methodName} | Error: {ex.Message}", ex);
        }
    }
}