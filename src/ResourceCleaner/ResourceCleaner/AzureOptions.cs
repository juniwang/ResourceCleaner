using Microsoft.Identity.Client;

namespace ResourceCleaner
{
    internal class AzureOptions
    {
        public AzureCloudInstance? CloudInstance { get; set; } = AzureCloudInstance.AzurePublic;
        public string TenantId { get; set; }
        public string SubscriptionId { get; set; }
        public string ClientId { get; set; }
        public string ClientCertificate { get; set; }
        public int TTLHours { get; set; } = 168; // 7 days
        public string[] ReservedGroups = new string[0];
    }
}
