# See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

# This stage is used when running from VS in fast mode (Default for Debug configuration)
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080

# This stage is used to build the service project
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["services/notification-service/src/NotificationService.Api/NotificationService.Api.csproj", "services/notification-service/src/NotificationService.Api/"]
COPY ["services/notification-service/src/NotificationService.Infrastructure/NotificationService.Infrastructure.csproj", "services/notification-service/src/NotificationService.Infrastructure/"]
COPY ["services/notification-service/src/NotificationService.Application/NotificationService.Application.csproj", "services/notification-service/src/NotificationService.Application/"]
COPY ["services/notification-service/src/NotificationService.Domain/NotificationService.Domain.csproj", "services/notification-service/src/NotificationService.Domain/"]
COPY ["contracts/EventPlatform.Contracts/EventPlatform.Contracts.csproj", "contracts/EventPlatform.Contracts/"]
RUN dotnet restore "./services/notification-service/src/NotificationService.Api/NotificationService.Api.csproj"
COPY . .
WORKDIR "/src/services/notification-service/src/NotificationService.Api"
RUN dotnet build "./NotificationService.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

# This stage is used to publish the service project to be copied to the final stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./NotificationService.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# This stage is used in production or when running from VS in regular mode (Default when not using the Debug configuration)
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
COPY ["db/notification-service/*.sql", "/app/seed/"]
ENTRYPOINT ["dotnet", "NotificationService.Api.dll"]
