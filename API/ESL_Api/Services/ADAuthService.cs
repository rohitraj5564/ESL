using System;
using System.Collections.Generic;
using System.DirectoryServices;
using System.DirectoryServices.Protocols;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using ESL_Api.Global;

namespace ESL_Api.Services
{
    public class ADAuthResult
    {
        public bool Success { get; set; }
        public string UserName { get; set; }
        public string DisplayName { get; set; }
        public string UserPrincipalName { get; set; }
        public string DistinguishedName { get; set; }
        public string ErrorMessage { get; set; }
    }

    public class ADAuthService
    {
        public bool AuthenticateUser(string userName, string password)
        {
            var result = AuthenticateUserDetailed(userName, password);
            if (!result.Success)
            {
                throw new Exception(result.ErrorMessage ?? "Invalid Active Directory username or password.");
            }
            return true;
        }

        public ADAuthResult FindUserInAD(string userName)
        {
            if (string.IsNullOrWhiteSpace(userName)) return null;

            string cleanUserName = NormalizeUserName(userName);
            string adServer = Appsetting.AD_SERVER?.Trim();
            int adPort = Appsetting.AD_PORT > 0 ? Appsetting.AD_PORT : 389;
            string baseDn = Appsetting.AD_BASE_DN?.Trim();
            string bindDn = Appsetting.AD_BIND_DN?.Trim();
            string bindPassword = Appsetting.AD_BIND_PASSWORD;
            int timeoutMs = Appsetting.AD_TIMEOUT_MS > 0 ? Appsetting.AD_TIMEOUT_MS : 8000;

            if (string.IsNullOrWhiteSpace(bindDn)) return null;

            try
            {
                var identifier = new LdapDirectoryIdentifier(adServer, adPort, fullyQualifiedDnsHostName: false, connectionless: false);
                using (var connection = new LdapConnection(identifier))
                {
                    connection.Timeout = TimeSpan.FromMilliseconds(timeoutMs);
                    connection.SessionOptions.ProtocolVersion = 3;
                    connection.SessionOptions.SendTimeout = TimeSpan.FromMilliseconds(timeoutMs);
                    connection.AuthType = AuthType.Basic;
                    connection.Credential = new NetworkCredential(bindDn, bindPassword);
                    connection.Bind();

                    string escapedUser = EscapeLdapSearchFilter(cleanUserName);
                    string filter = $"(&(objectCategory=person)(objectClass=user)(|(sAMAccountName={escapedUser})(userPrincipalName={escapedUser})(displayName={escapedUser})(cn={escapedUser})(mail={escapedUser})))";

                    string[] searchBases = new[]
                    {
                        baseDn,
                        "OU=Electro Steels Limited,DC=ESL01,DC=vedantaresource,DC=local",
                        "DC=ESL01,DC=vedantaresource,DC=local"
                    };

                    foreach (var sBase in searchBases)
                    {
                        if (string.IsNullOrWhiteSpace(sBase)) continue;
                        try
                        {
                            var searchReq = new System.DirectoryServices.Protocols.SearchRequest(
                                sBase,
                                filter,
                                System.DirectoryServices.Protocols.SearchScope.Subtree,
                                "distinguishedName", "sAMAccountName", "displayName", "userPrincipalName"
                            );
                            searchReq.TimeLimit = TimeSpan.FromMilliseconds(timeoutMs);
                            var resp = (System.DirectoryServices.Protocols.SearchResponse)connection.SendRequest(searchReq);
                            if (resp.Entries.Count > 0)
                            {
                                var entry = resp.Entries[0];
                                return new ADAuthResult
                                {
                                    DistinguishedName = entry.DistinguishedName,
                                    UserName = entry.Attributes["sAMAccountName"]?.Count > 0 ? entry.Attributes["sAMAccountName"][0]?.ToString() : cleanUserName,
                                    DisplayName = entry.Attributes["displayName"]?.Count > 0 ? entry.Attributes["displayName"][0]?.ToString() : null,
                                    UserPrincipalName = entry.Attributes["userPrincipalName"]?.Count > 0 ? entry.Attributes["userPrincipalName"][0]?.ToString() : null,
                                    Success = true
                                };
                            }
                        }
                        catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warn($"[ADAuthService] FindUserInAD error for '{cleanUserName}': {ex.Message}");
            }

            return null;
        }

        public ADAuthResult AuthenticateUserDetailed(string userName, string password)
        {
            var result = new ADAuthResult();

            if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(password))
            {
                Logger.Warn("[ADAuthService] Empty username or password provided");
                result.Success = false;
                result.ErrorMessage = "Username and password are required.";
                return result;
            }

            string cleanUserName = NormalizeUserName(userName);
            result.UserName = cleanUserName;

            string adServer = Appsetting.AD_SERVER?.Trim();
            int adPort = Appsetting.AD_PORT > 0 ? Appsetting.AD_PORT : 389;
            string baseDn = Appsetting.AD_BASE_DN?.Trim();
            string domainSuffix = Appsetting.AD_DOMAIN_SUFFIX?.Trim();
            string bindDn = Appsetting.AD_BIND_DN?.Trim();
            string bindPassword = Appsetting.AD_BIND_PASSWORD;
            int timeoutMs = Appsetting.AD_TIMEOUT_MS > 0 ? Appsetting.AD_TIMEOUT_MS : 8000;

            Logger.Info($"[ADAuthService] Starting AD authentication for '{cleanUserName}' on Server: {adServer}:{adPort}");

            string userDn = null;
            string userPrincipalName = null;
            string discoveredSam = cleanUserName;
            string displayName = null;

            // Step 1: Query AD via Service Account to locate user object and check status
            if (!string.IsNullOrWhiteSpace(bindDn))
            {
                try
                {
                    var identifier = new LdapDirectoryIdentifier(adServer, adPort, fullyQualifiedDnsHostName: false, connectionless: false);
                    using (var connection = new LdapConnection(identifier))
                    {
                        connection.Timeout = TimeSpan.FromMilliseconds(timeoutMs);
                        connection.SessionOptions.ProtocolVersion = 3;
                        connection.SessionOptions.SendTimeout = TimeSpan.FromMilliseconds(timeoutMs);
                        connection.AuthType = AuthType.Basic;
                        connection.Credential = new NetworkCredential(bindDn, bindPassword);
                        connection.Bind();

                        string escapedUser = EscapeLdapSearchFilter(cleanUserName);
                        string filter = $"(&(objectCategory=person)(objectClass=user)(|(sAMAccountName={escapedUser})(userPrincipalName={escapedUser})(displayName={escapedUser})(cn={escapedUser})(mail={escapedUser})))";

                        string[] searchBases = new[]
                        {
                            baseDn,
                            "OU=Electro Steels Limited,DC=ESL01,DC=vedantaresource,DC=local",
                            "DC=ESL01,DC=vedantaresource,DC=local"
                        };

                        foreach (var sBase in searchBases)
                        {
                            if (string.IsNullOrWhiteSpace(sBase)) continue;

                            try
                            {
                                var searchReq = new System.DirectoryServices.Protocols.SearchRequest(
                                    sBase,
                                    filter,
                                    System.DirectoryServices.Protocols.SearchScope.Subtree,
                                    "distinguishedName", "sAMAccountName", "displayName", "userPrincipalName", "userAccountControl", "lockoutTime"
                                );
                                searchReq.TimeLimit = TimeSpan.FromMilliseconds(timeoutMs);

                                var resp = (System.DirectoryServices.Protocols.SearchResponse)connection.SendRequest(searchReq);
                                if (resp.Entries.Count > 0)
                                {
                                    var entry = resp.Entries[0];
                                    userDn = entry.DistinguishedName;
                                    result.DistinguishedName = userDn;

                                    if (entry.Attributes["sAMAccountName"]?.Count > 0)
                                    {
                                        discoveredSam = entry.Attributes["sAMAccountName"][0]?.ToString();
                                        result.UserName = discoveredSam;
                                    }

                                    if (entry.Attributes["displayName"]?.Count > 0)
                                    {
                                        displayName = entry.Attributes["displayName"][0]?.ToString();
                                        result.DisplayName = displayName;
                                    }

                                    if (entry.Attributes["userPrincipalName"]?.Count > 0)
                                    {
                                        userPrincipalName = entry.Attributes["userPrincipalName"][0]?.ToString();
                                        result.UserPrincipalName = userPrincipalName;
                                    }

                                    if (entry.Attributes["userAccountControl"]?.Count > 0)
                                    {
                                        if (int.TryParse(entry.Attributes["userAccountControl"][0]?.ToString(), out int uac))
                                        {
                                            bool isDisabled = (uac & 2) != 0;
                                            bool isLocked = (uac & 16) != 0;
                                            if (isDisabled)
                                            {
                                                Logger.Warn($"[ADAuthService] Account {discoveredSam} is DISABLED in AD.");
                                                result.Success = false;
                                                result.ErrorMessage = "Active Directory account is disabled. Contact IT.";
                                                return result;
                                            }
                                        }
                                    }

                                    Logger.Info($"[ADAuthService] User located in AD: DN={userDn}, SAM={discoveredSam}, UPN={userPrincipalName}, Display={displayName}");
                                    break;
                                }
                            }
                            catch (Exception sEx)
                            {
                                Logger.Warn($"[ADAuthService] Search in '{sBase}' error: {sEx.Message}");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warn($"[ADAuthService] Service account search error: {ex.Message}");
                }
            }

            // Step 2: Validate user credentials against AD
            var authCandidates = new List<(string logonName, AuthType authType, string description)>();

            if (!string.IsNullOrWhiteSpace(domainSuffix) && !string.IsNullOrWhiteSpace(discoveredSam))
            {
                authCandidates.Add(($"{domainSuffix}\\{discoveredSam}", AuthType.Negotiate, "Domain Logon (DOMAIN\\sAMAccountName)"));
            }

            if (!string.IsNullOrWhiteSpace(userPrincipalName))
            {
                authCandidates.Add((userPrincipalName, AuthType.Negotiate, "User Principal Name (UPN)"));
            }

            if (!string.IsNullOrWhiteSpace(userDn))
            {
                authCandidates.Add((userDn, AuthType.Basic, "Distinguished Name (LDAP Simple Bind)"));
            }

            authCandidates.Add((discoveredSam, AuthType.Negotiate, "Plain sAMAccountName"));

            string lastDetailedError = null;

            foreach (var candidate in authCandidates)
            {
                string logonName = candidate.logonName;
                AuthType authType = candidate.authType;
                string description = candidate.description;

                try
                {
                    Logger.Info($"[ADAuthService] Validating credentials via {description}: '{logonName}' ({authType})");
                    var identifier = new LdapDirectoryIdentifier(adServer, adPort, fullyQualifiedDnsHostName: false, connectionless: false);
                    using (var userConn = new LdapConnection(identifier))
                    {
                        userConn.Timeout = TimeSpan.FromMilliseconds(timeoutMs);
                        userConn.SessionOptions.ProtocolVersion = 3;
                        userConn.SessionOptions.SendTimeout = TimeSpan.FromMilliseconds(timeoutMs);
                        userConn.AuthType = authType;
                        userConn.Credential = new NetworkCredential(logonName, password);
                        userConn.Bind();

                        Logger.Info($"[ADAuthService] SUCCESS! Authenticated via {description} for '{logonName}'");
                        result.Success = true;
                        return result;
                    }
                }
                catch (LdapException lex)
                {
                    var adErr = ParseAdError(lex.ServerErrorMessage, lex.ErrorCode);
                    string subCode = adErr.code;
                    string desc = adErr.description;
                    lastDetailedError = desc;
                    Logger.Warn($"[ADAuthService] LdapException ({lex.ErrorCode}): {desc} | AD Msg: {lex.ServerErrorMessage?.Trim()}");

                    if (subCode == "532" || subCode == "533" || subCode == "775")
                    {
                        result.Success = false;
                        result.ErrorMessage = desc;
                        return result;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warn($"[ADAuthService] Auth attempt error for '{logonName}': {ex.Message}");
                }
            }

            // Fallback: Windows DirectoryEntry with Domain format
            try
            {
                string domainLogon = !string.IsNullOrWhiteSpace(domainSuffix) ? $"{domainSuffix}\\{discoveredSam}" : discoveredSam;
                Logger.Info($"[ADAuthService] Fallback DirectoryEntry bind: '{domainLogon}'");
                string rootPath = $"LDAP://{adServer}:{adPort}";
                using (var entry = new DirectoryEntry(rootPath, domainLogon, password, AuthenticationTypes.Secure | AuthenticationTypes.ServerBind))
                {
                    object native = entry.NativeObject;
                    Logger.Info($"[ADAuthService] SUCCESS! Authenticated via DirectoryEntry for '{domainLogon}'");
                    result.Success = true;
                    return result;
                }
            }
            catch (DirectoryServicesCOMException comEx)
            {
                var adErr = ParseAdError(null, comEx.ErrorCode);
                if (lastDetailedError == null) lastDetailedError = adErr.description;
                Logger.Warn($"[ADAuthService] DirectoryServicesCOMException (0x{comEx.ErrorCode:X8}): {adErr.description}");
            }
            catch (Exception ex)
            {
                Logger.Warn($"[ADAuthService] DirectoryEntry fallback error: {ex.Message}");
            }

            result.Success = false;
            result.ErrorMessage = lastDetailedError ?? "Invalid Active Directory username or password.";
            return result;
        }

        private (string code, string description) ParseAdError(string serverErrorMessage, int comErrorCode = 0)
        {
            if (!string.IsNullOrEmpty(serverErrorMessage))
            {
                var match = Regex.Match(serverErrorMessage, @"data\s+([0-9a-fA-F]{3,4})");
                if (match.Success)
                {
                    string subCode = match.Groups[1].Value.ToLower();
                    string desc;
                    switch (subCode)
                    {
                        case "525":
                            desc = "User not found in Active Directory (data 525).";
                            break;
                        case "52e":
                            desc = "Invalid password for this Active Directory account (data 52e).";
                            break;
                        case "530":
                            desc = "Logon not permitted at this time (data 530).";
                            break;
                        case "531":
                            desc = "Logon not permitted from this workstation (data 531).";
                            break;
                        case "532":
                            desc = "Active Directory password has expired (data 532).";
                            break;
                        case "533":
                            desc = "Active Directory account is disabled (data 533).";
                            break;
                        case "701":
                            desc = "Active Directory account has expired (data 701).";
                            break;
                        case "773":
                            desc = "User must reset password before logging in (data 773).";
                            break;
                        case "775":
                            desc = "Active Directory account is locked out (data 775).";
                            break;
                        default:
                            desc = $"Active Directory authentication failure (data {subCode}).";
                            break;
                    }
                    return (subCode, desc);
                }
            }

            if (comErrorCode == unchecked((int)0x8007052E)) return ("52e", "Invalid Active Directory username or password.");
            if (comErrorCode == unchecked((int)0x80070775)) return ("775", "Active Directory account is locked out.");
            if (comErrorCode == unchecked((int)0x80070773)) return ("532", "Active Directory password has expired.");
            if (comErrorCode == unchecked((int)0x8007203A)) return (null, "Active Directory server is down or unreachable.");

            return (null, "Invalid Active Directory username or password.");
        }

        private string NormalizeUserName(string userName)
        {
            if (string.IsNullOrWhiteSpace(userName))
                return string.Empty;

            string cleaned = userName.Trim();

            int slashIndex = cleaned.IndexOf('\\');
            if (slashIndex >= 0 && slashIndex < cleaned.Length - 1)
            {
                cleaned = cleaned.Substring(slashIndex + 1);
            }
            else
            {
                int fwdSlashIndex = cleaned.IndexOf('/');
                if (fwdSlashIndex >= 0 && fwdSlashIndex < cleaned.Length - 1)
                {
                    cleaned = cleaned.Substring(fwdSlashIndex + 1);
                }
            }

            int atIndex = cleaned.IndexOf('@');
            if (atIndex > 0)
            {
                cleaned = cleaned.Substring(0, atIndex);
            }

            return cleaned.Trim();
        }

        private string EscapeLdapSearchFilter(string input)
        {
            if (string.IsNullOrEmpty(input))
                return string.Empty;

            StringBuilder sb = new StringBuilder(input.Length);
            foreach (char c in input)
            {
                switch (c)
                {
                    case '\\': sb.Append("\\5c"); break;
                    case '*':  sb.Append("\\2a"); break;
                    case '(':  sb.Append("\\28"); break;
                    case ')':  sb.Append("\\29"); break;
                    case '\0': sb.Append("\\00"); break;
                    case '/':  sb.Append("\\2f"); break;
                    default:   sb.Append(c); break;
                }
            }
            return sb.ToString();
        }
    }
}