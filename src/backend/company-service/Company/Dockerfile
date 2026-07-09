FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY building-blocks/BuildingBlocks/Sellevate.BuildingBlocks.csproj building-blocks/BuildingBlocks/
COPY company-service/Company/Sellevate.Company.csproj company-service/Company/
RUN dotnet restore company-service/Company/Sellevate.Company.csproj

COPY building-blocks/ building-blocks/
COPY company-service/ company-service/

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish company-service/Company/Sellevate.Company.csproj -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

ENTRYPOINT ["dotnet", "Sellevate.Company.dll"]
