# Azure Deployment Guide — Healthcare FHIR API

## Prerequisites

- Azure CLI installed (`az --version`)
- Azure subscription with Owner/Contributor access
- .NET 10 SDK installed
- Logged in: `az login`

## Deployment Steps

Run these in order from PowerShell:

### 1. Create Resource Group
```powershell
az group create --name fhir-api-rg --location eastus --tags environment=production project=fhir-api
```

### 2. Create SQL Server + Database
```powershell
az sql server create --resource-group fhir-api-rg --name fhirapi-sql --location eastus --admin-user fhiradmin --admin-password "YourStrongPassword123!" --minimal-tls-version 1.2

az sql db create --resource-group fhir-api-rg --server fhirapi-sql --name FhirDb --service-objective S1 --max-size 250GB

az sql server firewall-rule create --resource-group fhir-api-rg --server fhirapi-sql --name AllowAzureServices --start-ip-address 0.0.0.0 --end-ip-address 0.0.0.0
```

### 3. Create Redis Cache
```powershell
az redis create --resource-group fhir-api-rg --name fhirapi-redis --location eastus --sku Basic --vm-size c0 --minimum-tls-version 1.2
```
Wait ~15 minutes for Redis to provision, then get the key:
```powershell
az redis list-keys --resource-group fhir-api-rg --name fhirapi-redis --query primaryKey --output tsv
```

### 4. Create App Service
```powershell
az appservice plan create --resource-group fhir-api-rg --name fhirapi-plan --location eastus --sku B2

az webapp create --resource-group fhir-api-rg --plan fhirapi-plan --name fhirapi-prod-api --runtime "dotnet:10"

az webapp update --resource-group fhir-api-rg --name fhirapi-prod-api --https-only true

az webapp config set --resource-group fhir-api-rg --name fhirapi-prod-api --always-on true --ftps-state Disabled --min-tls-version 1.2
```

### 5. Configure App Settings
Replace `<SQL_PASSWORD>`, `<REDIS_HOST>`, `<REDIS_PORT>`, `<REDIS_KEY>` with actual values:
```powershell
az webapp config appsettings set --resource-group fhir-api-rg --name fhirapi-prod-api --settings ConnectionStrings__FhirDb="Server=tcp:fhirapi-sql.database.windows.net,1433;Database=FhirDb;User ID=fhiradmin;Password=<SQL_PASSWORD>;Encrypt=true;TrustServerCertificate=false;" ConnectionStrings__Redis="<REDIS_HOST>:<REDIS_PORT>,password=<REDIS_KEY>,ssl=True,abortConnect=false" SmartAuth__Authority="https://login.microsoftonline.com/common/v2.0" SmartAuth__Audience="fhir-api" ASPNETCORE_ENVIRONMENT="Production"
```

### 6. Deploy the API
```powershell
dotnet publish src/HealthcareFhirApi.Api/HealthcareFhirApi.Api.csproj -c Release -o ./publish
Compress-Archive -Path ./publish/* -DestinationPath ./publish.zip -Force
az webapp deploy --resource-group fhir-api-rg --name fhirapi-prod-api --src-path ./publish.zip --type zip
```

### 7. Initialize Database
Connect to Azure SQL and run the SQL from `07-init-database.yml`:
```powershell
sqlcmd -S fhirapi-sql.database.windows.net -U fhiradmin -P "YourStrongPassword123!" -d FhirDb -i infra/07-init-database.sql
```

### 8. Test
```powershell
curl https://fhirapi-prod-api.azurewebsites.net/metadata
curl https://fhirapi-prod-api.azurewebsites.net/.well-known/smart-configuration
curl https://fhirapi-prod-api.azurewebsites.net/Patient -H "X-Api-Key: fhir_testkey123456"
```

## Estimated Monthly Cost

| Resource | Tier | ~Cost/month |
|---|---|---|
| App Service Plan | B2 | $55 |
| Azure SQL | S1 (250GB) | $30 |
| Azure Cache for Redis | Basic C0 | $16 |
| **Total** | | **~$101/month** |

Scale up as needed — S2/P1 for production workloads.
