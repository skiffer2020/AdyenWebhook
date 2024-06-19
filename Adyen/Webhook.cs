using System.Net.Http.Headers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;
using System.Text.Json;
using System.Text;
using System.Web;
using System.Text.Encodings.Web;

namespace Adyen
{
    public class Webhook
    {
        private static string[] s_scopes = new string[] { "https://api.businesscentral.dynamics.com/.default" };
        internal static Guid FunctionExecutionUnit;
        private readonly ILogger<Webhook> _logger;

        public Webhook(ILogger<Webhook> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        [Function("AdyenCloud")]
        public async Task<IActionResult> AdyenCloud(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post",
            Route = "AdyenCloud/{tenant}/{environment}/{webhookRef}")]  HttpRequest req,
            string tenant, string environment, string webhookRef)
        {
            return await Cloud(req, tenant, environment, webhookRef);
        }
        public async Task<IActionResult> Cloud(HttpRequest req, string tenant, string environment, string webhookRef)
        {
            try
            {
                _logger.LogInformation(JsonSerializer.Serialize(new { h = req.Headers }));

                var s_app = ConfidentialClientApplicationBuilder
                .Create(System.Environment.GetEnvironmentVariable("ClientId"))
                .WithTenantId(tenant)
                .WithClientSecret(System.Environment.GetEnvironmentVariable("AppSecret"))
                .Build();
                AuthenticationResult authResult = await s_app.AcquireTokenForClient(s_scopes)
                    .ExecuteAsync();

                HttpClient client = new HttpClient();

                string companyName = req.Query["CompanyName"];
                string decodedCompanyName = HttpUtility.UrlDecode(companyName);

                client.BaseAddress =
                    new Uri($"https://api.businesscentral.dynamics.com/v2.0/{tenant}/{environment}/ODataV4/AdyenWebhook_ReceiveWebhook?company={decodedCompanyName}");
                client.DefaultRequestHeaders.Clear();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authResult.AccessToken);

                string requestContent;
                using (var reader = new StreamReader(req.Body, Encoding.UTF8))
                {
                    requestContent = await reader.ReadToEndAsync();
                }

                BcRequest BcContent = new BcRequest
                {
                    FromHost = req.Host.ToString(),
                    Path = req.Path.ToString(),
                    HttpMethod = req.Method,
                    Query = req.QueryString.ToString(),
                    HeadersDictionary = req.Headers,
                    Content = requestContent,
                    WebhookReference = webhookRef
                };

                HttpContent content = new StringContent(JsonSerializer.Serialize(new { json = JsonSerializer.Serialize(BcContent) }));
                content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                var bc = await client.PostAsync("", content);
                if (!bc.IsSuccessStatusCode)
                    _logger.LogInformation("Error: " + await bc.Content.ReadAsStringAsync());
                    return new ObjectResult("")
                    {
                        StatusCode = (int)bc.StatusCode,
                        Value = await bc.Content.ReadAsStringAsync()
                    };
            }
            catch (Exception e)
            {
                _logger.LogTrace(e, "Error");
                return new ObjectResult("")
                {
                    StatusCode = 500,
                    Value = e.ToString()
                };
            }
        }
        public class BcRequest
        {
            public string FromHost { get; set; }
            public string Path { get; set; }
            public string Query { get; set; }
            public IHeaderDictionary HeadersDictionary { get; set; }
            public string Content { get; set; }
            public string WebhookReference { get; set; }
            public object HttpMethod { get; internal set; }
        }
    }
}
