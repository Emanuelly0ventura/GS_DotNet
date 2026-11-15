FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props ./
COPY AgroConnect.Api/AgroConnect.Api.csproj AgroConnect.Api/
COPY AgroConnect.Application/AgroConnect.Application.csproj AgroConnect.Application/
COPY AgroConnect.Domain/AgroConnect.Domain.csproj AgroConnect.Domain/
COPY AgroConnect.Infrastructure/AgroConnect.Infrastructure.csproj AgroConnect.Infrastructure/
RUN dotnet restore AgroConnect.Api/AgroConnect.Api.csproj

COPY AgroConnect.Api/ AgroConnect.Api/
COPY AgroConnect.Application/ AgroConnect.Application/
COPY AgroConnect.Domain/ AgroConnect.Domain/
COPY AgroConnect.Infrastructure/ AgroConnect.Infrastructure/
RUN dotnet publish AgroConnect.Api/AgroConnect.Api.csproj --configuration Release --no-restore --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=10000
EXPOSE 10000
COPY --from=build /app/publish/ ./
USER $APP_UID
ENTRYPOINT ["dotnet", "AgroConnect.Api.dll"]
