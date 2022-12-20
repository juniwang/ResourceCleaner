using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Microsoft.Identity.Client;
using System.Security.Cryptography.X509Certificates;

namespace ResourceCleaner
{
    internal class ResouceGroupCleaner
    {
        readonly AzureOptions options;

        public ResouceGroupCleaner(AzureOptions azureOptions)
        {
            options = azureOptions;
        }

        public async Task TriggerCleanUp()
        {
            var credential = GetAzureCredential();

            var armClient = new ArmClient(credential, options.SubscriptionId);
            var subscription = await armClient.GetDefaultSubscriptionAsync();
            Console.WriteLine($"Browse subscription: {subscription.Data.DisplayName}");
            var groups = subscription.GetResourceGroups();
            await foreach (var group in groups)
            {
                Console.WriteLine(group.Data.Name);
            }
        }

        private TokenCredential GetAzureCredential()
        {
            if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development")
            {
                return new DefaultAzureCredential();
            }

            var authHost = options.CloudInstance switch
            {
                AzureCloudInstance.AzureChina => AzureAuthorityHosts.AzureChina,
                AzureCloudInstance.AzureUsGovernment => AzureAuthorityHosts.AzureGovernment,
                _ => AzureAuthorityHosts.AzurePublicCloud
            };
            var cert = new X509Certificate2(Convert.FromBase64String(options.ClientCertificate));
            var credentialOptions = new ClientCertificateCredentialOptions
            {
                SendCertificateChain = true,
                AuthorityHost = authHost,
            };
            var credential = new ClientCertificateCredential(options.TenantId, options.ClientId, cert, credentialOptions);

            Console.WriteLine($"Get credential with cert thumbprint: {cert.Thumbprint.Substring(0, 8)}{new string('*', cert.Thumbprint.Length - 8)}");
            return credential;
        }
    }
}
