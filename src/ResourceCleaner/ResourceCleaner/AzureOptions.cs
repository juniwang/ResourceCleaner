namespace ResourceCleaner
{
    internal class AzureOptions
    {
        public string TenantId { get; set; }
        public string SubscriptionId { get; set; }
        public string ClientId { get; set; }
        public string ClientCertificate { get; set; }
        public int TTLMinutes { get; set; }
    }
}
