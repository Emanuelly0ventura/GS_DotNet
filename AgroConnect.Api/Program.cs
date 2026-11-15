using AgroConnect.Api.Endpoints;
using AgroConnect.Api.Exceptions;
using AgroConnect.Api.Extensions;
using AgroConnect.Api.Filters;
using AgroConnect.Api.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.AddAgroConnectPersistence();
builder.AddAgroConnectAuthentication();
builder.AddAgroConnectCors();
builder.AddAgroConnectRateLimiting();
builder.AddAgroConnectSwagger();
builder.AddAgroConnectObservability();
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(new UtcDateConverter()));
builder.Services.AddScoped<IdempotencyService>();

var app = builder.Build();
app.UseAgroConnectErrors();
app.UseSwagger();
app.UseSwaggerUI();
app.UseCors();
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();
app.MapAgroConnectHealth();

var api = app.MapGroup("/api/v1").RequireRateLimiting("api");
api.MapAuthEndpoints();
api.MapPropertyEndpoints();
api.MapFieldEndpoints();

await app.InitializeDatabaseAsync();
app.Run();

public partial class Program;
