using System.Net;
using System.Net.Http.Json;

namespace Game.Server.Auth
{
    public static class AuthFacade
    {
        private static readonly HttpClient _http = new HttpClient();
        
        // record 한정자
        // 데이터 캡슐 내장 기능을 제공하는 참조형식 정의를 위한 키워드
        public record LoginRequest(string id, string pw);
        public record LoginResponse(string jwt, string sessionId, DateTime createdAt);
        public record LogoutRequest(string sessionId);
        public record ValidateRequest(string jwt);
        public record ValidateResponse(bool isValid, string sessionId);

        public static async Task<(HttpStatusCode statusCode, string jwt, string sessionId, DateTime createdAt)> LoginAsync(string baseUrl,
                                                    string id,
                                                    string pw,
                                                    CancellationToken cancellationToken = default)
        {
            var response = await _http.PostAsJsonAsync($"{baseUrl}/auth/login",
                                                       new LoginRequest(id, pw),
                                                       cancellationToken);

            response.EnsureSuccessStatusCode(); // status 가 400번대, 500번대처럼 에러관련 코드면 예외던짐. 
            var body = await response.Content.ReadFromJsonAsync<LoginResponse>(cancellationToken);
            return (response.StatusCode, body.jwt, body.sessionId, body.createdAt);
        }

        public static async Task LogoutAsync(string baseUrl, string sessionId, CancellationToken cancellationToken = default)
        {
            var response = await _http.PostAsJsonAsync($"{baseUrl}/auth/logout",
                                                       new LogoutRequest(sessionId),
                                                       cancellationToken);

            response.EnsureSuccessStatusCode();
            return;
        }

        public static async Task<(bool isValid, string sessionId)> ValidateAsync(string baseUrl, string jwt, CancellationToken cancellationToken = default)
        {
            var response = await _http.PostAsJsonAsync($"{baseUrl}/auth/validate",
                                                       new ValidateRequest(jwt),
                                                       cancellationToken);

            response.EnsureSuccessStatusCode();
            var body = await response.Content.ReadFromJsonAsync<ValidateResponse>(cancellationToken);
            return (body.isValid, body.sessionId);
        }
    }
}
