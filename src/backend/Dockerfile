FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["SalesTrainer.Api.csproj", "SalesTrainer.Api/"]
RUN dotnet restore "SalesTrainer.Api/SalesTrainer.Api.csproj"
COPY . ./SalesTrainer.Api/
WORKDIR "/src/SalesTrainer.Api"
RUN dotnet build "SalesTrainer.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "SalesTrainer.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
COPY appsettings*.json ./

ENTRYPOINT ["dotnet", "SalesTrainer.Api.dll"]
