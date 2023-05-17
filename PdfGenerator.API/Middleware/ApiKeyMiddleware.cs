using DomainObjects.Constants;
using DomainObjects.Models.Config;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace PdfGenerator.API.Middleware;

public class ApiKeyMiddleware
{
    private readonly RequestDelegate _requestDelegate;
    private readonly ApiSecret _apiSecret;

    public ApiKeyMiddleware(RequestDelegate requestDelegate, IOptions<ApiSecret> option)
    {
        _requestDelegate = requestDelegate;
        _apiSecret = option.Value;
    }

    public async Task Invoke(HttpContext context)
    {
        var endpoint = context.GetEndpoint();
        var isAllowAnonymous = endpoint?.Metadata.Any(x => x.GetType() == typeof(AllowAnonymousAttribute)) ?? false;

        if (isAllowAnonymous)
        {
            await _requestDelegate(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue(GeneralConstants.ApiKeyHeader, out var apiKeyVal) 
            || !_apiSecret.Secret.Equals(apiKeyVal))
        {
            context.Response.StatusCode = 401;
            await context.Response.WriteAsync("Unauthorized client");
            return;
        }

        await _requestDelegate(context);
    }
}