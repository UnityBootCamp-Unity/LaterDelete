using System;
using System.Text;
using UnityEngine;

namespace Utils.Security
{
    /// <summary>
    /// XOR ��ȣȭ�� ����� ���� ��ȭ PlayerPrefs
    /// ���� ����� ID ���� �ΰ����� ���� �������� �⺻ ���ȿ� ����
    /// ������ ������ �������� ���� ���� ��
    /// </summary>
    public static class SecurePlayerPrefs
    {
        // ������ XOR ��ȣȭ Ű (���Ӻ��� �ٸ��� ����)
        private static readonly string EncryptionKey = "UnityBootCamp13-Multiplay-Game-VR-2025";


        // ����� �α� Ȱ��ȭ ���� (���� ���忡���� Ȱ��)
        private static readonly bool EnableDebugLog = Debug.isDebugBuild;

        /// <summary>
        /// ���ڿ��� XOR ��ȣȭ�ؼ� ����
        /// 
        /// ���� ����:
        /// 1. �Է� ���ڿ��� UTF-8 ����Ʈ �迭�� ��ȯ
        /// 2. ��ȣȭ Ű�� XOR ���� ����
        /// 3. ����� Base64�� ���ڵ�
        /// 4. PlayerPrefs�� ����
        /// </summary>
        /// <param name="key">������ Ű (PlayerPrefs Ű)</param>
        /// <param name="value">������ �� (��ȣȭ�� ���ڿ�)</param>
        public static void SetSecureString(string key, string value)
        {
            if (EnableDebugLog)
                Debug.Log($"[SecurePlayerPrefs] ��ȣȭ ���� ���� - Key: '{key}', Value: '{value}'");

            try
            {
                // 1. XOR ��ȣȭ ����
                string encryptedValue = XOREncrypt(value);

                if (EnableDebugLog)
                    Debug.Log($"[SecurePlayerPrefs] ��ȣȭ ���� - ����: '{value}' -> ��ȣȭ: '{encryptedValue.Substring(0, Math.Min(20, encryptedValue.Length))}...'");

                // 2. PlayerPrefs�� ��ȣȭ�� �� ����
                PlayerPrefs.SetString(key, encryptedValue);
                PlayerPrefs.Save();

                if (EnableDebugLog)
                    Debug.Log($"[SecurePlayerPrefs] ���� �Ϸ� - Key: '{key}'");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SecurePlayerPrefs] ��ȣȭ ���� ���� - Key: '{key}', Error: {ex.Message}");

                // ��ȣȭ ���� �� ������ ���� (ȣȯ�� ����)
                Debug.LogWarning($"[SecurePlayerPrefs] ȣȯ�� ���� �� ���� - Key: '{key}'");
                PlayerPrefs.SetString(key, value);
                PlayerPrefs.Save();
            }
        }

        /// <summary>
        /// XOR ��ȣȭ�� ���ڿ��� ��ȣȭ�ؼ� �б�
        /// 
        /// ���� ����:
        /// 1. PlayerPrefs���� ��ȣȭ�� ���ڿ� �б�
        /// 2. Base64 ���ڵ�
        /// 3. ��ȣȭ Ű�� XOR �������� ��ȣȭ
        /// 4. UTF-8 ���ڿ��� ��ȯ �� ��ȯ
        /// </summary>
        /// <param name="key">���� Ű (PlayerPrefs Ű)</param>
        /// <param name="defaultValue">Ű�� ���ų� ��ȣȭ ���� �� ��ȯ�� �⺻��</param>
        /// <returns>��ȣȭ�� ���ڿ�</returns>
        public static string GetSecureString(string key, string defaultValue = "")
        {
            if (EnableDebugLog)
                Debug.Log($"[SecurePlayerPrefs] ��ȣȭ �б� ���� - Key: '{key}', DefaultValue: '{defaultValue}'");

            try
            {
                // 1. PlayerPrefs���� ��ȣȭ�� �� �б�
                string encryptedValue = PlayerPrefs.GetString(key, "");

                if (string.IsNullOrEmpty(encryptedValue))
                {
                    if (EnableDebugLog)
                        Debug.Log($"[SecurePlayerPrefs] Ű�� �������� ���� - Key: '{key}', �⺻�� ��ȯ: '{defaultValue}'");
                    return defaultValue;
                }

                // 2. XOR ��ȣȭ ����
                string decryptedValue = XORDecrypt(encryptedValue);

                if (EnableDebugLog)
                    Debug.Log($"[SecurePlayerPrefs] ��ȣȭ ���� - Key: '{key}', Value: '{decryptedValue}'");

                return decryptedValue;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SecurePlayerPrefs] ��ȣȭ ���� - Key: '{key}', Error: {ex.Message}");

                // ��ȣȭ ���� �� ������ �ٽ� �õ� (ȣȯ��)
                Debug.LogWarning($"[SecurePlayerPrefs] ȣȯ�� ���� �� �б� �õ� - Key: '{key}'");
                string plainValue = PlayerPrefs.GetString(key, defaultValue);

                // �� �����Ͱ� ������ ��ȣȭ�ؼ� �ٽ� ���� (���̱׷��̼�)
                if (!string.IsNullOrEmpty(plainValue) && plainValue != defaultValue)
                {
                    Debug.Log($"[SecurePlayerPrefs] �� ������ �߰�, ��ȣȭ�� ���̱׷��̼� - Key: '{key}'");
                    SetSecureString(key, plainValue);
                }

                return plainValue;
            }
        }

        /// <summary>
        /// �������� ��ȣȭ�ؼ� ����
        /// ���������� ���ڿ��� ��ȯ �� ��ȣȭ
        /// </summary>
        /// <param name="key">������ Ű</param>
        /// <param name="value">������ ������</param>
        public static void SetSecureInt(string key, int value)
        {
            if (EnableDebugLog)
                Debug.Log($"[SecurePlayerPrefs] ���� ��ȣȭ ���� - Key: '{key}', Value: {value}");

            SetSecureString(key, value.ToString());
        }

        /// <summary>
        /// ��ȣȭ�� �������� ��ȣȭ�ؼ� �б�
        /// </summary>
        /// <param name="key">���� Ű</param>
        /// <param name="defaultValue">Ű�� ���ų� ��ȯ ���� �� �⺻��</param>
        /// <returns>��ȣȭ�� ������</returns>
        public static int GetSecureInt(string key, int defaultValue = 0)
        {
            string stringValue = GetSecureString(key, defaultValue.ToString());

            if (int.TryParse(stringValue, out int result))
            {
                if (EnableDebugLog)
                    Debug.Log($"[SecurePlayerPrefs] ���� ��ȣȭ ���� - Key: '{key}', Value: {result}");
                return result;
            }

            Debug.LogWarning($"[SecurePlayerPrefs] ���� ��ȯ ���� - Key: '{key}', StringValue: '{stringValue}', �⺻�� ��ȯ: {defaultValue}");
            return defaultValue;
        }

        /// <summary>
        /// �Ǽ����� ��ȣȭ�ؼ� ���� (�Ҽ��� 6�ڸ����� ���е� ����)
        /// </summary>
        /// <param name="key">������ Ű</param>
        /// <param name="value">������ �Ǽ���</param>
        public static void SetSecureFloat(string key, float value)
        {
            if (EnableDebugLog)
                Debug.Log($"[SecurePlayerPrefs] �Ǽ� ��ȣȭ ���� - Key: '{key}', Value: {value:F6}");

            SetSecureString(key, value.ToString("F6")); // �Ҽ��� 6�ڸ�����
        }

        /// <summary>
        /// ��ȣȭ�� �Ǽ����� ��ȣȭ�ؼ� �б�
        /// </summary>
        /// <param name="key">���� Ű</param>
        /// <param name="defaultValue">Ű�� ���ų� ��ȯ ���� �� �⺻��</param>
        /// <returns>��ȣȭ�� �Ǽ���</returns>
        public static float GetSecureFloat(string key, float defaultValue = 0f)
        {
            string stringValue = GetSecureString(key, defaultValue.ToString("F6"));

            if (float.TryParse(stringValue, out float result))
            {
                if (EnableDebugLog)
                    Debug.Log($"[SecurePlayerPrefs] �Ǽ� ��ȣȭ ���� - Key: '{key}', Value: {result:F6}");
                return result;
            }

            Debug.LogWarning($"[SecurePlayerPrefs] �Ǽ� ��ȯ ���� - Key: '{key}', StringValue: '{stringValue}', �⺻�� ��ȯ: {defaultValue:F6}");
            return defaultValue;
        }

        /// <summary>
        /// bool ���� ��ȣȭ�ؼ� ����
        /// </summary>
        /// <param name="key">������ Ű</param>
        /// <param name="value">������ bool ��</param>
        public static void SetSecureBool(string key, bool value)
        {
            if (EnableDebugLog)
                Debug.Log($"[SecurePlayerPrefs] Bool ��ȣȭ ���� - Key: '{key}', Value: {value}");

            SetSecureString(key, value.ToString());
        }

        /// <summary>
        /// ��ȣȭ�� bool ���� ��ȣȭ�ؼ� �б�
        /// </summary>
        /// <param name="key">���� Ű</param>
        /// <param name="defaultValue">Ű�� ���ų� ��ȯ ���� �� �⺻��</param>
        /// <returns>��ȣȭ�� bool ��</returns>
        public static bool GetSecureBool(string key, bool defaultValue = false)
        {
            string stringValue = GetSecureString(key, defaultValue.ToString());

            if (bool.TryParse(stringValue, out bool result))
            {
                if (EnableDebugLog)
                    Debug.Log($"[SecurePlayerPrefs] Bool ��ȣȭ ���� - Key: '{key}', Value: {result}");
                return result;
            }

            Debug.LogWarning($"[SecurePlayerPrefs] Bool ��ȯ ���� - Key: '{key}', StringValue: '{stringValue}', �⺻�� ��ȯ: {defaultValue}");
            return defaultValue;
        }

        /// <summary>
        /// Ư�� ��ȣȭ Ű ����
        /// </summary>
        /// <param name="key">������ Ű</param>
        public static void DeleteSecureKey(string key)
        {
            if (EnableDebugLog)
                Debug.Log($"[SecurePlayerPrefs] Ű ���� - Key: '{key}'");

            PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();

            if (EnableDebugLog)
                Debug.Log($"[SecurePlayerPrefs] Ű ���� �Ϸ� - Key: '{key}'");
        }

        /// <summary>
        /// ��ȣȭ�� Ű�� �����ϴ��� Ȯ��
        /// </summary>
        /// <param name="key">Ȯ���� Ű</param>
        /// <returns>Ű ���� ����</returns>
        public static bool HasSecureKey(string key)
        {
            bool exists = PlayerPrefs.HasKey(key);

            if (EnableDebugLog)
                Debug.Log($"[SecurePlayerPrefs] Ű ���� Ȯ�� - Key: '{key}', Exists: {exists}");

            return exists;
        }

        /// <summary>
        /// ��� SecurePlayerPrefs ������ ����
        /// 
        /// ����: PlayerPrefs.DeleteAll()�� �޸� �Ϲ� PlayerPrefs�� �����˴ϴ�.
        /// ���� �����͸� ���������� �����Ϸ��� Ű ����� ���� �����ؾ� �մϴ�.
        /// </summary>
        public static void ClearAll()
        {
            Debug.LogWarning($"[SecurePlayerPrefs] ��ü ������ ���� ���� - ��� PlayerPrefs �����Ͱ� �����˴ϴ�!");

            try
            {
                PlayerPrefs.DeleteAll();
                PlayerPrefs.Save();

                Debug.Log($"[SecurePlayerPrefs] ��ü ������ ���� �Ϸ�");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SecurePlayerPrefs] ��ü ������ ���� ���� - Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Ư�� ���λ縦 ���� Ű�鸸 ���� (������ ����)
        /// 
        /// ��: ClearByPrefix("User_") �� "User_Name", "User_Level" �� ����
        /// </summary>
        /// <param name="prefix">������ Ű�� ���λ�</param>
        public static void ClearByPrefix(string prefix)
        {
            Debug.Log($"[SecurePlayerPrefs] ���λ� ��� ���� ���� - Prefix: '{prefix}'");

            // PlayerPrefs���� Ű ����� �������� API�� �����Ƿ�
            // �̸� �˷��� Ű�鿡 ���ؼ��� ���� ����
            // ���� ���������� ����ϴ� Ű ����� ������ �����ؾ� ��

            string[] commonKeys = {
            "CurrentUserId", "LastLoginTime", "ClientId", "LastValidation",
            "UserLevel", "UserScore", "GameSettings", "AudioVolume"
        };

            int deletedCount = 0;

            foreach (string key in commonKeys)
            {
                if (key.StartsWith(prefix) && PlayerPrefs.HasKey(key))
                {
                    PlayerPrefs.DeleteKey(key);
                    deletedCount++;

                    if (EnableDebugLog)
                        Debug.Log($"[SecurePlayerPrefs] ������ - Key: '{key}'");
                }
            }

            PlayerPrefs.Save();
            Debug.Log($"[SecurePlayerPrefs] ���λ� ��� ���� �Ϸ� - Prefix: '{prefix}', ������ Ű ��: {deletedCount}");
        }

        /// <summary>
        /// XOR ��ȣȭ ���� (���� �޼���)
        /// 
        /// �˰���:
        /// 1. �򹮰� Ű�� UTF-8 ����Ʈ �迭�� ��ȯ
        /// 2. ���� �� ����Ʈ�� Ű ����Ʈ�� XOR ����
        /// 3. Ű�� �򹮺��� ª���� ��ȯ ��� (key[i % keyLength])
        /// 4. ����� Base64�� ���ڵ��Ͽ� ��ȯ
        /// </summary>
        /// <param name="plainText">��ȣȭ�� ��</param>
        /// <returns>Base64 ���ڵ��� ��ȣȭ ���ڿ�</returns>
        private static string XOREncrypt(string plainText)
        {
            if (string.IsNullOrEmpty(plainText))
            {
                if (EnableDebugLog)
                    Debug.Log($"[SecurePlayerPrefs] �� ���ڿ� ��ȣȭ - �� ���ڿ� ��ȯ");
                return "";
            }

            try
            {
                // 1. �ؽ�Ʈ�� Ű�� ����Ʈ �迭�� ��ȯ
                byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
                byte[] keyBytes = Encoding.UTF8.GetBytes(EncryptionKey);

                if (EnableDebugLog)
                    Debug.Log($"[SecurePlayerPrefs] XOR ��ȣȭ ���� - PlainBytes: {plainBytes.Length}, KeyBytes: {keyBytes.Length}");

                // 2. XOR ���� ����
                byte[] encryptedBytes = new byte[plainBytes.Length];
                for (int i = 0; i < plainBytes.Length; i++)
                {
                    encryptedBytes[i] = (byte)(plainBytes[i] ^ keyBytes[i % keyBytes.Length]);
                }

                // 3. Base64�� ���ڵ� (PlayerPrefs�� ���ڿ��� ���� ����)
                string result = Convert.ToBase64String(encryptedBytes);

                if (EnableDebugLog)
                    Debug.Log($"[SecurePlayerPrefs] XOR ��ȣȭ �Ϸ� - ��� ����: {result.Length}");

                return result;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SecurePlayerPrefs] XOR ��ȣȭ ���� - Error: {ex.Message}");
                throw; // ���� �޼��忡�� ó���ϵ��� ���� ��߻�
            }
        }

        /// <summary>
        /// XOR ��ȣȭ ���� (���� �޼���)
        /// 
        /// �˰���:
        /// 1. Base64 ���ڿ��� ����Ʈ �迭�� ���ڵ�
        /// 2. ��ȣȭ�� ������ XOR ���� ���� (XOR�� �ڱ� �ڽ��� ������)
        /// 3. ����� UTF-8 ���ڿ��� ��ȯ�Ͽ� ��ȯ
        /// </summary>
        /// <param name="encryptedText">Base64 ���ڵ��� ��ȣȭ ���ڿ�</param>
        /// <returns>��ȣȭ�� ��</returns>
        private static string XORDecrypt(string encryptedText)
        {
            if (string.IsNullOrEmpty(encryptedText))
            {
                if (EnableDebugLog)
                    Debug.Log($"[SecurePlayerPrefs] �� ���ڿ� ��ȣȭ - �� ���ڿ� ��ȯ");
                return "";
            }

            try
            {
                // 1. Base64 ���ڵ�
                byte[] encryptedBytes = Convert.FromBase64String(encryptedText);
                byte[] keyBytes = Encoding.UTF8.GetBytes(EncryptionKey);

                if (EnableDebugLog)
                    Debug.Log($"[SecurePlayerPrefs] XOR ��ȣȭ ���� - EncryptedBytes: {encryptedBytes.Length}, KeyBytes: {keyBytes.Length}");

                // 2. XOR ���� ���� (��ȣȭ�� ������ ����)
                byte[] decryptedBytes = new byte[encryptedBytes.Length];
                for (int i = 0; i < encryptedBytes.Length; i++)
                {
                    decryptedBytes[i] = (byte)(encryptedBytes[i] ^ keyBytes[i % keyBytes.Length]);
                }

                // 3. ����Ʈ �迭�� UTF-8 ���ڿ��� ��ȯ
                string result = Encoding.UTF8.GetString(decryptedBytes);

                if (EnableDebugLog)
                    Debug.Log($"[SecurePlayerPrefs] XOR ��ȣȭ �Ϸ� - ��� ����: {result.Length}");

                return result;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SecurePlayerPrefs] XOR ��ȣȭ ���� - Error: {ex.Message}");
                throw; // ���� �޼��忡�� ó���ϵ��� ���� ��߻�
            }
        }

        /// <summary>
        /// ��ȣȭ/��ȣȭ �׽�Ʈ ���� (����׿�)
        /// ���� ���忡���� ����˴ϴ�.
        /// </summary>
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        public static void RunEncryptionTest()
        {
            Debug.Log($"[SecurePlayerPrefs] ��ȣȭ �׽�Ʈ ����");

            string[] testStrings = {
            "hello_world",
            "john_doe_2024",
            "�ѱ�_�׽�Ʈ_123",
            "!@#$%^&*()_+{}|:<>?",
            "",
            "a",
            "very_long_string_for_testing_encryption_and_decryption_process_with_korean_�ѱ۵�_�����ؼ�_�׽�Ʈ�غ��ô�"
        };

            foreach (string original in testStrings)
            {
                try
                {
                    string encrypted = XOREncrypt(original);
                    string decrypted = XORDecrypt(encrypted);

                    bool success = original == decrypted;

                    Debug.Log($"[SecurePlayerPrefs] �׽�Ʈ {(success ? "����" : "����")} - " +
                             $"����: '{original}' -> ��ȣȭ: '{decrypted}'");

                    if (!success)
                    {
                        Debug.LogError($"[SecurePlayerPrefs] ��ȣȭ �׽�Ʈ ����!");
                        return;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[SecurePlayerPrefs] �׽�Ʈ �� ���� �߻� - ����: '{original}', Error: {ex.Message}");
                }
            }

            Debug.Log($"[SecurePlayerPrefs] ��� ��ȣȭ �׽�Ʈ ����!");
        }

        /// <summary>
        /// ���� ����� ��� ��ȣȭ ���� ����� ��� (���߿�)
        /// ���� ���忡���� ����˴ϴ�.
        /// </summary>
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        public static void DebugPrintAllSecureData()
        {
            Debug.Log($"[SecurePlayerPrefs] === ���� ������ ����� ���� ===");
            Debug.Log($"[SecurePlayerPrefs] ��ȣȭ Ű ����: {EncryptionKey.Length} ����");
            Debug.Log($"[SecurePlayerPrefs] ����� �α� Ȱ��ȭ: {EnableDebugLog}");

            // �˷��� Ű�鿡 ���� ���� ���
            string[] knownKeys = {
            "CurrentUserId", "LastLoginTime", "ClientId", "LastValidation"
        };

            int foundCount = 0;

            foreach (string key in knownKeys)
            {
                if (PlayerPrefs.HasKey(key))
                {
                    foundCount++;
                    string encryptedValue = PlayerPrefs.GetString(key, "");

                    try
                    {
                        string decryptedValue = GetSecureString(key);
                        Debug.Log($"[SecurePlayerPrefs] {key}: '{decryptedValue}' (��ȣȭ��)");
                    }
                    catch
                    {
                        Debug.Log($"[SecurePlayerPrefs] {key}: [��ȣȭ ����] (����: {encryptedValue.Length})");
                    }
                }
            }

            Debug.Log($"[SecurePlayerPrefs] �� {foundCount}���� ���� Ű �߰�");
            Debug.Log($"[SecurePlayerPrefs] =====================================");
        }
    }
}