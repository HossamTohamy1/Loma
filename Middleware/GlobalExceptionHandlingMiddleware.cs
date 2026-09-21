using System.Net;
using System.Text.Json;
using AuraCommerce.Application.Common.Exceptions;
using AuraCommerce.Application.Common.Models;
using AuraCommerce.Domain.Exceptions;

namespace AuraCommerce.Api.Middleware;

public class GlobalExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandlingMiddleware> _logger;

    public GlobalExceptionHandlingMiddleware(RequestDelegate next, ILogger<GlobalExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception caught by middleware: {Message}", ex.Message);
            await HandleExceptionAsync(context, ex);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";

        var statusCode = HttpStatusCode.InternalServerError;
        ApiResponse<object> response;

        switch (exception)
        {
            case ValidationException validationException:
                statusCode = HttpStatusCode.BadRequest;
                response = ApiResponse<object>.ErrorResult("Validation failed.", validationException.Errors);
                break;

            case NotFoundException notFoundException:
                statusCode = HttpStatusCode.NotFound;
                response = ApiResponse<object>.ErrorResult(notFoundException.Message);
                break;

            case DomainException domainException:
                statusCode = HttpStatusCode.BadRequest;
                response = ApiResponse<object>.ErrorResult(domainException.Message);
                break;

            default:
                statusCode = HttpStatusCode.InternalServerError;
                response = ApiResponse<object>.ErrorResult("An unexpected error occurred.");
                break;
        }

        context.Response.StatusCode = (int)statusCode;
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        var json = JsonSerializer.Serialize(response, options);
        await context.Response.WriteAsync(json);
    }
}
