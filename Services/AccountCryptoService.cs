using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using EmailSummarizer.Models;

namespace EmailSummarizer.Services
{
    public static class AccountCryptoService
    {
        // Custom entropy for DPAPI encryption to add an extra layer of application-specific separation
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("EmailSummarizer.SecureAccounts.v1");

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = false,
            PropertyNameCaseInsensitive = true
        };

        /// <summary>
        /// Encrypts a list of EmailAccount objects into a DPAPI-protected binary payload.
        /// </summary>
        public static byte[] EncryptAccounts(List<EmailAccount> accounts)
        {
            if (accounts == null)
            {
                accounts = new List<EmailAccount>();
            }

            string json = JsonSerializer.Serialize(accounts, JsonOptions);
            byte[] plainBytes = Encoding.UTF8.GetBytes(json);

            // Encrypt using Windows DPAPI tied to the current interactive Windows user account
            return ProtectedData.Protect(plainBytes, Entropy, DataProtectionScope.CurrentUser);
        }

        /// <summary>
        /// Decrypts a DPAPI-protected binary payload into a list of EmailAccount objects.
        /// </summary>
        public static List<EmailAccount> DecryptAccounts(byte[] encryptedBytes)
        {
            if (encryptedBytes == null || encryptedBytes.Length == 0)
            {
                return new List<EmailAccount>();
            }

            try
            {
                // Decrypt using Windows DPAPI
                byte[] plainBytes = ProtectedData.Unprotect(encryptedBytes, Entropy, DataProtectionScope.CurrentUser);
                string json = Encoding.UTF8.GetString(plainBytes);

                var accounts = JsonSerializer.Deserialize<List<EmailAccount>>(json, JsonOptions);
                return accounts ?? new List<EmailAccount>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AccountCryptoService] Decryption failed: {ex.Message}");
                return new List<EmailAccount>();
            }
        }

        /// <summary>
        /// Saves the accounts to an encrypted file on disk.
        /// </summary>
        public static bool SaveToEncryptedFile(string filePath, List<EmailAccount> accounts)
        {
            try
            {
                string? dir = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                byte[] encryptedBytes = EncryptAccounts(accounts);

                // Write atomically using temporary file
                string tempFile = filePath + ".tmp";
                File.WriteAllBytes(tempFile, encryptedBytes);

                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
                File.Move(tempFile, filePath);

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AccountCryptoService] Error saving encrypted accounts: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Loads and decrypts the accounts from an encrypted file on disk.
        /// </summary>
        public static List<EmailAccount> LoadFromEncryptedFile(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    return new List<EmailAccount>();
                }

                byte[] encryptedBytes = File.ReadAllBytes(filePath);
                return DecryptAccounts(encryptedBytes);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AccountCryptoService] Error reading encrypted file: {ex.Message}");
                return new List<EmailAccount>();
            }
        }
    }
}
