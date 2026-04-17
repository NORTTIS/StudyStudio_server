using StudioStudio_Server.Exceptions;
using StudioStudio_Server.Models.DTOs.Response;
using StudioStudio_Server.Resources.Localization;
using StudioStudio_Server.Utils;
using System.Net;

namespace StudioStudio_Server.Middlewares;

public class ExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<ExceptionHandlingMiddleware> logger,
    IWebHostEnvironment env)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
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
        var culture = HttpContextHelper.GetCultureFromHeader(context);
        var localizer = new JsonStringLocalizer(env, culture);

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
        logger.LogError(ex, "Unhandled exception");

        var culture = HttpContextHelper.GetCultureFromHeader(context);
        var localizer = new JsonStringLocalizer(env, culture);

        context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
        context.Response.ContentType = "application/json";

        var response = ApiResponse<object>.Error(
            ErrorCodes.UnexpectedError,
            localizer.Get(ErrorCodes.UnexpectedError)
        );

        await context.Response.WriteAsJsonAsync(response);
    }
}