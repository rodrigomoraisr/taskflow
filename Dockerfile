# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

# Restore layers survive changes to source files.
COPY src/TaskFlow.Domain/TaskFlow.Domain.csproj src/TaskFlow.Domain/
COPY src/TaskFlow.Application/TaskFlow.Application.csproj src/TaskFlow.Application/
COPY src/TaskFlow.Infrastructure/TaskFlow.Infrastructure.csproj src/TaskFlow.Infrastructure/
COPY src/TaskFlow.Api/TaskFlow.Api.csproj src/TaskFlow.Api/
COPY src/TaskFlow.Migrator/TaskFlow.Migrator.csproj src/TaskFlow.Migrator/
RUN dotnet restore src/TaskFlow.Api/TaskFlow.Api.csproj \
    && dotnet restore src/TaskFlow.Migrator/TaskFlow.Migrator.csproj
COPY src/ src/

FROM build AS publish-api
RUN dotnet publish src/TaskFlow.Api/TaskFlow.Api.csproj \
    --configuration Release --no-restore --output /out /p:UseAppHost=false

FROM build AS publish-migrations
RUN dotnet publish src/TaskFlow.Migrator/TaskFlow.Migrator.csproj \
    --configuration Release --no-restore --output /out /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS migrations
WORKDIR /app
COPY --from=publish-migrations /out/ ./
USER $APP_UID
ENTRYPOINT ["dotnet", "TaskFlow.Migrator.dll"]

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS api
# curl is used by the image health check; the SDK and EF tooling stay in build stages.
USER root
RUN apt-get update \
    && apt-get install --yes --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=publish-api /out/ ./
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID
HEALTHCHECK --interval=10s --timeout=3s --start-period=10s --retries=5 \
    CMD curl --fail --silent --show-error --max-time 2 http://localhost:8080/health/ready || exit 1
ENTRYPOINT ["dotnet", "TaskFlow.Api.dll"]
