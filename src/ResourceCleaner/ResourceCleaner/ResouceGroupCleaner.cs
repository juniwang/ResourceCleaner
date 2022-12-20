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
            var armClient = GetArmClient(credential);
            var subscription = await armClient.GetDefaultSubscriptionAsync();
            Console.WriteLine($"Subscription: {subscription.Data.DisplayName}");
            Console.WriteLine("ResourceGroups:");
            var groups = subscription.GetResourceGroups();
            await foreach (var group in groups)
            {
                var ss = group.Data.SystemData;
                Console.WriteLine(group.Data.Name);
            }
        }

        //private async Task

        private ArmClient GetArmClient(TokenCredential credential)
        {
            switch (options.CloudInstance)
            {
                case AzureCloudInstance.AzurePublic:
                    return new ArmClient(credential, options.SubscriptionId);
                case AzureCloudInstance.AzureUsGovernment:
                    return new ArmClient(credential, options.SubscriptionId, new ArmClientOptions
                    {
                        Environment = ArmEnvironment.AzureGovernment
                    });
                case AzureCloudInstance.AzureChina:
                    return new ArmClient(credential, options.SubscriptionId, new ArmClientOptions
                    {
                        Environment = ArmEnvironment.AzureChina
                    });
                default:
                    throw new ArgumentOutOfRangeException("unsupported AzureCloudInstance.");
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
