using Game.Auth;
using Game.Server.Network;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Server.Network;

namespace Game.Server.Auth
{
    class AuthServiceImpl : AuthService.AuthServiceBase
    {
        public AuthServiceImpl(IdGenerator clientIdGenerator)
        {
            _clientIdGenerator = clientIdGenerator;
        }

        private IdGenerator _clientIdGenerator;

        public override async Task<LoginResponse> Login(LoginRequest request, ServerCallContext context)
        {
            try
            {
                var response = await AuthFacade.LoginAsync(PersistenceApiSettings.BASE_URL, request.Username, request.Password, context.CancellationToken);

                return new LoginResponse()
                {
                    ClientInfo = new ClientInfo
                    {
                        ClientId = _clientIdGenerator.AssignId(),
                        SessionsId = response.sessionId,
                        ConnectedAt = Timestamp.FromDateTime(response.createdAt),
                        UserId = request.Username
                    },
                    Jwt = response.jwt,
                    Result = new Result
                    {
                        Success = true,
                        Message = "Loged in.",
                        Code = (int)response.statusCode
                    }
                };
            }
            catch (HttpRequestException httpRequestEx)
            {
                return new LoginResponse()
                {
                    ClientInfo = new ClientInfo
                    {
                        ClientId = -1, // TODO : ClientIdGenerator 가지고 부여해줘야함 
                        SessionsId = "Error",
                        ConnectedAt = default,
                        UserId = "Error"
                    },
                    Jwt = "Error",
                    Result = new Result
                    {
                        Success = false,
                        Message = $"Failed to Log in. {httpRequestEx.ToString()}",
                        Code = (int)httpRequestEx.StatusCode,
                    }
                };
            }
            catch (Exception ex)
            {
                return new LoginResponse()
                {
                    ClientInfo = new ClientInfo
                    {
                        ClientId = -1, // TODO : ClientIdGenerator 가지고 부여해줘야함 
                        SessionsId = "Error",
                        ConnectedAt = default,
                        UserId = "Error"
                    },
                    Jwt = "Error",
                    Result = new Result
                    {
                        Success = false,
                        Message = $"Failed to Log in. {ex.ToString()}",
                        Code = -1,
                    }
                };
            }
        }

        public override Task<Empty> Logout(LogoutRequest request, ServerCallContext context)
        {
            // 각자 해보삼
            return base.Logout(request, context);
        }

        public override Task<ValidateTokenResponse> ValidateToken(ValidateTokenRequest request, ServerCallContext context)
        {
            // 각자 해보삼
            return base.ValidateToken(request, context);
        }
    }
}
