using System.Globalization;
using System.Net;
using StudioStudio_Server.Exceptions;
using StudioStudio_Server.Localization;

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
            var culture = GetCulture(context);
            var localizer = new JsonStringLocalizer(_env, culture);

            context.Response.StatusCode = ex.HttpStatus;
            context.Response.ContentType = "application/json";

            await context.Response.WriteAsJsonAsync(new
            {
                status = "error",
                code = ex.Code,
                message = localizer.Get(ex.Code)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception");

            var culture = GetCulture(context);
            var localizer = new JsonStringLocalizer(_env, culture);

            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

            await context.Response.WriteAsJsonAsync(new
            {
                status = "error",
                code = ErrorCodes.UnexpectedError,
                message = localizer.Get(ErrorCodes.UnexpectedError)
            });
        }
    }

    private static string GetCulture(HttpContext context)
    {
        var lang = context.Request.Headers["Accept-Language"].FirstOrDefault();
        return string.IsNullOrWhiteSpace(lang) ? "vi" : lang.Split(',')[0];
    }
}
