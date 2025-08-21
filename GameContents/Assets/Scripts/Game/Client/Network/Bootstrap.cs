using Unity.Multiplayer;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Client.Network
{
    public static class Bootstrap
    {
        //[RuntimeInitializeOnLoadMethod]
        public static void Init()
        {
            MultiplayerRoleFlags roleflags = MultiplayerRolesManager.ActiveMultiplayerRoleMask;

            bool isServer = roleflags.HasFlag(MultiplayerRoleFlags.Server);
            bool isClient = roleflags.HasFlag(MultiplayerRoleFlags.Client);

            if (isServer && isClient)
            {
                Debug.Log($"[{nameof(Bootstrap)}] Role : Server/Client (Listen server)");
            }
            else if (isServer)
            {
                Debug.Log($"[{nameof(Bootstrap)}] Role : Server (Dedicated server)");
                SceneManager.LoadScene("InGame");
            }
            else if (isClient)
            {
                Debug.Log($"[{nameof(Bootstrap)}] Role : Client");
                SceneManager.LoadScene("Login");
            }
            else
            {
#if UNITY_EDITOR
                EditorApplication.ExitPlaymode();
#else
                Application.Quit();
#endif           
                return;
            }
        }
    }
}
