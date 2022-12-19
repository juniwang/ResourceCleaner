using Microsoft.Extensions.Configuration;

namespace ResourceCleaner
{
    public class Program
    {
        const string Azure = "Azure";

        public static void Main(string[] args)
        {
            var options = GetOptions(args);
            Console.WriteLine($"TenantId: {options.TenantId}");
            Console.WriteLine($"Cert: {options.ClientCertificate}");
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
    }
}