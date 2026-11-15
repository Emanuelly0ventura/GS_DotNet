using AgroConnect.Api.Filters;
using Microsoft.OpenApi.Models;

namespace AgroConnect.Api.Extensions;

public static class SwaggerExtensions
{
    public static void AddAgroConnectSwagger(this WebApplicationBuilder builder)
    {
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo { Title = "AgroConnect API", Version = "v1" });
            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT",
                Description = "Informe o accessToken recebido no login."
            });
            options.OperationFilter<ApiDocumentation>();
        });
    }
}
