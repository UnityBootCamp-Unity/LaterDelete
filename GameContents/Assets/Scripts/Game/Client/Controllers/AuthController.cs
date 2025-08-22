using Cysharp.Net.Http;
using Game.Auth;
using Game.Client.Network;
using Grpc.Core;
using Grpc.Net.Client;
using System;
using System.Threading.Tasks;
using UnityEngine;
using Utils.Security;

namespace Game.Client.Controllers
{
    public class AuthController : MonoBehaviour
    {
        private AuthService.AuthServiceClient _authClient;

        async void Start()
        {
            await InitializeAsync();
        }

        async Task InitializeAsync()
        {
            _authClient = new AuthService.AuthServiceClient(GrpcConnection.channel);
        }

        public async Task<(bool success, string message)> LoginAsync(string username, string password)
        {
            try
            {
                var response = await _authClient.LoginAsync(new LoginRequest
                {
                    Username = username,
                    Password = password
                });

                if (response.Result.Success)
                {
                    GrpcConnection.jwt = response.Jwt;
                    GrpcConnection.clientInfo = response.ClientInfo;

                    // 단순하게 PlayerPrefs에 저장
                    SecurePlayerPrefs.SetSecureString("CurrentUserId", response.ClientInfo.UserId);

                    // 추가 보안 정보도 암호화해서 저장 가능
                    SecurePlayerPrefs.SetSecureString("LastLoginTime", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));
                    //SecurePlayerPrefs.SetSecureInt("ClientId", response.ClientInfo.ClientId);

                    Debug.Log($"[AuthController] User '{username}' logged in successfully (encrypted storage)");

                    GameManager.instance.ChangeState(State.LoggedIn);
                }

                return (response.Result.Success, response.Result.Message);
            }
            catch (Exception ex)
            {
                return (false, ex.ToString());
            }
        }

        public async Task<string> LogoutAsync()
        {
            try
            {
                var response = await _authClient.LogoutAsync(new LogoutRequest
                {
                    SessionId = GrpcConnection.clientInfo.SessionsId,
                });

                // 로그아웃 시 보안 정보 완전 삭제
                SecurePlayerPrefs.DeleteSecureKey("CurrentUserId");
                SecurePlayerPrefs.DeleteSecureKey("LastLoginTime");

                return "logged out.";
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AuthController] Logout failed: {ex}");
                return ex.ToString();
            }
        }

        public async Task<bool> ValidateAsync()
        {
            try
            {
                var response = await _authClient.ValidateTokenAsync(new ValidateTokenRequest
                {
                    Jwt = GrpcConnection.jwt,
                });

                if (response.IsValid)
                {
                    GrpcConnection.clientInfo = response.ClientInfo;
                    return true;
                }

                return false;
            }
            catch
            {
                return false;
            }
        }
    }
}
