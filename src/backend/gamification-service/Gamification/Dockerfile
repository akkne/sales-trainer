# Build context is src/backend (the backend root) so the service can reference the
# shared building-blocks project that lives next to it:
#   docker build -f gamification-service/Gamification/Dockerfile src/backend
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY building-blocks/BuildingBlocks/Sellevate.BuildingBlocks.csproj building-blocks/BuildingBlocks/
COPY gamification-service/Gamification/Sellevate.Gamification.csproj gamification-service/Gamification/
RUN dotnet restore gamification-service/Gamification/Sellevate.Gamification.csproj

COPY building-blocks/ building-blocks/
COPY gamification-service/ gamification-service/

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish gamification-service/Gamification/Sellevate.Gamification.csproj -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

ENTRYPOINT ["dotnet", "Sellevate.Gamification.dll"]
