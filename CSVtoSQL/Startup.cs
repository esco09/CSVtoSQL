using System;
using System.IO;
using System.Reflection;
using Azure.Extensions.AspNetCore.Configuration.Secrets;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;

[assembly: FunctionsStartup(typeof(CSVtoSQL.Startup))]

namespace CSVtoSQL
{
    public class Startup : FunctionsStartup
    {
        public override void ConfigureAppConfiguration(IFunctionsConfigurationBuilder builder)
        {
            var basePath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "..");

            var configuration = builder.ConfigurationBuilder
                .SetBasePath(basePath)
                .AddJsonFile("local.settings.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();

            var secretClient = new SecretClient(
                new Uri($"https://{configuration["KeyVaultName"]}.vault.azure.net/"),
                new DefaultAzureCredential());

           builder.ConfigurationBuilder.AddAzureKeyVault(secretClient, new KeyVaultSecretManager());

            /*
            builder.ConfigurationBuilder.AddAzureAppConfiguration(options =>
            {
                //options.Connect(configuration["ConnectionStrings:AppConfig"])
                options.Connect(new Uri(configuration["AppConfigEndpoint"]), new DefaultAzureCredential())
                    .ConfigureKeyVault(kv =>
                    {
                        kv.SetCredential(new DefaultAzureCredential());
                    });
            });*/

            builder.ConfigurationBuilder.Build();
        }

        public override void Configure(IFunctionsHostBuilder builder)
        {
        }
    }
}
