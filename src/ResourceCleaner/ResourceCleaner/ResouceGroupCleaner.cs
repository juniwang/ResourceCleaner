using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Resources;
using Azure.ResourceManager.Resources.Models;
using Microsoft.Identity.Client;
using System.Security.Cryptography.X509Certificates;

namespace ResourceCleaner
{
    internal class ResouceGroupCleaner
    {
        const string CreatedTime = "CreatedTime";
        static readonly string[] DefaultReservedGroups = new string[]
        {
            // well-known
            "NetworkWatcherRG",
            "AzSecPackAutoConfigRG",
            // public
            "srprodcloudtestrg",
            // mc
            "SignalRServiceE2ETest",
            "srmccloudtestrg",
            "signalrdevmc",
            // ff
            "SignalRServiceE2ETest",
            "srffcloudtestrg"
        };
        static readonly string[] DefaultReserveGroupsPrefix = new string[]
        {
            "cloud-shell-storage-",
            "DefaultResourceGroup-",
        };

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

            // check and cleanup
            Console.WriteLine($"{Environment.NewLine}enumerate groups and try cleaning up:");
            await foreach (var group in groups)
            {
                try
                {
                    await CheckAndCleanup(group);
                }
                catch (Exception e)
                {
                    Console.WriteLine($"{group.Data.Name}: unhandled exception: {e.Message}");
                    Console.WriteLine(e.ToString());
                }
            }
        }

        private async Task CheckAndCleanup(ResourceGroupResource resourceGroup)
        {
            var groupName = resourceGroup.Data.Name;
            // reserved groups
            if (IsReserved(groupName))
            {
                LogWithSkipReason(groupName, "known reserved group.");
                return;
            }

            // check locks
            if (await IsLocked(resourceGroup))
            {
                LogWithSkipReason(groupName, "locked from deletion.");
                return;
            }
            
            // check TTL
            if (await IsWithinTTL(resourceGroup))
            {
                LogWithSkipReason(groupName, $"resourceGroup was created within {options.TTLHours} hours.");
                return;
            }

            // deleting group
            await resourceGroup.DeleteAsync(Azure.WaitUntil.Completed);
            Console.WriteLine($"{groupName}: Deleted.");
        }

        private async Task<bool> IsLocked(ResourceGroupResource resourceGroup)
        {
            var locks = resourceGroup.GetManagementLocks();
            await foreach (var @lock in locks)
            {
                if (@lock.Data.Level == ManagementLockLevel.CanNotDelete)
                {
                    return true;
                }
            }

            return false;
        }

        private async Task<bool> IsWithinTTL(ResourceGroupResource resourceGroup)
        {
            DateTimeOffset createdon = DateTimeOffset.MaxValue;
            if (resourceGroup.Data.SystemData != null && resourceGroup.Data.SystemData.CreatedOn.HasValue)
            {
                // get createdOn from group systemData
                createdon = resourceGroup.Data.SystemData.CreatedOn.Value;
            }
            else if (resourceGroup.Data.Tags != null
                && resourceGroup.Data.Tags.TryGetValue(CreatedTime, out string dateString)
                && DateTimeOffset.TryParse(dateString, out DateTimeOffset dt))
            {
                // get from group tag
                createdon = dt;
            }
            else
            {
                // get the earliest createdOn in group resources.
                var enumerator = resourceGroup.GetGenericResourcesAsync().GetAsyncEnumerator();
                while (await enumerator.MoveNextAsync())
                {
                    var resource = enumerator.Current;
                    if (resource.HasData && resource.Data.SystemData != null && resource.Data.SystemData.CreatedOn.HasValue)
                    {
                        if (resource.Data.SystemData.CreatedOn.Value < createdon)
                        {
                            createdon = resource.Data.SystemData.CreatedOn.Value;
                        }
                    }
                    // break if ttl already expired
                    if (createdon != DateTimeOffset.MaxValue
                        && createdon.AddHours(options.TTLHours) < DateTimeOffset.UtcNow)
                    {
                        break;
                    }
                }
            }

            // backfill CreatedTime to now.
            if (createdon == DateTimeOffset.MaxValue)
            {
                createdon = DateTimeOffset.UtcNow;
                var tags = resourceGroup.Data.Tags ?? new Dictionary<string, string>();
                tags[CreatedTime] = createdon.ToString();
                Console.WriteLine($"{resourceGroup.Data.Name}: Cannot determine the createdTime. Backfill to DateTimeOffset.UtcNow.");
                await resourceGroup.SetTagsAsync(tags);
            }

            return createdon.AddHours(options.TTLHours) > DateTimeOffset.UtcNow;
        }

        private bool IsReserved(string groupName)
        {
            // by prefix, for CloudShell, Default group etc
            foreach (var prefix in DefaultReserveGroupsPrefix)
            {
                if (groupName.StartsWith(prefix))
                {
                    return true;
                }
            }
            // by name matching
            foreach (var reserved in DefaultReservedGroups.Concat(options.ReservedGroups))
            {
                if (groupName == reserved)
                {
                    return true;
                }
            }

            return false;
        }


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

        private void LogWithSkipReason(string groupName, string reason)
        {
            Console.WriteLine($"{groupName}: skipped. reason: {reason}");
        }
    }
}
