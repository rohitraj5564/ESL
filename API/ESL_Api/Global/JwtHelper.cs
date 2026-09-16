using Microsoft.IdentityModel.Tokens;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Configuration;

namespace ESL_Api.Global
{
    public static class JwtHelper
    {
        private static readonly string SecretKey =
            ConfigurationManager.AppSettings["JwtSecretKey"];
        private static readonly int ExpiryMinutes =
            int.Parse(ConfigurationManager.AppSettings["JwtExpiryMinutes"]);
        private static readonly string AesKey =
            ConfigurationManager.AppSettings["AesEncryptionKey"];

        public static string GenerateToken(string userID, string userName,
                                           int locationId, string locationName, string sessionToken = null)
        {
            var securityKey = new SymmetricSecurityKey( Encoding.UTF8.GetBytes(SecretKey));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var claimsList = new System.Collections.Generic.List<Claim>
            {
                new Claim("userID", userID ?? string.Empty),
                new Claim("userName", userName ?? string.Empty),
                new Claim("locationId", locationId.ToString()),
                new Claim("locationName", locationName ?? string.Empty),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString())
            };

            if (!string.IsNullOrEmpty(sessionToken))
            {
                claimsList.Add(new Claim("sessionToken", sessionToken));
            }

            var token = new JwtSecurityToken(
                issuer: "ESL_Api",
                audience: "ESL_Client",
                claims: claimsList,
                expires: DateTime.UtcNow.AddMinutes(ExpiryMinutes),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public static ClaimsPrincipal ValidateToken(string token)
        {
            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                var key = Encoding.UTF8.GetBytes(SecretKey);

                var validationParams = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = "ESL_Api",
                    ValidAudience = "ESL_Client",
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ClockSkew = TimeSpan.Zero
                };

                return tokenHandler.ValidateToken(token, validationParams,out _);

            }
            catch
            {
                return null;
            }
        }

        public static string Encrypt(string plainText)
        {
            if (string.IsNullOrWhiteSpace(plainText))
                return string.Empty;

            try
            {
                string combined = $"{plainText}|{AesKey}";
                byte[] bytes = Encoding.UTF8.GetBytes(combined);
                return Convert.ToBase64String(bytes);
            }
            catch
            {
                return plainText;
            }
        }

        public static string Decrypt(string encryptedText)
        {
            if (string.IsNullOrWhiteSpace(encryptedText))
                throw new Exception("Invalid credentials.");

            try
            {
                // 1. Decode from Base64
                byte[] bytes = Convert.FromBase64String(encryptedText);
                string decoded = Encoding.UTF8.GetString(bytes);

                // 2. Split by the pipe character
                string[] parts = decoded.Split('|');
                if (parts.Length >= 2 && parts[1].Trim() == AesKey.Trim())
                {
                    return parts[0].Replace("\0", "").Trim();
                }

                if (parts.Length > 0 && !string.IsNullOrWhiteSpace(parts[0]))
                {
                    return parts[0].Replace("\0", "").Trim();
                }

                return encryptedText.Trim();
            }
            catch
            {
                return encryptedText.Trim();
            }
        }
    }
}