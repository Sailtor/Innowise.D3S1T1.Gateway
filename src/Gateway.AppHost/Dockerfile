# See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

# This stage is used when running from VS in fast mode (Default for Debug configuration)
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# This stage is used to build the service project
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Directory.Build.props", "."]
COPY ["src/Gateway.AppHost/Gateway.AppHost.csproj", "src/Gateway.AppHost/"]
COPY ["src/Gateway.Presentation/Gateway.Presentation.csproj", "src/Gateway.Presentation/"]
COPY ["src/Gateway.Infrastructure/Gateway.Infrastructure.csproj", "src/Gateway.Infrastructure/"]
COPY ["src/Gateway.Application/Gateway.Application.csproj", "src/Gateway.Application/"]
COPY ["src/Gateway.Domain/Gateway.Domain.csproj", "src/Gateway.Domain/"]
RUN dotnet restore "src/Gateway.AppHost/Gateway.AppHost.csproj"
COPY . .
RUN dotnet build "src/Gateway.AppHost/Gateway.AppHost.csproj" -c $BUILD_CONFIGURATION -o /app/build

# This stage is used to publish the service project to be copied to the final stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "src/Gateway.AppHost/Gateway.AppHost.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# This stage is used in production or when running from VS in regular mode (Default when not using the Debug configuration)
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# No HEALTHCHECK here: the probe is declared in the infrastructure repo's docker-compose.yml, where
# the interval and the dependency gating live together. The app exposes /health (readiness - runs
# the metrics-db check) and /health/live (liveness - no checks at all) for it to point at.
#
# Whatever compose uses as its `test` runs INSIDE this container, and this image has no HTTP client
# - curl was removed from the .NET runtime images in .NET 5 and wget was never there. It does ship
# bash, though, so compose probes /health over bash's /dev/tcp redirection rather than making this
# image carry curl for the sake of a healthcheck. Keep bash available if that probe is to keep
# working (a switch to a chiselled/distroless base tag would remove it).
ENTRYPOINT ["dotnet", "Gateway.AppHost.dll"]