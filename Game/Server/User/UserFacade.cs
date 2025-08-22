using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Diagnostics;

static class UserFacade
{
    private static readonly HttpClient _http = CreateDevHttpClient();

    private static HttpClient CreateDevHttpClient()
    {
        var handler = new HttpClientHandler
        {
            // 개발용: 자체서명/이름불일치 무시
            ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        };

        return new HttpClient(handler)
        {
            // ★ persistence는 HTTP/2 핸드셰이크가 불안 → 1.1로 고정
            DefaultRequestVersion = HttpVersion.Version11,
            DefaultVersionPolicy = HttpVersionPolicy.RequestVersionExact,
            Timeout = TimeSpan.FromSeconds(15)
        };
    }

    // DTO
    public record CreateUserRequest(string username, string password);
    public record CreateUserResponse(Guid id, string username, string? nickname, DateTime createdAt, DateTime? lastConnected);
    public record UpdateNicknameRequest(string nickname);
    public record UpdateNicknameResponse(string newNickname, bool exists, string message);

    public static async Task<(HttpStatusCode statusCode, CreateUserResponse user)> RegisterAsync(
        string baseUrl, string username, string password, CancellationToken cancellationToken = default)
    {
        var endpoint = $"{baseUrl.TrimEnd('/')}/users/create";
        Debug.WriteLine($"[UserFacade] POST {endpoint} username={username}");

        var req = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Version = HttpVersion.Version11,                       // 안전빵
            VersionPolicy = HttpVersionPolicy.RequestVersionExact,
            Content = JsonContent.Create(new CreateUserRequest(username, password))
        };

        var res = await _http.SendAsync(req, cancellationToken);
        var text = await res.Content.ReadAsStringAsync(cancellationToken);

        if (!res.IsSuccessStatusCode)
            throw new HttpRequestException($"Persistence returned {(int)res.StatusCode} {res.StatusCode}. Body: {text}");

        var body = await res.Content.ReadFromJsonAsync<CreateUserResponse>(cancellationToken)
                   ?? throw new InvalidOperationException($"Empty JSON. Raw: {text}");

        return (res.StatusCode, body);
    }

    public static async Task<(HttpStatusCode statusCode, UpdateNicknameResponse response)> UpdateNicknameAsync(
        string baseUrl, string id, string newNickname, CancellationToken cancellationToken = default)
    {
        var endpoint = $"{baseUrl.TrimEnd('/')}/users/{id}/nickname";
        Debug.WriteLine($"[UserFacade] POST {endpoint} id={id} newNickname={newNickname}");

        var req = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Version = HttpVersion.Version11,
            VersionPolicy = HttpVersionPolicy.RequestVersionExact,
            Content = JsonContent.Create(new UpdateNicknameRequest(newNickname))
        };

        var res = await _http.SendAsync(req, cancellationToken);
        var text = await res.Content.ReadAsStringAsync(cancellationToken);

        if (!res.IsSuccessStatusCode)
            throw new HttpRequestException($"Persistence returned {(int)res.StatusCode} {res.StatusCode}. Body: {text}");

        var body = await res.Content.ReadFromJsonAsync<UpdateNicknameResponse>(cancellationToken)
                   ?? throw new InvalidOperationException($"Empty JSON. Raw: {text}");

        return (res.StatusCode, body);
    }
}
