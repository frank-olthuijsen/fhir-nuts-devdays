﻿using Microsoft.Extensions.Logging;
using Nuts.Plugin.Operations;
using System.Net.Mime;
using System.Text;
using System.Text.Json.Nodes;

namespace Nuts.Plugin
{
    public class NutsClient
    {
        private readonly ILogger<NutsClient> _logger;
        private readonly HttpClient HttpClient = new();

        public NutsClient(ILogger<NutsClient> logger)
        {
            _logger = logger;
            HttpClient.BaseAddress = new Uri("http://localhost:1323"); // TODO: make configurable
        }

        public async Task<string> CreateAuthorizationCredentialsAsync(string sender, string receiver, int taskId)
        {
            var searchJson = string.Format(ReferTemplates.CREATE_VC_TEMPLATE, sender, receiver, taskId);

            using HttpContent body = new StringContent(searchJson, Encoding.UTF8, MediaTypeNames.Application.Json);

            using HttpResponseMessage response =
                await HttpClient.PostAsync("/internal/vcr/v2/issuer/vc", body);

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
                await HttpClient.PostAsync("/internal/vcr/v2/search", body);

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
                return idNode.ToString();
            }

            return null;
        }

        public async Task<string?> GetEndpoint(string did, string compoundService, string endpointType)
        {
            if (string.IsNullOrEmpty(did) ||
                string.IsNullOrEmpty(compoundService) ||
                string.IsNullOrEmpty(endpointType))
            {
                return null;
            }

            string uri = $"/internal/didman/v1/did/{did}/compoundservice/{compoundService}/endpoint/{endpointType}";

            using HttpResponseMessage response = await HttpClient.GetAsync(uri);

            response.EnsureSuccessStatusCode();

            string content = await response.Content.ReadAsStringAsync();

            var jsonContent = JsonNode.Parse(content);
            JsonNode? node = jsonContent["endpoint"];

            return node?.ToString();
        }

        public async Task<string?> GetAccessTokenAsync(string authorizer, string requester)
        {
            var searchJson = string.Format(ReferTemplates.GET_AT_TEMPLATE, authorizer, requester);

            using HttpContent body = new StringContent(searchJson, Encoding.UTF8, MediaTypeNames.Application.Json);

            using HttpResponseMessage response =
                await HttpClient.PostAsync("/internal/auth/v1/request-access-token", body);

            response.EnsureSuccessStatusCode();

            string content = await response.Content.ReadAsStringAsync();

            var jsonContent = JsonNode.Parse(content);
            JsonNode? node = jsonContent["access_token"];

            return node?.ToString();
        }

        public async Task<string?> GetAccessToken(string authorizer, string requester, string authorization)
        {
            var searchJson = string.Format(ReferTemplates.GET_AT_EX_TEMPLATE, authorizer, requester, authorization);

            using HttpContent body = new StringContent(searchJson, Encoding.UTF8, MediaTypeNames.Application.Json);

            using HttpResponseMessage response =
                await HttpClient.PostAsync("/internal/auth/v1/request-access-token", body);

            response.EnsureSuccessStatusCode();

            string content = await response.Content.ReadAsStringAsync();

            var jsonContent = JsonNode.Parse(content);
            JsonNode? node = jsonContent["access_token"];

            return node?.ToString();
        }

        public async Task<string?> GetVerifiableCredential(string vcDid)
        {
            vcDid = vcDid.Replace("#", "%23");

            int retry = 0;
            int maxAttempts = 6;
            while (retry < maxAttempts)
            {
                try
                {
                    HttpResponseMessage response = await HttpClient.GetAsync($"/internal/vcr/v2/vc/{vcDid}");
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
