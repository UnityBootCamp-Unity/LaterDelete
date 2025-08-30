using UnityEngine;
using UnityEngine.PlayerLoop;
using Utils.Security;

namespace Assets.Scripts.Game.Client
{
    public class UserInfoCheck
    {
        public static bool CheckAuth() // 있는지 확인
        {
            var userId = SecurePlayerPrefs.GetSecureString("CurrentUserId", "");
            if (!string.IsNullOrWhiteSpace(userId))
            {
                Debug.Log($"User is authenticated with UserId: {userId}");
                return true;
            }

            return !string.IsNullOrWhiteSpace(SecurePlayerPrefs.GetSecureString("CurrentUserId", ""));
        }


    }
}
