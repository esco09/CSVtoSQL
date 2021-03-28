using System;
using Azure.Identity;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;

[assembly: FunctionsStartup(typeof(CSVtoSQL.Startup))]

namespace CSVtoSQL
{
    public class Startup : FunctionsStartup
    {
        public override void ConfigureAppConfiguration(IFunctionsConfigurationBuilder builder)
        {
            var configuration = builder.ConfigurationBuilder
                .AddJsonFile("local.settings.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();

            builder.ConfigurationBuilder.AddAzureAppConfiguration(options =>
            {
                //options.Connect(configuration["ConnectionStrings:AppConfig"])
                options.Connect(new Uri(configuration["AppConfigEndpoint"]), new DefaultAzureCredential())
                    .ConfigureKeyVault(kv =>
                    {
                        kv.SetCredential(new DefaultAzureCredential());
                    });
            });

            builder.ConfigurationBuilder.Build();
        }

        public override void Configure(IFunctionsHostBuilder builder)
        {
        }
    }
}
