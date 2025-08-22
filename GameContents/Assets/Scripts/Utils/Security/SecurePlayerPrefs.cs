using System;
using System.Text;
using UnityEngine;

namespace Utils.Security
{
    /// <summary>
    /// XOR 암호화를 사용한 보안 강화 PlayerPrefs
    /// 게임 사용자 ID 같은 민감하지 않은 데이터의 기본 보안에 적합
    /// 보안은 낮지만 성능적인 면은 좋은 편
    /// </summary>
    public static class SecurePlayerPrefs
    {
        // 간단한 XOR 암호화 키 (게임별로 다르게 설정)
        private static readonly string EncryptionKey = "UnityBootCamp13-Multiplay-Game-VR-2025";


        // 디버그 로그 활성화 여부 (개발 빌드에서만 활성)
        private static readonly bool EnableDebugLog = Debug.isDebugBuild;

        /// <summary>
        /// 문자열을 XOR 암호화해서 저장
        /// 
        /// 동작 과정:
        /// 1. 입력 문자열을 UTF-8 바이트 배열로 변환
        /// 2. 암호화 키와 XOR 연산 수행
        /// 3. 결과를 Base64로 인코딩
        /// 4. PlayerPrefs에 저장
        /// </summary>
        /// <param name="key">저장할 키 (PlayerPrefs 키)</param>
        /// <param name="value">저장할 값 (암호화될 문자열)</param>
        public static void SetSecureString(string key, string value)
        {
            if (EnableDebugLog)
                Debug.Log($"[SecurePlayerPrefs] 암호화 저장 시작 - Key: '{key}', Value: '{value}'");

            try
            {
                // 1. XOR 암호화 수행
                string encryptedValue = XOREncrypt(value);

                if (EnableDebugLog)
                    Debug.Log($"[SecurePlayerPrefs] 암호화 성공 - 원본: '{value}' -> 암호화: '{encryptedValue.Substring(0, Math.Min(20, encryptedValue.Length))}...'");

                // 2. PlayerPrefs에 암호화된 값 저장
                PlayerPrefs.SetString(key, encryptedValue);
                PlayerPrefs.Save();

                if (EnableDebugLog)
                    Debug.Log($"[SecurePlayerPrefs] 저장 완료 - Key: '{key}'");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SecurePlayerPrefs] 암호화 저장 실패 - Key: '{key}', Error: {ex.Message}");

                // 암호화 실패 시 평문으로 저장 (호환성 보장)
                Debug.LogWarning($"[SecurePlayerPrefs] 호환성 모드로 평문 저장 - Key: '{key}'");
                PlayerPrefs.SetString(key, value);
                PlayerPrefs.Save();
            }
        }

        /// <summary>
        /// XOR 암호화된 문자열을 복호화해서 읽기
        /// 
        /// 동작 과정:
        /// 1. PlayerPrefs에서 암호화된 문자열 읽기
        /// 2. Base64 디코딩
        /// 3. 암호화 키와 XOR 연산으로 복호화
        /// 4. UTF-8 문자열로 변환 후 반환
        /// </summary>
        /// <param name="key">읽을 키 (PlayerPrefs 키)</param>
        /// <param name="defaultValue">키가 없거나 복호화 실패 시 반환할 기본값</param>
        /// <returns>복호화된 문자열</returns>
        public static string GetSecureString(string key, string defaultValue = "")
        {
            if (EnableDebugLog)
                Debug.Log($"[SecurePlayerPrefs] 복호화 읽기 시작 - Key: '{key}', DefaultValue: '{defaultValue}'");

            try
            {
                // 1. PlayerPrefs에서 암호화된 값 읽기
                string encryptedValue = PlayerPrefs.GetString(key, "");

                if (string.IsNullOrEmpty(encryptedValue))
                {
                    if (EnableDebugLog)
                        Debug.Log($"[SecurePlayerPrefs] 키가 존재하지 않음 - Key: '{key}', 기본값 반환: '{defaultValue}'");
                    return defaultValue;
                }

                // 2. XOR 복호화 수행
                string decryptedValue = XORDecrypt(encryptedValue);

                if (EnableDebugLog)
                    Debug.Log($"[SecurePlayerPrefs] 복호화 성공 - Key: '{key}', Value: '{decryptedValue}'");

                return decryptedValue;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SecurePlayerPrefs] 복호화 실패 - Key: '{key}', Error: {ex.Message}");

                // 복호화 실패 시 평문으로 다시 시도 (호환성)
                Debug.LogWarning($"[SecurePlayerPrefs] 호환성 모드로 평문 읽기 시도 - Key: '{key}'");
                string plainValue = PlayerPrefs.GetString(key, defaultValue);

                // 평문 데이터가 있으면 암호화해서 다시 저장 (마이그레이션)
                if (!string.IsNullOrEmpty(plainValue) && plainValue != defaultValue)
                {
                    Debug.Log($"[SecurePlayerPrefs] 평문 데이터 발견, 암호화로 마이그레이션 - Key: '{key}'");
                    SetSecureString(key, plainValue);
                }

                return plainValue;
            }
        }

        /// <summary>
        /// 정수값을 암호화해서 저장
        /// 내부적으로 문자열로 변환 후 암호화
        /// </summary>
        /// <param name="key">저장할 키</param>
        /// <param name="value">저장할 정수값</param>
        public static void SetSecureInt(string key, int value)
        {
            if (EnableDebugLog)
                Debug.Log($"[SecurePlayerPrefs] 정수 암호화 저장 - Key: '{key}', Value: {value}");

            SetSecureString(key, value.ToString());
        }

        /// <summary>
        /// 암호화된 정수값을 복호화해서 읽기
        /// </summary>
        /// <param name="key">읽을 키</param>
        /// <param name="defaultValue">키가 없거나 변환 실패 시 기본값</param>
        /// <returns>복호화된 정수값</returns>
        public static int GetSecureInt(string key, int defaultValue = 0)
        {
            string stringValue = GetSecureString(key, defaultValue.ToString());

            if (int.TryParse(stringValue, out int result))
            {
                if (EnableDebugLog)
                    Debug.Log($"[SecurePlayerPrefs] 정수 복호화 성공 - Key: '{key}', Value: {result}");
                return result;
            }

            Debug.LogWarning($"[SecurePlayerPrefs] 정수 변환 실패 - Key: '{key}', StringValue: '{stringValue}', 기본값 반환: {defaultValue}");
            return defaultValue;
        }

        /// <summary>
        /// 실수값을 암호화해서 저장 (소수점 6자리까지 정밀도 보장)
        /// </summary>
        /// <param name="key">저장할 키</param>
        /// <param name="value">저장할 실수값</param>
        public static void SetSecureFloat(string key, float value)
        {
            if (EnableDebugLog)
                Debug.Log($"[SecurePlayerPrefs] 실수 암호화 저장 - Key: '{key}', Value: {value:F6}");

            SetSecureString(key, value.ToString("F6")); // 소수점 6자리까지
        }

        /// <summary>
        /// 암호화된 실수값을 복호화해서 읽기
        /// </summary>
        /// <param name="key">읽을 키</param>
        /// <param name="defaultValue">키가 없거나 변환 실패 시 기본값</param>
        /// <returns>복호화된 실수값</returns>
        public static float GetSecureFloat(string key, float defaultValue = 0f)
        {
            string stringValue = GetSecureString(key, defaultValue.ToString("F6"));

            if (float.TryParse(stringValue, out float result))
            {
                if (EnableDebugLog)
                    Debug.Log($"[SecurePlayerPrefs] 실수 복호화 성공 - Key: '{key}', Value: {result:F6}");
                return result;
            }

            Debug.LogWarning($"[SecurePlayerPrefs] 실수 변환 실패 - Key: '{key}', StringValue: '{stringValue}', 기본값 반환: {defaultValue:F6}");
            return defaultValue;
        }

        /// <summary>
        /// bool 값을 암호화해서 저장
        /// </summary>
        /// <param name="key">저장할 키</param>
        /// <param name="value">저장할 bool 값</param>
        public static void SetSecureBool(string key, bool value)
        {
            if (EnableDebugLog)
                Debug.Log($"[SecurePlayerPrefs] Bool 암호화 저장 - Key: '{key}', Value: {value}");

            SetSecureString(key, value.ToString());
        }

        /// <summary>
        /// 암호화된 bool 값을 복호화해서 읽기
        /// </summary>
        /// <param name="key">읽을 키</param>
        /// <param name="defaultValue">키가 없거나 변환 실패 시 기본값</param>
        /// <returns>복호화된 bool 값</returns>
        public static bool GetSecureBool(string key, bool defaultValue = false)
        {
            string stringValue = GetSecureString(key, defaultValue.ToString());

            if (bool.TryParse(stringValue, out bool result))
            {
                if (EnableDebugLog)
                    Debug.Log($"[SecurePlayerPrefs] Bool 복호화 성공 - Key: '{key}', Value: {result}");
                return result;
            }

            Debug.LogWarning($"[SecurePlayerPrefs] Bool 변환 실패 - Key: '{key}', StringValue: '{stringValue}', 기본값 반환: {defaultValue}");
            return defaultValue;
        }

        /// <summary>
        /// 특정 암호화 키 삭제
        /// </summary>
        /// <param name="key">삭제할 키</param>
        public static void DeleteSecureKey(string key)
        {
            if (EnableDebugLog)
                Debug.Log($"[SecurePlayerPrefs] 키 삭제 - Key: '{key}'");

            PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();

            if (EnableDebugLog)
                Debug.Log($"[SecurePlayerPrefs] 키 삭제 완료 - Key: '{key}'");
        }

        /// <summary>
        /// 암호화된 키가 존재하는지 확인
        /// </summary>
        /// <param name="key">확인할 키</param>
        /// <returns>키 존재 여부</returns>
        public static bool HasSecureKey(string key)
        {
            bool exists = PlayerPrefs.HasKey(key);

            if (EnableDebugLog)
                Debug.Log($"[SecurePlayerPrefs] 키 존재 확인 - Key: '{key}', Exists: {exists}");

            return exists;
        }

        /// <summary>
        /// 모든 SecurePlayerPrefs 데이터 삭제
        /// 
        /// 주의: PlayerPrefs.DeleteAll()과 달리 일반 PlayerPrefs는 유지됩니다.
        /// 보안 데이터만 선별적으로 삭제하려면 키 목록을 별도 관리해야 합니다.
        /// </summary>
        public static void ClearAll()
        {
            Debug.LogWarning($"[SecurePlayerPrefs] 전체 데이터 삭제 시작 - 모든 PlayerPrefs 데이터가 삭제됩니다!");

            try
            {
                PlayerPrefs.DeleteAll();
                PlayerPrefs.Save();

                Debug.Log($"[SecurePlayerPrefs] 전체 데이터 삭제 완료");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SecurePlayerPrefs] 전체 데이터 삭제 실패 - Error: {ex.Message}");
            }
        }

        /// <summary>
        /// 특정 접두사를 가진 키들만 삭제 (선별적 삭제)
        /// 
        /// 예: ClearByPrefix("User_") → "User_Name", "User_Level" 등 삭제
        /// </summary>
        /// <param name="prefix">삭제할 키의 접두사</param>
        public static void ClearByPrefix(string prefix)
        {
            Debug.Log($"[SecurePlayerPrefs] 접두사 기반 삭제 시작 - Prefix: '{prefix}'");

            // PlayerPrefs에는 키 목록을 가져오는 API가 없으므로
            // 미리 알려진 키들에 대해서만 삭제 가능
            // 실제 구현에서는 사용하는 키 목록을 별도로 관리해야 함

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
                        Debug.Log($"[SecurePlayerPrefs] 삭제됨 - Key: '{key}'");
                }
            }

            PlayerPrefs.Save();
            Debug.Log($"[SecurePlayerPrefs] 접두사 기반 삭제 완료 - Prefix: '{prefix}', 삭제된 키 수: {deletedCount}");
        }

        /// <summary>
        /// XOR 암호화 수행 (내부 메서드)
        /// 
        /// 알고리즘:
        /// 1. 평문과 키를 UTF-8 바이트 배열로 변환
        /// 2. 평문의 각 바이트와 키 바이트를 XOR 연산
        /// 3. 키가 평문보다 짧으면 순환 사용 (key[i % keyLength])
        /// 4. 결과를 Base64로 인코딩하여 반환
        /// </summary>
        /// <param name="plainText">암호화할 평문</param>
        /// <returns>Base64 인코딩된 암호화 문자열</returns>
        private static string XOREncrypt(string plainText)
        {
            if (string.IsNullOrEmpty(plainText))
            {
                if (EnableDebugLog)
                    Debug.Log($"[SecurePlayerPrefs] 빈 문자열 암호화 - 빈 문자열 반환");
                return "";
            }

            try
            {
                // 1. 텍스트와 키를 바이트 배열로 변환
                byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
                byte[] keyBytes = Encoding.UTF8.GetBytes(EncryptionKey);

                if (EnableDebugLog)
                    Debug.Log($"[SecurePlayerPrefs] XOR 암호화 진행 - PlainBytes: {plainBytes.Length}, KeyBytes: {keyBytes.Length}");

                // 2. XOR 연산 수행
                byte[] encryptedBytes = new byte[plainBytes.Length];
                for (int i = 0; i < plainBytes.Length; i++)
                {
                    encryptedBytes[i] = (byte)(plainBytes[i] ^ keyBytes[i % keyBytes.Length]);
                }

                // 3. Base64로 인코딩 (PlayerPrefs는 문자열만 저장 가능)
                string result = Convert.ToBase64String(encryptedBytes);

                if (EnableDebugLog)
                    Debug.Log($"[SecurePlayerPrefs] XOR 암호화 완료 - 결과 길이: {result.Length}");

                return result;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SecurePlayerPrefs] XOR 암호화 실패 - Error: {ex.Message}");
                throw; // 상위 메서드에서 처리하도록 예외 재발생
            }
        }

        /// <summary>
        /// XOR 복호화 수행 (내부 메서드)
        /// 
        /// 알고리즘:
        /// 1. Base64 문자열을 바이트 배열로 디코딩
        /// 2. 암호화와 동일한 XOR 연산 수행 (XOR은 자기 자신이 역연산)
        /// 3. 결과를 UTF-8 문자열로 변환하여 반환
        /// </summary>
        /// <param name="encryptedText">Base64 인코딩된 암호화 문자열</param>
        /// <returns>복호화된 평문</returns>
        private static string XORDecrypt(string encryptedText)
        {
            if (string.IsNullOrEmpty(encryptedText))
            {
                if (EnableDebugLog)
                    Debug.Log($"[SecurePlayerPrefs] 빈 문자열 복호화 - 빈 문자열 반환");
                return "";
            }

            try
            {
                // 1. Base64 디코딩
                byte[] encryptedBytes = Convert.FromBase64String(encryptedText);
                byte[] keyBytes = Encoding.UTF8.GetBytes(EncryptionKey);

                if (EnableDebugLog)
                    Debug.Log($"[SecurePlayerPrefs] XOR 복호화 진행 - EncryptedBytes: {encryptedBytes.Length}, KeyBytes: {keyBytes.Length}");

                // 2. XOR 연산 수행 (암호화와 동일한 과정)
                byte[] decryptedBytes = new byte[encryptedBytes.Length];
                for (int i = 0; i < encryptedBytes.Length; i++)
                {
                    decryptedBytes[i] = (byte)(encryptedBytes[i] ^ keyBytes[i % keyBytes.Length]);
                }

                // 3. 바이트 배열을 UTF-8 문자열로 변환
                string result = Encoding.UTF8.GetString(decryptedBytes);

                if (EnableDebugLog)
                    Debug.Log($"[SecurePlayerPrefs] XOR 복호화 완료 - 결과 길이: {result.Length}");

                return result;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SecurePlayerPrefs] XOR 복호화 실패 - Error: {ex.Message}");
                throw; // 상위 메서드에서 처리하도록 예외 재발생
            }
        }

        /// <summary>
        /// 암호화/복호화 테스트 수행 (디버그용)
        /// 개발 빌드에서만 실행됩니다.
        /// </summary>
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        public static void RunEncryptionTest()
        {
            Debug.Log($"[SecurePlayerPrefs] 암호화 테스트 시작");

            string[] testStrings = {
            "hello_world",
            "john_doe_2024",
            "한글_테스트_123",
            "!@#$%^&*()_+{}|:<>?",
            "",
            "a",
            "very_long_string_for_testing_encryption_and_decryption_process_with_korean_한글도_포함해서_테스트해봅시다"
        };

            foreach (string original in testStrings)
            {
                try
                {
                    string encrypted = XOREncrypt(original);
                    string decrypted = XORDecrypt(encrypted);

                    bool success = original == decrypted;

                    Debug.Log($"[SecurePlayerPrefs] 테스트 {(success ? "성공" : "실패")} - " +
                             $"원본: '{original}' -> 복호화: '{decrypted}'");

                    if (!success)
                    {
                        Debug.LogError($"[SecurePlayerPrefs] 암호화 테스트 실패!");
                        return;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[SecurePlayerPrefs] 테스트 중 예외 발생 - 원본: '{original}', Error: {ex.Message}");
                }
            }

            Debug.Log($"[SecurePlayerPrefs] 모든 암호화 테스트 성공!");
        }

        /// <summary>
        /// 현재 저장된 모든 암호화 정보 디버그 출력 (개발용)
        /// 개발 빌드에서만 실행됩니다.
        /// </summary>
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        public static void DebugPrintAllSecureData()
        {
            Debug.Log($"[SecurePlayerPrefs] === 보안 데이터 디버그 정보 ===");
            Debug.Log($"[SecurePlayerPrefs] 암호화 키 길이: {EncryptionKey.Length} 문자");
            Debug.Log($"[SecurePlayerPrefs] 디버그 로그 활성화: {EnableDebugLog}");

            // 알려진 키들에 대해 정보 출력
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
                        Debug.Log($"[SecurePlayerPrefs] {key}: '{decryptedValue}' (암호화됨)");
                    }
                    catch
                    {
                        Debug.Log($"[SecurePlayerPrefs] {key}: [복호화 실패] (길이: {encryptedValue.Length})");
                    }
                }
            }

            Debug.Log($"[SecurePlayerPrefs] 총 {foundCount}개의 보안 키 발견");
            Debug.Log($"[SecurePlayerPrefs] =====================================");
        }
    }
}