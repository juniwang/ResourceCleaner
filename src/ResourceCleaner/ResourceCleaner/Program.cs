using Microsoft.Extensions.Configuration;

namespace ResourceCleaner
{
    public class Program
    {
        const string Azure = "Azure";

        public async static Task Main(string[] args)
        {
            var options = GetOptions(args);
            Console.WriteLine($"Options loaded:");
            Console.WriteLine($"CloudInstance: {options.CloudInstance}");
            Console.WriteLine($"TenantId: {DisplayGuid(options.TenantId)}");
            Console.WriteLine($"SubscriptionId: {DisplayGuid(options.SubscriptionId)}");
            Console.WriteLine($"ClientId: {DisplayGuid(options.ClientId)}");
            Console.WriteLine($"TTLHours: {options.TTLHours}");

            var cleaner = new ResouceGroupCleaner(options);
            await cleaner.TriggerCleanUp();
        }

        private static AzureOptions GetOptions(string[] args)
        {
            IConfiguration configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables()
                .AddCommandLine(args)
                .AddUserSecrets(typeof(AzureOptions).Assembly)
                .Build();

            var azureOptions = new AzureOptions();
            configuration.GetSection(Azure).Bind(azureOptions);

            return azureOptions;
        }

        private static string DisplayGuid(string guid)
        {
            if (string.IsNullOrWhiteSpace(guid))
            {
                return "";
            }

            if (guid.Length <= 8)
            {
                return guid;
            }

            return $"{guid.Substring(0, 4)}{new string('*', guid.Length - 8)}{guid.Substring(guid.Length - 4)}";
        }
    }
}