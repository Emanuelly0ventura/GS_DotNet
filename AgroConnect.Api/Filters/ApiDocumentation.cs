using AgroConnect.Application.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace AgroConnect.Api.Filters;

public sealed class ApiDocumentation : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var metadata = context.ApiDescription.ActionDescriptor.EndpointMetadata;
        if (metadata.OfType<IAuthorizeData>().Any() && !metadata.OfType<IAllowAnonymous>().Any())
        {
            operation.Security = [new OpenApiSecurityRequirement
            {
                [new OpenApiSecurityScheme { Reference = new() { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }] = []
            }];
            AddError("401", "Sessão inválida ou expirada.");
            AddError("403", "Acesso proibido.");
        }
        if (context.ApiDescription.RelativePath?.StartsWith("api/v1/") == true)
        {
            AddError("400", "Dados inválidos.");
            AddError("429", "Limite de requisições atingido.");
            AddError("500", "Erro interno.");
        }
        if (metadata.OfType<IdempotentAttribute>().Any())
        {
            operation.Parameters.Add(new OpenApiParameter
            {
                Name = "Idempotency-Key", In = ParameterLocation.Header,
                Schema = new OpenApiSchema { Type = "string", MaxLength = 128 },
                Description = "Reutilize a chave no retry da mesma operação. Sem chave, corpos iguais são deduplicados por cinco minutos."
            });
            AddError("409", "Chave já usada com conteúdo diferente.");
        }

        void AddError(string status, string description)
        {
            operation.Responses.TryAdd(status, new OpenApiResponse
            {
                Description = description,
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    ["application/json"] = new() { Schema = context.SchemaGenerator.GenerateSchema(typeof(ApiError), context.SchemaRepository) }
                }
            });
        }
    }
}
