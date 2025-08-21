using Game.Server.Network;
using Game.User;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace Game.Server.User
{
    class UserServiceImpl : UserService.UserServiceBase
    {
        public override async Task<RegisterResponse> Register(RegisterRequest request, ServerCallContext context)
        {
            try
            {
                var actionResult = await UserFacade.RegisterAsync(
                    PersistenceApiSettings.BASE_URL,
                    request.Username,
                    request.Password,
                    context.CancellationToken
                );

                return new RegisterResponse
                {
                    UserInfo = new UserInfo
                    {
                        Id = actionResult.user.id.ToString(),
                        Username = actionResult.user.username,
                        Nickname = actionResult.user.nickname ?? string.Empty,
                        CreatedAt = Timestamp.FromDateTime(actionResult.user.createdAt),
                        LastConnected = actionResult.user.lastConnected.HasValue ? Timestamp.FromDateTime(actionResult.user.lastConnected.Value) : new Timestamp(),
                    },
                    Result = new Result
                    {
                        Success = true,
                        Message = "User registered successfully.",
                        Code = (int)actionResult.statusCode
                    }
                };
            }
            catch (HttpRequestException httpRequestEx)
            {
                return new RegisterResponse
                {
                    UserInfo = new UserInfo
                    {
                        Id = string.Empty,
                        Username = "Error",
                        Nickname = "Error",
                        CreatedAt = default,
                        LastConnected = default
                    },
                    Result = new Result
                    {
                        Success = false,
                        Message = $"Failed to register user. {httpRequestEx.Message}.. ex: {httpRequestEx.ToString()}",
                        Code = httpRequestEx.StatusCode == null ? -1 : (int)httpRequestEx.StatusCode
                    }
                };
            }
            catch (Exception ex)
            {
                return new RegisterResponse
                {
                    UserInfo = new UserInfo
                    {
                        Id = string.Empty,
                        Username = "Error",
                        Nickname = "Error",
                        CreatedAt = default,
                        LastConnected = default
                    },
                    Result = new Result
                    {
                        Success = false,
                        Message = $"Failed to register user. {ex.Message}.",
                        Code = -1
                    }
                };
            }
        }

        public override async Task<UpdateNicknameResponse> UpdateNickname(UpdateNicknameRequest request, ServerCallContext context)
        {
            try
            {
                var actionResult = await UserFacade.UpdateNicknameAsync(
                    PersistenceApiSettings.BASE_URL,
                    request.Id,
                    request.NewNickname,
                    context.CancellationToken
                );

                return new UpdateNicknameResponse
                {
                    NewNickname = actionResult.response.newNickname,
                    Exists = actionResult.response.exists,
                    Result = new Result
                    {
                        Success = !actionResult.response.exists,
                        Message = actionResult.response.message,
                        Code = (int)actionResult.statusCode,
                    }
                };
            }
            catch (HttpRequestException httpRequestEx)
            {
                return new UpdateNicknameResponse
                {
                    NewNickname = string.Empty,
                    Exists = false,
                    Result = new Result
                    {
                        Success = false,
                        Message = $"Failed to update nickname {httpRequestEx.Message}",
                        Code = httpRequestEx.StatusCode == null ? -1 : (int)httpRequestEx.StatusCode,
                    }
                };
            }
            catch (Exception ex)
            {
                return new UpdateNicknameResponse
                {
                    NewNickname = string.Empty,
                    Exists = false,
                    Result = new Result
                    {
                        Success = false,
                        Message = $"Failed to update nickname {ex.Message}",
                        Code = -1,
                    }
                };
            }
        }
    }
}
