using Cysharp.Net.Http;
using Game.Auth;
using Game.Client.Network;
using Grpc.Core;
using Grpc.Net.Client;
using System;
using System.Threading.Tasks;
using UnityEngine;

namespace Game.Client.Controllers
{
    public class AuthController : MonoBehaviour
    {
        private AuthService.AuthServiceClient _authClient;

        async void Start()
        {
            await InitailizeAsync();
        }

        async Task InitailizeAsync()
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

                return "logged out.";
            }
            catch (Exception ex)
            {
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
