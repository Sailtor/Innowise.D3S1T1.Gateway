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
COPY ["src/Gateway.Infrastructure/Gateway.Infrastructure.csproj", "src/Gateway.Infrastructure/"]
COPY ["src/Gateway.Application/Gateway.Application.csproj", "src/Gateway.Application/"]
COPY ["src/Gateway.Domain/Gateway.Domain.csproj", "src/Gateway.Domain/"]
COPY ["src/Gateway.Presentation/Gateway.Presentation.csproj", "src/Gateway.Presentation/"]
RUN dotnet restore "./src/Gateway.AppHost/Gateway.AppHost.csproj"
COPY . .
WORKDIR "/src/src/Gateway.AppHost"
RUN dotnet build "./Gateway.AppHost.csproj" -c $BUILD_CONFIGURATION -o /app/build

# This stage is used to publish the service project to be copied to the final stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Gateway.AppHost.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# This stage is used in production or when running from VS in regular mode (Default when not using the Debug configuration)
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Gateway.AppHost.dll"]