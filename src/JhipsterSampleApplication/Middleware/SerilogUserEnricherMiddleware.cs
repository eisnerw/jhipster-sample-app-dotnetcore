using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Serilog.Context;

namespace JhipsterSampleApplication.Middleware;

public class SerilogUserEnricherMiddleware
{
    private readonly RequestDelegate _next;

    public SerilogUserEnricherMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var userName = context?.User?.Identity?.IsAuthenticated == true
            ? (context!.User.Identity!.Name ?? string.Empty)
            : string.Empty;
        var userBrackets = string.IsNullOrWhiteSpace(userName) ? "[]" : $"[{userName}]";
        var route = context?.GetEndpoint()?.DisplayName ?? context?.Request?.Path.Value ?? string.Empty;

        // ECS-friendly fields: user.name and labels.route/module
        using (LogContext.PushProperty("user.name", userName))
        using (LogContext.PushProperty("UserName", userBrackets))
        using (LogContext.PushProperty("labels.route", route))
        using (LogContext.PushProperty("labels.client_ip", context?.Connection?.RemoteIpAddress?.ToString()))
        using (LogContext.PushProperty("trace.id", context?.TraceIdentifier))
        {
            await _next(context);
        }
    }
}
