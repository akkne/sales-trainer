# Build context is src/backend (the backend root) so the gateway can reference the
# shared building-blocks project that lives next to it:
#   docker build -f gateway/Gateway/Dockerfile src/backend
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Restore against just the project files first for better layer caching.
COPY building-blocks/BuildingBlocks/Sellevate.BuildingBlocks.csproj building-blocks/BuildingBlocks/
COPY gateway/Gateway/Sellevate.Gateway.csproj gateway/Gateway/
RUN dotnet restore gateway/Gateway/Sellevate.Gateway.csproj

COPY building-blocks/ building-blocks/
COPY gateway/ gateway/

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish gateway/Gateway/Sellevate.Gateway.csproj -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

ENTRYPOINT ["dotnet", "Sellevate.Gateway.dll"]
