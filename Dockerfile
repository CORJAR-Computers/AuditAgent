FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY src/AuditAgent.Core/AuditAgent.Core.csproj AuditAgent.Core/
COPY src/AuditAgent.Security/AuditAgent.Security.csproj AuditAgent.Security/
COPY src/AuditAgent.Api/AuditAgent.Api.csproj AuditAgent.Api/
RUN dotnet restore AuditAgent.Api/AuditAgent.Api.csproj
COPY . .
RUN dotnet publish AuditAgent.Api/AuditAgent.Api.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8443
ENTRYPOINT ["dotnet", "AuditAgent.Api.dll"]
