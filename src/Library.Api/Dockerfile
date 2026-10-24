# Build context: repository root.
#   API image (default, last stage): docker build -f src/Library.Api/Dockerfile .
#   Migrator image:                  docker build -f src/Library.Api/Dockerfile --target migrator .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY global.json Directory.Build.props Directory.Packages.props dotnet-tools.json ./
COPY src/Library.Domain/Library.Domain.csproj src/Library.Domain/
COPY src/Library.Api/Library.Api.csproj src/Library.Api/
RUN dotnet restore src/Library.Api/Library.Api.csproj

COPY src/ src/
RUN dotnet publish src/Library.Api/Library.Api.csproj -c Release --no-restore -o /app/publish /p:UseAppHost=false

FROM build AS bundle
RUN dotnet tool restore \
    && dotnet ef migrations bundle \
        --project src/Library.Api/Library.Api.csproj \
        --configuration Release \
        --self-contained -r linux-x64 \
        -o /app/efbundle

# Applies EF Core migrations; the connection string comes from ConnectionStrings__Postgres.
FROM mcr.microsoft.com/dotnet/runtime-deps:10.0 AS migrator
WORKDIR /app
COPY --from=bundle /app/efbundle .
USER $APP_UID
ENTRYPOINT ["./efbundle"]

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_RUNNING_IN_CONTAINER=true
EXPOSE 8080
COPY --from=build /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "Library.Api.dll"]
