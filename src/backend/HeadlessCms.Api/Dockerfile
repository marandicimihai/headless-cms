FROM mcr.microsoft.com/dotnet/sdk:10.0.401 AS build
WORKDIR /source
COPY src/backend/HeadlessCms.Api/HeadlessCms.Api.csproj src/backend/HeadlessCms.Api/
RUN dotnet restore src/backend/HeadlessCms.Api/HeadlessCms.Api.csproj
COPY src/backend/HeadlessCms.Api/ src/backend/HeadlessCms.Api/
RUN dotnet publish src/backend/HeadlessCms.Api/HeadlessCms.Api.csproj -c Release --no-restore -o /out /p:UseAppHost=false /p:DebugType=None \
    && rm -f /out/appsettings.Development.json /out/appsettings.Testing.json

FROM mcr.microsoft.com/dotnet/aspnet:10.0.9
RUN apt-get update && apt-get install --yes --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /out/ ./
ENV ASPNETCORE_URLS=http://+:8080 ASPNETCORE_ENVIRONMENT=Production
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "HeadlessCms.Api.dll"]
