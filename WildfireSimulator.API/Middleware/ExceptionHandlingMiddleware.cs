using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;

namespace WildfireSimulator.API.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IWebHostEnvironment _environment;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger,
        IWebHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception occurred");
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";
        
        var statusCode = exception switch
        {
            KeyNotFoundException => HttpStatusCode.NotFound,
            ArgumentException => HttpStatusCode.BadRequest,
            UnauthorizedAccessException => HttpStatusCode.Unauthorized,
            InvalidOperationException => HttpStatusCode.Conflict,
            DbUpdateException => HttpStatusCode.InternalServerError,
            _ => HttpStatusCode.InternalServerError
        };

        context.Response.StatusCode = (int)statusCode;

        var response = new
        {
            error = new
            {
                message = GetErrorMessage(exception),
                type = exception.GetType().Name,
                stackTrace = _environment.IsDevelopment() ? exception.StackTrace : null,
                innerException = _environment.IsDevelopment() && exception.InnerException != null 
                    ? new { message = exception.InnerException.Message, type = exception.InnerException.GetType().Name }
                    : null
            },
            timestamp = DateTime.UtcNow,
            path = context.Request.Path,
            method = context.Request.Method
        };

        var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var jsonResponse = JsonSerializer.Serialize(response, jsonOptions);
        
        await context.Response.WriteAsync(jsonResponse);
    }

    private string GetErrorMessage(Exception exception)
    {
        return exception switch
        {
            KeyNotFoundException => "Ресурс не найден",
            ArgumentException => "Некорректные параметры запроса",
            UnauthorizedAccessException => "Доступ запрещен",
            InvalidOperationException => "Операция не может быть выполнена",
            DbUpdateException => "Ошибка при сохранении данных",
            _ => "Внутренняя ошибка сервера"
        };
    }
}
