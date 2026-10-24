using OnlineShopServer.Dtos;

namespace OnlineShopServer.Endpoints;

public static class ExceptionHandlingExtensions
{
    public static IApplicationBuilder UseApiExceptionHandler(this IApplicationBuilder app) =>
        app.UseExceptionHandler(error => error.Run(context =>
        {
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            return context.Response.WriteAsJsonAsync(
                new ErrorDto("internal_error", "Внутренняя ошибка сервера"));
        }));
}
