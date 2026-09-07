using System.Text.Json;
using FluentValidation;

namespace NotificationService.Api.Middleware;

public sealed class ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (!context.RequestAborted.IsCancellationRequested)
        {
            throw;
        }
        catch (ValidationException ex)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            context.Response.ContentType = "application/problem+json";

            var details = new
            {
                type = "https://datatracker.ietf.org/doc/html/rfc9457",
                title = "Validation failed",
                status = StatusCodes.Status400BadRequest,
                errors = ex.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(g => string.IsNullOrEmpty(g.Key) ? "request" : g.Key, g => g.Select(e => e.ErrorMessage)),
            };

            await context.Response.WriteAsJsonAsync(details, JsonSerializerOptions.Web);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception during request {Method} {Path}", context.Request.Method, context.Request.Path);
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/problem+json";

            await context.Response.WriteAsJsonAsync(new
            {
                type = "https://datatracker.ietf.org/doc/html/rfc9457",
                title = "Internal server error",
                status = StatusCodes.Status500InternalServerError,
            }, JsonSerializerOptions.Web);
        }
    }
}
