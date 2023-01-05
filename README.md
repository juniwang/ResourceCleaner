# ResourceCleaner

## Dev setup

Make sure you have configured the AzureDefaultCredential on your local machine following this guide: https://learn.microsoft.com/en-us/dotnet/api/azure.identity.environmentcredential?view=azure-dotnet.

Need following environment variables:

| Key         | Required    | Comments    | Sample Value |
| ----------- | ----------- | ----------- | ------------ |
|ASPNETCORE_ENVIRONMENT|true|indicating it's local environment. Must be "Development". |Development|
|Azure__SubscriptionId|requried|Azure Subscription Id|00000000-0000-0000-0000-000000000001|
|Azure__CloudInstance|false|Cloud Name of Azure. Default to "AzurePublic". Allowed values: AzurePublic, AzureUsGovernment, AzureChina |AzurePublic|
|Azure__TTLHours|false|TTL in hours, default to 168 hours.|24|
