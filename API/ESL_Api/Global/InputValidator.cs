using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Text.RegularExpressions;

namespace ESL_Api.Global
{
    public static class InputValidator
    {
        public static string SanitizeInput(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return string.Empty;

            input = Regex.Replace(input, "<.*?>", string.Empty);
            input = Regex.Replace(input, @"[<>""'%;()&+\\]", string.Empty);
            return input.Trim();
        }
        public static string EncodeOutput(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return string.Empty;

            return HttpUtility.HtmlEncode(input);
        }
        public static bool IsValidUsername(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
                return false;

            return Regex.IsMatch(username, @"^[a-zA-Z0-9_]{1,50}$");
        }
        public static bool IsValidPassword(string password)
        {
            if (string.IsNullOrWhiteSpace(password))
                return false;

            return password.Length >= 2 && password.Length <= 100;
        }
        public static bool ContainsSQLInjection(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return false;

            string[] sqlKeywords = {
                "--", ";--", ";", "/*", "*/", "xp_",
                "SELECT", "INSERT", "UPDATE", "DELETE",
                "DROP", "TRUNCATE", "EXEC", "EXECUTE",
                "UNION", "CAST", "CONVERT", "CHAR",
                "VARCHAR", "NVARCHAR", "ALTER", "CREATE"
            };

            foreach (var keyword in sqlKeywords)
            {
                if (input.ToUpper().Contains(keyword.ToUpper()))
                    return true;
            }
            return false;
        }
        public static bool ContainsXSS(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return false;

            string[] xssPatterns = {
                "<script", "</script>", "javascript:",
                "onerror=", "onload=", "onclick=",
                "alert(", "document.cookie",
                "<iframe", "<img", "eval("
            };

            foreach (var pattern in xssPatterns)
            {
                if (input.ToLower().Contains(pattern.ToLower()))
                    return true;
            }
            return false;
        }
    }
}