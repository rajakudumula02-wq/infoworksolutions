# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY HealthcareFhirApi.sln .
COPY src/HealthcareFhirApi.Api/HealthcareFhirApi.Api.csproj src/HealthcareFhirApi.Api/
COPY src/HealthcareFhirApi.Core/HealthcareFhirApi.Core.csproj src/HealthcareFhirApi.Core/
COPY src/HealthcareFhirApi.Infrastructure/HealthcareFhirApi.Infrastructure.csproj src/HealthcareFhirApi.Infrastructure/
COPY Directory.Build.props .
RUN dotnet restore

COPY . .
RUN dotnet publish src/HealthcareFhirApi.Api/HealthcareFhirApi.Api.csproj -c Release -o /app/publish --no-restore

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "HealthcareFhirApi.Api.dll"]
