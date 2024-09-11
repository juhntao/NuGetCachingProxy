using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.Options;

using NuGetCachingProxy.Core;

namespace NuGetCachingProxy.Endpoints;

/// <summary>
///     This endpoint creates all other requests to the upstream server in proxy. This is required to replace the upstream
///     URL with this proxy server URL so the client actually fetches cached resources from us instead of directly going to
///     the upstream.
/// </summary>
internal sealed class CatchallEndpoint(IOptions<ServiceConfig> options, IHttpClientFactory clientFactory, ILogger<CatchallEndpoint> logger)
    : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("{**catch-all}");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if(HttpContext.Request.Method != "GET")
        {
            HttpContext.Response.StatusCode = 400;
            return;
        }

        string upstreamUrl = new Uri(options.Value.UpstreamUrl).GetLeftPart(UriPartial.Authority);

        Uri requestUrl = new(HttpContext.Request.GetDisplayUrl());
        string backendUrl = requestUrl.GetLeftPart(UriPartial.Authority);
        string requestPath = requestUrl.PathAndQuery;
        logger.LogInformation($"{HttpContext.Request.Method} {requestPath}");
        HttpClient client = clientFactory.CreateClient("UpstreamNuGetServer");

        var requestMessage = new HttpRequestMessage(System.Net.Http.HttpMethod.Get, requestPath);
        foreach(var h in HttpContext.Request.Headers)
        {
            if (h.Key.StartsWith("X-", StringComparison.OrdinalIgnoreCase) && !client.DefaultRequestHeaders.Contains(h.Key) && !requestMessage.Headers.Contains(h.Key))
            {
                requestMessage.Headers.TryAddWithoutValidation(h.Key, h.Value.ToString());
            }
        }

        HttpResponseMessage response = await client.SendAsync(requestMessage, ct);

        // pass error to client
        if (!response.IsSuccessStatusCode)
        {
            await SendStreamAsync(await response.Content.ReadAsStreamAsync(ct),
                contentType: response.Content.Headers.ContentType?.ToString() ?? "application/octet-stream",
                cancellation: ct);
            return;
        }

        if ("application/json".Equals(response.Content.Headers.ContentType?.MediaType, StringComparison.OrdinalIgnoreCase) || "application/xml".Equals(response.Content.Headers.ContentType?.MediaType, StringComparison.OrdinalIgnoreCase))
        {
            string body = await response.Content.ReadAsStringAsync(ct);

            body = body.Replace(upstreamUrl, backendUrl);

            await SendStringAsync(body, contentType: response.Content.Headers.ContentType.ToString(), cancellation: ct);
            return;
        }

        await SendStreamAsync(await response.Content.ReadAsStreamAsync(ct),
            contentType: response.Content.Headers.ContentType?.ToString() ?? "application/octet-stream",
            cancellation: ct);
    }
}