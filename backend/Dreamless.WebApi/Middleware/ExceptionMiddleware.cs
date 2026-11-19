using Dreamless.Application.Common;

namespace Dreamless.WebApi.Middleware;

public sealed class ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger, IHostEnvironment environment)
{
    public async Task Invoke(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (BusinessException exception)
        {
            context.Response.StatusCode = exception.StatusCode;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new { message = exception.Message });
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "未处理异常");
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/json";
            var message = environment.IsDevelopment() ? exception.Message : "服务器内部错误";
            await context.Response.WriteAsJsonAsync(new { message });
        }
    }
}
