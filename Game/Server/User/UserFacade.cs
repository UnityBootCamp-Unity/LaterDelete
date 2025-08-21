using System.Net;
using System.Net.Http.Json;

namespace Game.Server.User
{
    static class UserFacade
    {
        private static readonly HttpClient _http = new HttpClient();

        // DTO
        public record CreateUserRequest(string username, string password);

        /// <summary>
        /// 유저 생성 응답
        /// Server 는 DB 랑 직접 연동된것이 아니지만, DB에서 필수로 사용하는게 아니라고 명시하려고 ? nullable 을 붙여놨음. 안써도됨
        /// </summary>
        public record CreateUserResponse(Guid id, string username, string? nickname, DateTime createdAt, DateTime? lastConnected);
        public record UpdateNicknameRequest(string nickname);
        public record UpdateNicknameResponse(string newNickname, bool exists, string message);


        public static async Task<(HttpStatusCode statusCode, CreateUserResponse user)> RegisterAsync(string baseUrl, 
                                                                                                     string username,
                                                                                                     string password, 
                                                                                                     CancellationToken cancellationToken = default)
        {
            var response = await _http.PostAsJsonAsync($"{baseUrl}/users/create",
                                                       new CreateUserRequest(username, password),
                                                       cancellationToken);

            response.EnsureSuccessStatusCode();

            var body = await response.Content.ReadFromJsonAsync<CreateUserResponse>(cancellationToken);
            return (response.StatusCode, body);
        }

        public static async Task<(HttpStatusCode statusCode, UpdateNicknameResponse response)> UpdateNicknameAsync(string baseUrl,
                                                                                                                   string id,
                                                                                                                   string newNickname,
                                                                                                                   CancellationToken cancellationToken = default)
        {
            var response = await _http.PostAsJsonAsync($"{baseUrl}/users/{id}/nickname",
                                                       new UpdateNicknameRequest(newNickname),
                                                       cancellationToken);

            response.EnsureSuccessStatusCode();

            var body = await response.Content.ReadFromJsonAsync<UpdateNicknameResponse>(cancellationToken);
            return (response.StatusCode, body);
        }
    }
}
