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
COPY ["services/event-service/src/EventService.Api/EventService.Api.csproj", "services/event-service/src/EventService.Api/"]
COPY ["services/event-service/src/EventService.Infrastructure/EventService.Infrastructure.csproj", "services/event-service/src/EventService.Infrastructure/"]
COPY ["services/event-service/src/EventService.Application/EventService.Application.csproj", "services/event-service/src/EventService.Application/"]
COPY ["services/event-service/src/EventService.Domain/EventService.Domain.csproj", "services/event-service/src/EventService.Domain/"]
COPY ["contracts/EventPlatform.Contracts/EventPlatform.Contracts.csproj", "contracts/EventPlatform.Contracts/"]
RUN dotnet restore "./services/event-service/src/EventService.Api/EventService.Api.csproj"
COPY . .
WORKDIR "/src/services/event-service/src/EventService.Api"
RUN dotnet build "./EventService.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

# This stage is used to publish the service project to be copied to the final stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./EventService.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# This stage is used in production or when running from VS in regular mode (Default when not using the Debug configuration)
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
COPY ["db/event-service/*.sql", "/app/seed/"]
ENTRYPOINT ["dotnet", "EventService.Api.dll"]
