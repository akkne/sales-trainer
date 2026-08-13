# SDK image used for the build stage. The glibc default is what deploys build with.
# On arm64 under Apple's Virtualization Framework that image's JIT dies with SIGILL
# ("Illegal instruction" / "csc.dll exited with code 132") often enough that large
# projects never finish; the Alpine/musl SDK does not. docker-compose.local.yml
# switches this to -alpine locally. The published output is portable IL either way
# (no -r), so the runtime image below is unchanged. See docs/LOCAL_DEV.md.
ARG SDK_IMAGE=mcr.microsoft.com/dotnet/sdk:9.0

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080

# Optional NuGet package cache. Defaults to an EMPTY stage, so the deploy build
# restores from nuget.org exactly as before. docker-compose.local.yml overrides
# this stage with the host's ~/.nuget/packages (a named build context wins over a
# stage of the same name), which is what makes a cold local build survive a slow
# link — see docs/LOCAL_DEV.md.
FROM scratch AS nugetcache

FROM ${SDK_IMAGE} AS build
ARG BUILD_CONFIGURATION=Release
ENV NUGET_FALLBACK_PACKAGES=/nuget-fallback
WORKDIR /src

COPY building-blocks/BuildingBlocks/Sellevate.BuildingBlocks.csproj building-blocks/BuildingBlocks/
COPY company-service/Company/Sellevate.Company.csproj company-service/Company/
RUN --mount=type=bind,from=nugetcache,target=/nuget-fallback,ro \
    dotnet restore company-service/Company/Sellevate.Company.csproj

COPY building-blocks/ building-blocks/
COPY company-service/ company-service/

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN --mount=type=bind,from=nugetcache,target=/nuget-fallback,ro \
    dotnet publish company-service/Company/Sellevate.Company.csproj -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

ENTRYPOINT ["dotnet", "Sellevate.Company.dll"]
