using AgroConnect.Api.Extensions;

namespace AgroConnect.Api.Exceptions;

public static class ApiErrorHandling
{
    public static void UseAgroConnectErrors(this WebApplication app)
    {
        app.UseExceptionHandler(branch => branch.Run(context =>
            HttpExtensions.Error(500, "INTERNAL_ERROR", "Ocorreu um erro interno.").ExecuteAsync(context)));
        app.UseStatusCodePages(context =>
        {
            var http = context.HttpContext;
            return (http.Response.StatusCode switch
            {
                400 or 415 or 422 => HttpExtensions.Error(http.Response.StatusCode, "VALIDATION_ERROR", "Verifique o corpo e os parâmetros da requisição."),
                401 => HttpExtensions.Error(401, "UNAUTHORIZED", "Sessão inválida ou expirada."),
                403 => HttpExtensions.Error(403, "FORBIDDEN", "Você não tem permissão para acessar este recurso."),
                404 => HttpExtensions.Error(404, "NOT_FOUND", "Recurso não encontrado."),
                _ => HttpExtensions.Error(http.Response.StatusCode, "HTTP_ERROR", "Não foi possível concluir a requisição.")
            }).ExecuteAsync(http);
        });
    }
}
