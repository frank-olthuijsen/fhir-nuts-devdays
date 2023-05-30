using Hl7.Fhir.Model;
using Microsoft.Extensions.Logging;
using Vonk.Core.Context;
using Hl7.Fhir.Rest;
using Task = System.Threading.Tasks.Task;
using Microsoft.Extensions.Options;

namespace Nuts.Plugin.Operations
{
    internal class ReferOperation
    {
        private readonly ILogger<ReferOperation> _logger;

        private readonly NutsClient _nutsClient;
        private readonly IOptions<NutsOptions> _nutsOptions;

        public ReferOperation(ILogger<ReferOperation> logger, 
            NutsClient nutsClient,
            IOptions<NutsOptions> nutsOptions)
        {
            _logger = logger;
            _nutsClient = nutsClient;
            _nutsOptions = nutsOptions;
        }

        public async Task Execute(IVonkContext vonkContext)
        {
            _logger.LogInformation("Refer operation triggered");

            try
            {
                // the name of the organization that is receiving the referral should be supplied as an HTTP header
                string receiverName = vonkContext.HttpContext().Request.Headers["Receiver"];

                // obtain the sender DID based on its name
                string? senderDid = await _nutsClient.GetDidByOrganizationNameAsync(_nutsOptions.Value.OrganizationName);
                if (senderDid == null)
                {
                    throw new Exception("Unable to retrieve sender DID");
                }
                _logger.LogInformation($"Retrieved DID {senderDid} for {_nutsOptions.Value.OrganizationName}");

                // obtain the receiver DID based on its name
                string? receiverDid = await _nutsClient.GetDidByOrganizationNameAsync(receiverName);
                if (receiverDid == null)
                {
                    throw new Exception("Unable to retrieve receiver DID");
                }
                _logger.LogInformation($"Retrieved DID {receiverDid} for {receiverName}");

                // create workflow task
                // this step is skipped and instead we use a preset workflow task
                //CreateWorkflowTask(); 

                // create an authz credential
                string vcDid = await _nutsClient.CreateAuthorizationCredentialsAsync(senderDid, receiverDid, 1);
                _logger.LogInformation($"Created authorization credential: {vcDid}");

                // get notification endpoint of the receiver
                string? notificationEndpoint = await _nutsClient.GetEndpointAsync(receiverDid, "bgz-receiver", "notification");
                if (notificationEndpoint == null)
                {
                    throw new Exception("Unable to retrieve notification endpoint");
                }
                _logger.LogInformation($"Retrieved notification endpoint: {notificationEndpoint}");

                // get an access token so we can send the notification task to the receiver
                string accessToken = "";
                _logger.LogWarning("TODO: Implemented retrieval of access token."); // TODO
                //string? accessToken = await _nutsClient.GetAccessTokenAsync(receiverDid, senderDid);
                //if (accessToken == null)
                //{
                //    throw new Exception("Unable to obtain access token");
                //}
                //_logger.LogInformation($"Access token obtained: {accessToken}");

                // get the fhir endpoint of the sender
                string? senderFhirEndpoint = await _nutsClient.GetEndpointAsync(receiverDid, "bgz-sender", "fhir");
                if (senderFhirEndpoint == null)
                {
                    throw new Exception("Unable to retrieve FHIR endpoint of sender");
                }
                _logger.LogInformation($"Retrieved FHIR endpoint of sender: {senderFhirEndpoint}");
                
                // create a notification task to send
                Hl7.Fhir.Model.Task task = CreateNotificationTask(vcDid, senderDid, receiverDid, senderFhirEndpoint);
                _logger.LogInformation($"Created notification task");

                // send the newly created notification task to the notification endpoint (i.e. fhir server) of the organization we are referring to
                Hl7.Fhir.Model.Task? result = await SendNotificationAsync(notificationEndpoint, accessToken, task);
                if (result == null)
                {
                    throw new Exception("Unable to send notification task");
                }
                _logger.LogInformation("Notification task sent successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError($"An error occurred during the execution of $refer: {ex.Message}");
            }

            _ = await Task.FromResult(true);
        }
        
        private async Task<Hl7.Fhir.Model.Task?> SendNotificationAsync(string notificationEndpoint, string accessToken, Hl7.Fhir.Model.Task task)
        {
            var fhirClient = new FhirClient(notificationEndpoint);
            
            fhirClient.RequestHeaders.Add("Authorization", "Bearer " + accessToken);
       
             return await fhirClient.CreateAsync(task);
        }

        private Hl7.Fhir.Model.Task CreateNotificationTask(string vcDid, string senderDid, string receiverDid, string senderFhirEndpoint)
        {
            var task = new Hl7.Fhir.Model.Task
            {
                Status = Hl7.Fhir.Model.Task.TaskStatus.Requested,
                Intent = RequestIntent.Proposal,
                BasedOn = new List<ResourceReference> {new("Task/eb73e02a-3bd0-46f6-a66d-b0ecb7720d47")} // based on preloaded WorkflowTask
            };

            task.Requester = new Hl7.Fhir.Model.Task.RequesterComponent();
            task.Requester.Agent = new ResourceReference
            {
                Identifier = new Identifier("https://www.w3.org/ns/did/v1", senderFhirEndpoint)
            };

            // the did of the organization that is sending the referral
            task.Requester.OnBehalfOf = new ResourceReference
            {
                Identifier = new Identifier("https://www.w3.org/ns/did/v1", senderDid)
            };

            // the did of the organization that is receiving the referral
            task.Owner = new ResourceReference
            {
                Identifier = new Identifier("https://www.w3.org/ns/did/v1", receiverDid)
            };

            task.Input = new List<Hl7.Fhir.Model.Task.ParameterComponent>();

            var authInput = new Hl7.Fhir.Model.Task.ParameterComponent
            {
                Type = new CodeableConcept("http://xxx.nl/fhir/CodeSystem/TaskParameterType", "authorization_base"),
                Value = new FhirString(vcDid)
            };
            task.Input.Add(authInput);

            var taskInput = new Hl7.Fhir.Model.Task.ParameterComponent
            {
                Type = new CodeableConcept("http://xxx.nl/fhir/CodeSystem/TaskParameterType", "workflow-task"),
                Value = new ResourceReference("Task/eb73e02a-3bd0-46f6-a66d-b0ecb7720d47")
            };
            task.Input.Add(taskInput);
            
            return task;
        }
    }
}
