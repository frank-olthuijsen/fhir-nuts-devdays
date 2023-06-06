using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nuts.Plugin.Operations;
using System.Net.Mime;
using System.Text;
using System.Text.Json.Nodes;

namespace Nuts.Plugin
{
    public class NutsClient
    {
        private readonly ILogger<NutsClient> _logger;
        private readonly HttpClient _httpClient = new();

        public NutsClient(ILogger<NutsClient> logger,
            IOptions<NutsOptions> nutsOptions)
        {
            _logger = logger;
            _httpClient.BaseAddress = new Uri(nutsOptions.Value.NodeUrl); 
        }

        /// <summary>
        /// Creates a Nuts authorization credential
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="receiver"></param>
        /// <param name="taskId"></param>
        /// <returns></returns>
        /// <see href="https://nuts-foundation.gitbook.io/drafts/rfc/rfc014-authorization-credential"/>
        public async Task<string> CreateAuthorizationCredentialsAsync(string sender, string receiver, int taskId)
        {
            var searchJson = string.Format(ReferTemplates.CREATE_VC_TEMPLATE, sender, receiver, taskId);

            using HttpContent body = new StringContent(searchJson, Encoding.UTF8, MediaTypeNames.Application.Json);

            using HttpResponseMessage response =
                await _httpClient.PostAsync("/internal/vcr/v2/issuer/vc", body);

            response.EnsureSuccessStatusCode();

            string content = await response.Content.ReadAsStringAsync();
            var jsonContent = JsonNode.Parse(content);
            JsonNode? id = jsonContent["id"];
            return id.ToString();
        }

        public async Task<string?> GetDidByOrganizationNameAsync(string name)
        {
            var searchJson = string.Format(ReferTemplates.GET_DID_TEMPLATE, name);

            using HttpContent body = new StringContent(searchJson, Encoding.UTF8, MediaTypeNames.Application.Json);

            using HttpResponseMessage response =
                await _httpClient.PostAsync("/internal/vcr/v2/search", body);

            response.EnsureSuccessStatusCode();

            string content = await response.Content.ReadAsStringAsync();

            var jsonContent = JsonNode.Parse(content);
            JsonNode? vcsNode = jsonContent["verifiableCredentials"];

            var vcsArray = vcsNode.AsArray();
            foreach (var vc in vcsArray)
            {
                JsonNode? vcNode = vc["verifiableCredential"];
                JsonNode? subjectNode = vcNode["credentialSubject"];
                JsonNode? idNode = subjectNode["id"];

                // for simplicity sake we take the first match
                return idNode.ToString();
            }

            return null;
        }

        /// <summary>
        /// Retrieves the endpoint with the specified endpointType from the specified compound service
        /// </summary>
        /// <param name="did"></param>
        /// <param name="compoundService"></param>
        /// <param name="endpointType"></param>
        /// <returns></returns>
        public async Task<string?> GetEndpointAsync(string did, string compoundService, string endpointType)
        {
            if (string.IsNullOrEmpty(did) ||
                string.IsNullOrEmpty(compoundService) ||
                string.IsNullOrEmpty(endpointType))
            {
                return null;
            }

            string uri = $"/internal/didman/v1/did/{did}/compoundservice/{compoundService}/endpoint/{endpointType}";

            using HttpResponseMessage response = await _httpClient.GetAsync(uri);

            response.EnsureSuccessStatusCode();

            string content = await response.Content.ReadAsStringAsync();

            var jsonContent = JsonNode.Parse(content);
            JsonNode? node = jsonContent["endpoint"];

            return node?.ToString();
        }

        /// <summary>
        /// Creates a JWT Grant and uses it as authorization grant to get an access token from the authorizer
        /// </summary>
        /// <param name="authorizer"></param>
        /// <param name="requester"></param>
        /// <returns></returns>
        public async Task<string?> GetAccessTokenAsync(string authorizer, string requester)
        {
            var searchJson = string.Format(ReferTemplates.GET_AT_TEMPLATE, authorizer, requester);
            try
            {
                using HttpContent body = new StringContent(searchJson, Encoding.UTF8, MediaTypeNames.Application.Json);

                using HttpResponseMessage response =
                    await _httpClient.PostAsync("/internal/auth/v1/request-access-token", body);

                response.EnsureSuccessStatusCode();

                string content = await response.Content.ReadAsStringAsync();

                var jsonContent = JsonNode.Parse(content);
                JsonNode? node = jsonContent["access_token"];

                return node?.ToString();
            }
            catch (Exception e)
            {
               _logger.LogCritical($"Unable to obtain an access token. Node: {_httpClient.BaseAddress}. Body: {searchJson}. Exception: {e.Message}.");
               throw;
            }
        }

        /// <summary>
        /// Creates a JWT Grant and uses it as authorization grant to get an access token from the authorizer
        /// </summary>
        /// <param name="authorizer"></param>
        /// <param name="requester"></param>
        /// <param name="authorization"></param>
        /// <returns></returns>
        public async Task<string?> GetAccessTokenAsync(string authorizer, string requester, string authorization)
        {
            var searchJson = string.Format(ReferTemplates.GET_AT_EX_TEMPLATE, authorizer, requester, authorization);

            using HttpContent body = new StringContent(searchJson, Encoding.UTF8, MediaTypeNames.Application.Json);

            using HttpResponseMessage response =
                await _httpClient.PostAsync("/internal/auth/v1/request-access-token", body);

            response.EnsureSuccessStatusCode();

            string content = await response.Content.ReadAsStringAsync();

            var jsonContent = JsonNode.Parse(content);
            JsonNode? node = jsonContent["access_token"];

            return node?.ToString();
        }

        /// <summary>
        /// Returns the resolved verifiable credential, regardless of its revocation/trust state
        /// </summary>
        /// <param name="vcDid"></param>
        /// <returns></returns>
        /// <see href="https://nuts-foundation.gitbook.io/v1/rfc/rfc011-verifiable-credential"/>
        public async Task<string?> GetVerifiableCredentialAsync(string vcDid)
        {
            vcDid = vcDid.Replace("#", "%23");

            // for the PoC a retry mechanism has been created since it takes 1-2 seconds for the nodes to synchronize
            int retry = 0;
            int maxAttempts = 6;
            while (retry < maxAttempts)
            {
                try
                {
                    HttpResponseMessage response = await _httpClient.GetAsync($"/internal/vcr/v2/vc/{vcDid}");
                    response.EnsureSuccessStatusCode();
                    return await response.Content.ReadAsStringAsync();
                }
                catch (Exception ex)
                {
                    if (retry < maxAttempts - 1)
                    {
                        _logger.LogWarning(
                            $"Unable to retrieve verifiable credential at attempt {retry}: {ex.Message}");
                        Thread.Sleep(500);
                        retry++;
                    }
                    else
                    {
                        throw;
                    }
                }
            }
            return null;
        }
    }
}
