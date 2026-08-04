# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:8.0-bookworm-slim AS build
WORKDIR /src
COPY WebAPI/InventoryAPI.csproj WebAPI/
RUN dotnet restore WebAPI/InventoryAPI.csproj
COPY WebAPI/ WebAPI/
RUN dotnet publish WebAPI/InventoryAPI.csproj --configuration Release --output /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0-bookworm-slim AS final
WORKDIR /app
RUN apt-get update \
    && apt-get install --yes --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
COPY --from=build --chown=app:app /app/publish .
USER app
ENV DOTNET_EnableDiagnostics=0
EXPOSE 8080
ENTRYPOINT ["dotnet", "InventoryAPI.dll"]
