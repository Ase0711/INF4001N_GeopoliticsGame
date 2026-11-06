using GeopoliticsGame.Core.Engine;
using GeopoliticsGame.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace GeopoliticsGame.Web.Filters;

public sealed class ApiExceptionFilter : IExceptionFilter
{
    private readonly ILogger<ApiExceptionFilter> _logger;

    public ApiExceptionFilter(ILogger<ApiExceptionFilter> logger) => _logger = logger;

    public void OnException(ExceptionContext context)
    {
        var (status, message) = context.Exception switch
        {
            GameRuleException e => (StatusCodes.Status400BadRequest, e.Message),
            GameNotFoundException e => (StatusCodes.Status404NotFound, e.Message),
            GameAccessDeniedException e => (StatusCodes.Status403Forbidden, e.Message),
            _ => (0, string.Empty)
        };

        if (status == 0)
        {
            _logger.LogError(context.Exception, "Unhandled API error.");
            status = StatusCodes.Status500InternalServerError;
            message = "Something went wrong. Please try again.";
        }

        context.Result = new ObjectResult(new { error = message }) { StatusCode = status };
        context.ExceptionHandled = true;
    }
}
