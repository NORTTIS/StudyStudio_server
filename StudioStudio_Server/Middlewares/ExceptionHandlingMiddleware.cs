using System.Globalization;
using System.Net;
using StudioStudio_Server.Exceptions;
using StudioStudio_Server.Localization;
using StudioStudio_Server.Models.DTOs.Response;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IWebHostEnvironment _env;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger,
        IWebHostEnvironment env)
    {
        _next = next;
        _logger = logger;
        _env = env;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (AppException ex)
        {
            await HandleAppException(context, ex);
        }
        catch (Exception ex)
        {
            await HandleUnexpectedException(context, ex);
        }
    }

    private async Task HandleAppException(HttpContext context, AppException ex)
    {
        var culture = GetCulture(context);
        var localizer = new JsonStringLocalizer(_env, culture);

        context.Response.StatusCode = ex.HttpStatus;
        context.Response.ContentType = "application/json";

        var response = ApiResponse<object>.Error(
            ex.Code,
            localizer.Get(ex.Code)
        );

        await context.Response.WriteAsJsonAsync(response);
    }

    private async Task HandleUnexpectedException(HttpContext context, Exception ex)
    {
        _logger.LogError(ex, "Unhandled exception");

        var culture = GetCulture(context);
        var localizer = new JsonStringLocalizer(_env, culture);

        context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
        context.Response.ContentType = "application/json";

        var response = ApiResponse<object>.Error(
            ErrorCodes.UnexpectedError,
            localizer.Get(ErrorCodes.UnexpectedError)
        );

        await context.Response.WriteAsJsonAsync(response);
    }

    private static string GetCulture(HttpContext context)
    {
        var lang = context.Request.Headers["Accept-Language"].FirstOrDefault();
        return string.IsNullOrWhiteSpace(lang) ? "vi" : lang.Split(',')[0];
    }
}
