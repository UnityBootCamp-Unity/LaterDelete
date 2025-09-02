using Newtonsoft.Json;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using static Game.Server.MultiGameplay.UnityMultiplayerGameServerHostingConfiguration;

namespace Game.Server.MultiGameplay
{
    static class UnityMultiplayerGameServerHostingFacade
    {
        private static readonly HttpClient _httpClient = new HttpClient();
        private static string _accessToken;
        private static DateTime _tokenExpiryTime = DateTime.MinValue;

        public record class TokenExchangeRequest(string[] scopes);
        public record class TokenExchangeResponse(string accessToken);
        public record class GetAllocationsResponse(List<AllocationResponse> allocations, Pagination pagination);
        public record class AllocationResponse
        (
            string allocationId,
            long buildConfigurationId,
            DateTime created,
            string fleetId,
            DateTime fulfilled,
            ulong gamePort,
            string ipv4,
            string ipv6,
            long machineId,
            bool readiness,
            DateTime ready,
            string regionId,
            string requestId,
            DateTime requested,
            long serverId
        );

        public record class ServerAllocation
        (
            string AllocationId,
            long ServerId,
            string IpAddress,
            ulong Port,
            string Region,
            string FleetId,
            bool IsReady,
            long MachineId,
            long BuildConfigurationId
        );

        public record class CreateRequest(string allocationId, long buildConfigurationId, string payload, string regionId, bool restart);
        public record class CreateResponse(string allocationId, string href);
        public record class AllocationPayload(int lobbyId, List<int> clientIds, Dictionary<string, string> gameSettings);
        public record class DeleteRequest(string allocationId);
        public record class Pagination(int limit, int offset);

        private static async Task<string> GetAccessTokenAsync()
        {
            // 토큰이 없거나 만료 5분 전이면 새로 발급
            if (string.IsNullOrEmpty(_accessToken) || DateTime.UtcNow >= _tokenExpiryTime.AddMinutes(-5))
            {
                Console.WriteLine($"[Unity Auth] Token refresh needed. Current: {DateTime.UtcNow}, Expiry: {_tokenExpiryTime}");

                var tokenUrl = $"https://services.api.unity.com/auth/v1/token-exchange?projectId={PROJECT_ID}";
                if (!string.IsNullOrEmpty(ENVIRONMENT_ID))
                {
                    tokenUrl += $"&environmentId={ENVIRONMENT_ID}";
                }

                // 핵심 수정: DefaultRequestHeaders 완전 초기화
                _httpClient.DefaultRequestHeaders.Clear();

                // Basic Auth 설정
                var BasicAuthCreds = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{SERVICE_ACCOUNT_KEY_ID}:{SERVICE_ACCOUNT_SECRET_KEY}"));
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", BasicAuthCreds);

                var requestBody = new TokenExchangeRequest(new[]
                {
                    "multiplay.allocations.create",
                    "multiplay.allocations.get",
                    "multiplay.allocations.delete",
                });

                try
                {
                    var response = await _httpClient.PostAsJsonAsync(tokenUrl, requestBody);
                    response.EnsureSuccessStatusCode();

                    var tokenResponse = await response.Content.ReadFromJsonAsync<TokenExchangeResponse>();
                    Console.WriteLine("[Unity Auth] Token JSON: " + tokenResponse);
                    _accessToken = tokenResponse.accessToken;

                    //  토큰 만료시간을 50분으로 설정
                    _tokenExpiryTime = DateTime.UtcNow.AddMinutes(50);

                    Console.WriteLine($"[Unity Auth] Token refreshed. New expiry: {_tokenExpiryTime}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Unity Auth] Token refresh failed: {ex.Message}");
                    _accessToken = null;
                    _tokenExpiryTime = DateTime.MinValue;
                    throw;
                }
                finally
                {
                    // Basic Auth 헤더 완전 제거
                    _httpClient.DefaultRequestHeaders.Authorization = null;
                }
            }

            return _accessToken;
        }

        /// <summary>
        /// Bearer 요청 생성
        /// </summary>
        private static async Task<HttpRequestMessage> CreateAuthenticatedRequest(HttpMethod method, string endPoint)
        {
            var token = await GetAccessTokenAsync();
            var request = new HttpRequestMessage(method, endPoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return request;
        }

        private static async Task<HttpRequestMessage> CreateAuthenticatedRequest<T>(HttpMethod method, string endPoint, T body)
        {
            var request = await CreateAuthenticatedRequest(method, endPoint);
            string jsonString = JsonConvert.SerializeObject(body);
            request.Content = new StringContent(jsonString, Encoding.UTF8, "application/json");
            return request;
        }

        public static async Task<List<ServerAllocation>> GetAllocationsAsync(string age = null, int? limit = null, int? offset = null, IEnumerable<string>? ids = null)
        {
            var queryParams = new List<string>();

            if (age != null) queryParams.Add($"age={age}");
            if (limit != null) queryParams.Add($"limit={limit}");
            if (offset != null) queryParams.Add($"offset={offset}");
            if (ids != null) queryParams.Add($"ids={string.Join(',', ids)}");

            var queryString = queryParams.Count > 0 ? "?" + string.Join('&', queryParams) : "";
            var endPoint = $"{BASE_URL}v1/allocations/projects/{PROJECT_ID}/environments/{ENVIRONMENT_ID}/fleets/{FLEET_ID}/allocations{queryString}";

            // 첫 번째 시도
            var request = await CreateAuthenticatedRequest(HttpMethod.Get, endPoint);
            var response = await _httpClient.SendAsync(request);

            // 401 에러 발생 시 토큰 재발급 후 재시도
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                Console.WriteLine("[Unity Auth] 401 Unauthorized - forcing token refresh");

                // 토큰 강제 만료
                _accessToken = null;
                _tokenExpiryTime = DateTime.MinValue;

                // 잠시 대기 후 재시도
                await Task.Delay(1000);

                request = await CreateAuthenticatedRequest(HttpMethod.Get, endPoint);
                response = await _httpClient.SendAsync(request);
            }

            response.EnsureSuccessStatusCode();

            var responseDto = await response.Content.ReadFromJsonAsync<GetAllocationsResponse>();
            var serverAllocations = new List<ServerAllocation>();

            foreach (var allocation in responseDto.allocations)
            {
                serverAllocations.Add(new ServerAllocation(
                    AllocationId: allocation.allocationId,
                    ServerId: allocation.serverId,
                    IpAddress: allocation.ipv4,
                    Port: allocation.gamePort,
                    Region: allocation.regionId,
                    FleetId: allocation.fleetId,
                    IsReady: allocation.ready < DateTime.UtcNow,
                    MachineId: allocation.machineId,
                    BuildConfigurationId: allocation.buildConfigurationId
                ));
            }

            return serverAllocations;
        }

        public static async Task<ServerAllocation> GetAllocationAsync(string allocationId)
        {
            var endPoint = $"{BASE_URL}v1/allocations/projects/{PROJECT_ID}/environments/{ENVIRONMENT_ID}/fleets/{FLEET_ID}/allocations/{allocationId}";

            // 첫 번째 시도
            var request = await CreateAuthenticatedRequest(HttpMethod.Get, endPoint);
            var response = await _httpClient.SendAsync(request);

            // 401 에러 발생 시 토큰 재발급 후 재시도
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                Console.WriteLine("[Unity Auth] 401 Unauthorized in GetAllocation - forcing token refresh");

                _accessToken = null;
                _tokenExpiryTime = DateTime.MinValue;
                await Task.Delay(1000);

                request = await CreateAuthenticatedRequest(HttpMethod.Get, endPoint);
                response = await _httpClient.SendAsync(request);
            }

            response.EnsureSuccessStatusCode();

            var responseDto = await response.Content.ReadFromJsonAsync<AllocationResponse>();

            return new ServerAllocation(
                AllocationId: responseDto.allocationId,
                ServerId: responseDto.serverId,
                IpAddress: responseDto.ipv4,
                Port: responseDto.gamePort,
                Region: responseDto.regionId,
                FleetId: responseDto.fleetId,
                IsReady: responseDto.ready < DateTime.UtcNow,
                MachineId: responseDto.machineId,
                BuildConfigurationId: responseDto.buildConfigurationId
            );
        }

        public static async Task<(string allocationId, string href)> CreateAllocationAsync(string allocationId,
                                                long buildConfigurationId,
                                                string regionId,
                                                bool restart,
                                                AllocationPayload payload)
        {
            var endPoint = $"{BASE_URL}v1/allocations/projects/{PROJECT_ID}/environments/{ENVIRONMENT_ID}/fleets/{FLEET_ID}/allocations";
            string payloadJson = JsonConvert.SerializeObject(payload);

            var requestData = new CreateRequest(
                allocationId: allocationId ?? Guid.NewGuid().ToString(),
                buildConfigurationId: buildConfigurationId,
                payload: payloadJson,
                regionId: regionId,
                restart: restart
            );

            // 첫 번째 시도
            var request = await CreateAuthenticatedRequest(HttpMethod.Post, endPoint, requestData);
            var response = await _httpClient.SendAsync(request);

            // 401 에러 발생 시 재시도
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                Console.WriteLine("[Unity Auth] 401 Unauthorized in CreateAllocation - forcing token refresh");

                _accessToken = null;
                _tokenExpiryTime = DateTime.MinValue;
                await Task.Delay(1000);

                request = await CreateAuthenticatedRequest(HttpMethod.Post, endPoint, requestData);
                response = await _httpClient.SendAsync(request);
            }

            response.EnsureSuccessStatusCode();

            var responseDto = await response.Content.ReadFromJsonAsync<CreateResponse>();
            return (responseDto.allocationId, responseDto.href);
        }

        /// <summary>
        /// Deallocation
        /// </summary>
        public static async Task DeleteAllocationAsync(string allocationId)
        {
            var endPoint = $"{BASE_URL}v1/allocations/projects/{PROJECT_ID}/environments/{ENVIRONMENT_ID}/fleets/{FLEET_ID}/allocations/{allocationId}";
            var requestData = new DeleteRequest(allocationId: allocationId);

            // 첫 번째 시도
            var request = await CreateAuthenticatedRequest(HttpMethod.Delete, endPoint, requestData);
            var response = await _httpClient.SendAsync(request);

            // 401 에러 발생 시 재시도
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                Console.WriteLine("[Unity Auth] 401 Unauthorized in DeleteAllocation - forcing token refresh");

                _accessToken = null;
                _tokenExpiryTime = DateTime.MinValue;
                await Task.Delay(1000);

                request = await CreateAuthenticatedRequest(HttpMethod.Delete, endPoint, requestData);
                response = await _httpClient.SendAsync(request);
            }

            response.EnsureSuccessStatusCode();
        }
    }
}