using Hl7.Fhir.ElementModel;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;
using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using Vonk.Core.Context;
using Task = System.Threading.Tasks.Task;
using Microsoft.Extensions.Options;

namespace Nuts.Plugin.Handlers
{
    internal class NotificationTaskHandler
    {
        private readonly ILogger<NotificationTaskHandler> _logger;

        private readonly NutsClient _nutsClient;
        private readonly IOptions<NutsOptions> _nutsOptions;

        public NotificationTaskHandler(ILogger<NotificationTaskHandler> logger, 
            NutsClient nutsClient, 
            IOptions<NutsOptions> nutsOptions)
        {
            _logger = logger;
            
            _nutsClient = nutsClient;
            _nutsOptions = nutsOptions;
        }

        public async Task Handle(IVonkContext vonkContext)
        {
            _logger.LogInformation("Notification task received");

            try
            {
                // TODO: try to get this to work, see comment in GetToken()
                //string? token = GetToken(vonkContext);
                //if (string.IsNullOrEmpty(token))
                //{
                //    throw new Exception("No token provided");
                //}

                // TODO: enable this, tied to GetToken()
                //bool valid = await IntrospectToken(token);
                //if (!valid)
                //{
                //    throw new Exception("Token introspection failed");
                //    TODO: if this fails, return status code + OO
                //}
                
                // get task
                Hl7.Fhir.Model.Task notificationTask = vonkContext.Request.Payload.Resource.ToPoco<Hl7.Fhir.Model.Task>();
                if (notificationTask == null)
                {
                    throw new Exception("No notification task provided");
                }
                _logger.LogInformation($"Received notification task with id: {notificationTask.Id}");

                // obtain the did of the organization that is sending the referral
                string senderDid = notificationTask.Requester.OnBehalfOf.Identifier.Value;
                if (string.IsNullOrEmpty(senderDid))
                {
                    throw new Exception("No organization DID found in notification task");
                }
                _logger.LogInformation($"Organization DID found in notification task: {notificationTask.Id}");

                // obtain fhir endpoint
                string? fhirEndpoint = await _nutsClient.GetEndpoint(senderDid, "bgz-sender", "fhir");
                if (string.IsNullOrEmpty(fhirEndpoint))
                {
                    throw new Exception("Unable to obtain fhir endpoint");
                }
                _logger.LogInformation($"Organization FHIR endpoint retrieved: {fhirEndpoint}");

                var basedOn = notificationTask.BasedOn.SingleOrDefault();
                if (basedOn == null)
                {
                    throw new Exception("More than one basedOn specified");
                }
                _logger.LogInformation($"Workflow task found in notification task: {basedOn}");

                FhirString? vcDid = GetVerifiableCredentialDid(notificationTask);
                if (vcDid == null)
                {
                    throw new Exception("Verifiable credential did not found in notification task");
                }
                _logger.LogInformation($"Verifiable credential did found: {vcDid}");

                // obtain verifiable credential
                string? vc = await _nutsClient.GetVerifiableCredential(vcDid.Value);
                if (vc == null)
                {
                    throw new Exception("Unable to retrieve verifiable credential");
                }
                _logger.LogInformation($"Verifiable credential retrieved: {vc}");

                // obtain the receiver DID (us) based on its name
                string? receiverDid = await _nutsClient.GetDidByOrganizationNameAsync(_nutsOptions.Value.OrganizationName);
                if (receiverDid == null)
                {
                    throw new Exception("Unable to retrieve receiver DID");
                }
                _logger.LogInformation($"Retrieved DID {receiverDid} for {_nutsOptions.Value.OrganizationName}");

                // obtain access token
                string? accessToken = await _nutsClient.GetAccessToken(senderDid, receiverDid, vc);
                if (string.IsNullOrEmpty(accessToken))
                {
                    throw new Exception("Unable to obtain an access token");
                }
                _logger.LogInformation($"Access token obtained: {accessToken}");

                // obtain the workflow task
                Hl7.Fhir.Model.Task? workflowTask = await GetWorkflowTask(fhirEndpoint, accessToken, basedOn.Reference);
                if(workflowTask == null) 
                {
                    throw new Exception("Unable to retrieve workflow task");
                }
                _logger.LogInformation($"Retrieved workflow task with id: {workflowTask.Id}");

                var fhirClient = new FhirClient(fhirEndpoint);
                fhirClient.RequestHeaders.Add("Authorization", "Bearer " + accessToken);

                var transactionBundle = new Bundle{Type = Bundle.BundleType.Transaction};

                // retrieve other resources
                foreach (var input in workflowTask.Input)
                {
                    if (input.Value is not FhirString value)
                    {
                        continue;
                    }
                    
                    string search = Uri.UnescapeDataString(value.Value);
                     
                    Bundle? resources = await PerformSearch(fhirClient, search);

                    if (resources != null)
                    {
                        foreach (var entry in resources.Entry)
                        {
                            entry.Request = new Bundle.RequestComponent
                            {
                                Method = Bundle.HTTPVerb.PUT,
                                Url = entry.Resource.TypeName
                            };
                            
                            // remove duplicates
                            Bundle.EntryComponent? existing = transactionBundle.Entry.SingleOrDefault(e =>
                                e.Resource.TypeName == entry.Resource.TypeName
                                && e.Resource.Id == entry.Resource.Id);
                            if (existing == null)
                            {
                                transactionBundle.Entry.Add(entry);
                                _logger.LogInformation($"Retrieved resource of type {entry.Resource.TypeName} with id: {entry.Resource.Id}");
                            }
                        }
                    }
                    
                }
                
                //File.WriteAllText(@"C:\tmp\Nuts\transaction.json", transactionBundle.ToJson());

                var selfFhirClient = new FhirClient("http://localhost:4080");
                await selfFhirClient.TransactionAsync(transactionBundle);
                // TODO: look at https://firely.atlassian.net/browse/SD-798 for a more elegant implementation
                _logger.LogInformation($"Successfully stored resources in Firely Server.");
            }
            catch (Exception ex)
            {
                _logger.LogError($"An error occurred during the handling of the notification task: {ex.Message}");
            }

            _ = await Task.FromResult(true);
        }

        private async Task<Bundle?> PerformSearch(FhirClient fhirClient, string search)
        {
            List<Resource> resources = new List<Resource>();

            char[] delimiterChars = {'?', '&'};
            string[] arguments = search.Split(delimiterChars);

            if (arguments.Length == 0)
            {
                return null;
            }

            var searchCriteria = new List<string>();
            for (int i = 1; i < arguments.Length; i++)
            {
                searchCriteria.Add(arguments[i]);
            }

            try
            {
                return await fhirClient.SearchAsync(arguments[0], searchCriteria.ToArray());
            }
            catch (Exception ex)
            {
                _logger.LogError($"Unable to retrieve resource from source: {ex.Message}");
                return null;
            }
        }

        private FhirString? GetVerifiableCredentialDid(Hl7.Fhir.Model.Task notificationTask)
        {
            foreach (var input in notificationTask.Input)
            {
                Coding? coding = input.Type.Coding.SingleOrDefault(c => c.Code == "authorization_base");
                if (coding != null)
                {
                    return input.Value as FhirString;
                }
            }
            return null;
        }

        private async Task<bool> IntrospectToken(object token)
        {
            // TODO: implement
            return _ = await Task.FromResult(true);
        }

        private string? GetToken(IVonkContext vonkContext)
        {
            // Opmerking van Wouter:
            // Een TCP forward op ngrok voor de OAuth server endpoint, http endpoint registreren in document ipv https

            // get access token from request
            //var claimsValue = vonkContext.HttpContext().User?.Claims?.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
            //var userId = claimsValue ?? "unknown";
            //var username = vonkContext.HttpContext().User?.Identity?.Name;

            string bearer = vonkContext.HttpContext().Request.Headers[HeaderNames.Authorization].ToString()
                .Replace("Bearer ", "");

            return bearer;
        }

        private async Task<Hl7.Fhir.Model.Task?> GetWorkflowTask(string fhirEndpoint, string accessToken, string resourceReference)
        {
            var fhirClient = new FhirClient(fhirEndpoint);
            fhirClient.RequestHeaders.Add("Authorization", "Bearer " + accessToken);
            return await fhirClient.ReadAsync<Hl7.Fhir.Model.Task>(resourceReference);
        }
    }
}
