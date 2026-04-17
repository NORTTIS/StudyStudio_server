using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using StudioStudio_Server.Exceptions;
using StudioStudio_Server.Models.DTOs.Response;
using StudioStudio_Server.Resources.Localization;

namespace StudioStudio_Server.Filters
{
    public class ValidationFilter(IWebHostEnvironment env, IHttpContextAccessor httpContextAccessor) : IAsyncActionFilter
    {
        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            if (!context.ModelState.IsValid)
            {
                var culture = GetCulture();
                var localizer = new JsonStringLocalizer(env, culture);

                var errors = new Dictionary<string, List<string>>();
                var firstErrorCode = ErrorCodes.ValidationRequiredField;
                var firstErrorFound = false;

                foreach (var entry in context.ModelState)
                {
                    if (entry.Value.Errors.Count > 0)
                    {
                        var errorMessages = new List<string>();

                        foreach (var error in entry.Value.Errors)
                        {
                            var errorCode = error.ErrorMessage;

                            // Check if it's a valid error code
                            if (IsErrorCode(errorCode))
                            {
                                if (!firstErrorFound)
                                {
                                    firstErrorCode = errorCode;
                                    firstErrorFound = true;
                                }
                                errorMessages.Add(localizer.Get(errorCode));
                            }
                            else
                            {
                                // Fallback to original message if not an error code
                                errorMessages.Add(errorCode);
                            }
                        }

                        errors[entry.Key] = errorMessages;
                    }
                }

                var response = ApiResponse<Dictionary<string, List<string>>>.Error(
                    firstErrorCode,
                    localizer.Get(firstErrorCode),
                    errors
                );

                context.Result = new BadRequestObjectResult(response);
                return;
            }

            await next();
        }

        private string GetCulture()
        {
            var context = httpContextAccessor.HttpContext;
            if (context == null) return "vi";

            var lang = context.Request.Headers["Accept-Language"].FirstOrDefault();
            return string.IsNullOrWhiteSpace(lang) ? "vi" : lang.Split(',')[0];
        }

        private static bool IsErrorCode(string message)
        {
            // Check if the message matches error code pattern
            return !string.IsNullOrEmpty(message) &&
                   (message.StartsWith("VALIDATION") ||
                    message.StartsWith("AUTH") ||
                    message.StartsWith("USER") ||
                    message.StartsWith("REPORT") ||
                    message.StartsWith("TASK") ||
                    message.StartsWith("ANNOUNCEMENT") ||
                    message.StartsWith("SUCCESS") ||
                    message.StartsWith("SYS"));
        }
    }
}
