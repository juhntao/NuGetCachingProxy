global using FastEndpoints;

using System.Net;
using System.Net.Http.Headers;
using System.Reflection;

using MongoDB.Driver;
using MongoDB.Entities;

using Nefarius.Utilities.AspNetCore;

using NuGetCachingProxy;
using NuGetCachingProxy.Core;

using Polly;
using Polly.Contrib.WaitAndRetry;

using Serilog;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args).Setup();

IConfigurationSection section = builder.Configuration.GetSection(nameof(ServiceConfig));

ServiceConfig? serviceConfig = section.Get<ServiceConfig>();

if (serviceConfig is null)
{
    Console.WriteLine("Missing service configuration, can't continue!");
    return;
}

builder.Services.Configure<ServiceConfig>(builder.Configuration.GetSection(nameof(ServiceConfig)));

builder.Services.AddFastEndpoints(options => options.SourceGeneratorDiscoveredTypes.AddRange(DiscoveredTypes.All));

builder.Services.AddHttpClient("UpstreamNuGetServer",
        client =>
        {
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(
                Assembly.GetEntryAssembly()?.GetName().Name!,
                Assembly.GetEntryAssembly()?.GetName().Version!.ToString()));

            client.BaseAddress = new Uri(serviceConfig.UpstreamUrl);
            if (serviceConfig.Credential.ContainsKey("UserName") && serviceConfig.Credential.ContainsKey("Password"))
            {
                var userName = serviceConfig.Credential["UserName"];
                var password = serviceConfig.Credential["Password"];
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes($"{userName}:{password}")));
            }
        })
    .ConfigurePrimaryHttpMessageHandler(_ => new HttpClientHandler
    {
        AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
    })
    .AddTransientHttpErrorPolicy(pb =>
        pb.WaitAndRetryAsync(Backoff.DecorrelatedJitterBackoffV2(TimeSpan.FromSeconds(3), 10)));

Log.Logger.Information("Initializing database connection");

await DB.InitAsync(serviceConfig.DatabaseName,
    MongoClientSettings.FromConnectionString(serviceConfig.ConnectionString));

WebApplication app = builder.Build().Setup();

app.UseRouting();
app.MapFastEndpoints();

app.Run();