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
            // 이미 토큰 있으면 요청안함
            if (!string.IsNullOrEmpty(_accessToken))
            {
                return _accessToken;
            }

            var tokenUrl = $"https://services.api.unity.com/auth/v1/token-exchange?projectId={PROJECT_ID}";

            if (!string.IsNullOrEmpty(ENVIRONMENT_ID))
            {
                tokenUrl += $"&environmentId={ENVIRONMENT_ID}";
            }

            // Service account credential
            var BasicAuthCreds = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{SERVICE_ACCOUNT_KEY_ID}:{SERVICE_ACCOUNT_SECRET_KEY}"));
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", BasicAuthCreds);

            var requestBody = new TokenExchangeRequest
            (
                scopes: new[]
                {
                    "multiplay.allocations.create",
                    "multiplay.allocations.get",
                    "multiplay.allocations.delete",
                }
            );

            var response = await _httpClient.PostAsJsonAsync(tokenUrl, requestBody);

            response.EnsureSuccessStatusCode();
            var tokenResponse = await response.Content.ReadFromJsonAsync<TokenExchangeResponse>();
            _accessToken = tokenResponse.accessToken;
            _httpClient.DefaultRequestHeaders.Authorization = null; // 토큰 얻고나면 Basic 요청 할일 없음
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
            var reqeust = await CreateAuthenticatedRequest(method, endPoint);
            string jsonString = JsonConvert.SerializeObject(body);
            reqeust.Content = new StringContent(jsonString, Encoding.UTF8, "application/json");

            return reqeust;
        }

        public static async Task<List<ServerAllocation>> GetAllocationsAsync(string age = null, int? limit = null, int? offset = null, IEnumerable<string>? ids = null)
        {
            var queryParams = new List<string>();

            if (age != null)
            {
                queryParams.Add($"age={age}");
            }

            if (limit != null)
            {
                queryParams.Add($"limit={limit}" );
            }

            if (offset != null)
            {
                queryParams.Add($"offset={offset}");
            }

            if (ids != null)
            {
                queryParams.Add($"ids={string.Join(',', ids)}");
            }

            var queryString = queryParams.Count > 0 ? "?" + string.Join('&', queryParams) : "";
            var endPoint = $"{BASE_URL}v1/allocations/projects/{PROJECT_ID}/environments/{ENVIRONMENT_ID}/fleets/{FLEET_ID}/allocations{queryString}";
            var request = await CreateAuthenticatedRequest(HttpMethod.Get, endPoint);
            var response = await _httpClient.SendAsync(request);

            response.EnsureSuccessStatusCode();

            var responseDto = await response.Content.ReadFromJsonAsync<GetAllocationsResponse>();
            var serverAllocations = new List<ServerAllocation>();

            foreach (var allocation in responseDto.allocations)
            {
                serverAllocations.Add(new ServerAllocation
                (
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
            var request = await CreateAuthenticatedRequest(HttpMethod.Get, endPoint);
            var response = await _httpClient.SendAsync(request);

            response.EnsureSuccessStatusCode();

            var responseDto = await response.Content.ReadFromJsonAsync<AllocationResponse>();

            return new ServerAllocation
                (
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

            var request = await CreateAuthenticatedRequest(HttpMethod.Post, endPoint, new CreateRequest
                (
                    allocationId: allocationId ?? Guid.NewGuid().ToString(),
                    buildConfigurationId: buildConfigurationId,
                    payload: payloadJson,
                    regionId: regionId,
                    restart: restart
                ));

            var response = await _httpClient.SendAsync(request);

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
            var request = await CreateAuthenticatedRequest(HttpMethod.Delete, endPoint, new DeleteRequest
                (
                    allocationId: allocationId
                ));

            var response = await _httpClient.SendAsync(request);

            response.EnsureSuccessStatusCode();
            return;
        }
    }
}
