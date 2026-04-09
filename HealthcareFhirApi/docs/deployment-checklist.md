# Deployment Checklist — Healthcare FHIR API

## 1. Azure DevOps Setup

- [ ] Create Azure DevOps project (or use existing one)
- [ ] Push repo to Azure Repos (or connect to GitHub repo)
- [ ] Create a new pipeline pointing to `HealthcareFhirApi/azure-pipelines.yml`
- [ ] Create `production` environment in Pipelines → Environments
- [ ] Configure approval gates on the `production` environment (recommended)

## 2. Azure Resource Provisioning

- [ ] Create Azure Resource Group (e.g. `rg-healthcare-fhir-api`)
- [ ] Create Azure App Service Plan (Linux, .NET 10)
- [ ] Create Azure Web App (Linux, .NET 10 runtime)
- [ ] Note the Web App name and update `webAppName` in `azure-pipelines.yml`
- [ ] Create Azure SQL Server and database for FHIR data
- [ ] Create Azure Cache for Redis instance
- [ ] Note connection strings for SQL and Redis

## 3. Service Connection

- [ ] In Azure DevOps: Project Settings → Service connections → New → Azure Resource Manager
- [ ] Choose "Service principal (automatic)" or "Workload Identity federation"
- [ ] Name it `HealthcareFhirApi-ServiceConnection` (must match `azure-pipelines.yml`)
- [ ] Grant the service principal "Contributor" role on the resource group

## 4. Database Setup

- [ ] Run `sql/FhirDb_Performance.sql` against the Azure SQL database
- [ ] Apply Entity Framework migrations (if applicable)
- [ ] Verify database connectivity from the App Service
- [ ] Configure firewall rules to allow App Service access to SQL Server
- [ ] Enable Azure AD authentication for SQL (recommended)

## 5. App Service Configuration

- [ ] Set the following Application Settings in the Azure Web App:

| Setting | Value |
|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `ConnectionStrings__FhirDb` | (Azure SQL connection string) |
| `ConnectionStrings__Redis` | (Azure Redis connection string) |
| `ApiKey__Secret` | (strong random API key for auth) |
| `APPLICATIONINSIGHTS_CONNECTION_STRING` | (from App Insights resource) |

- [ ] Enable "Always On" to prevent cold starts
- [ ] Configure HTTPS Only = On
- [ ] Set minimum TLS version to 1.2
- [ ] Configure health check path to `/health` (if endpoint exists) or `/metadata`

## 6. Azure Application Gateway Configuration

### 6.1 Network Prerequisites
- [ ] Create or use existing VNet (e.g. `vnet-fhir-api`, address space `10.1.0.0/16`)
- [ ] Create a dedicated subnet for Application Gateway (e.g. `snet-appgw`, `10.1.1.0/24`)
- [ ] Create a separate subnet for App Service VNet integration (e.g. `snet-app`, `10.1.2.0/24`)
- [ ] Enable VNet integration on the App Service using `snet-app`
- [ ] Set App Service access restriction to allow traffic only from the AppGw subnet

### 6.2 SSL/TLS Certificate
- [ ] Obtain SSL certificate for your domain (e.g. `fhir.api.example.com`)
- [ ] Upload PFX certificate to Azure Key Vault
- [ ] Create a User-Assigned Managed Identity for Application Gateway
- [ ] Grant the managed identity "Key Vault Secrets User" role on the Key Vault

### 6.3 Provision Application Gateway
- [ ] Create Application Gateway v2 (WAF_v2 SKU recommended for healthcare)
- [ ] Select the `snet-appgw` subnet
- [ ] Assign a public IP (Static, Standard SKU) as the frontend IP
- [ ] Configure DNS A record pointing your domain to the public IP

### 6.4 Frontend Configuration
- [ ] Add HTTPS listener on port 443 with the SSL certificate
- [ ] Add HTTP listener on port 80 (for redirect only)
- [ ] Create a routing rule to redirect HTTP → HTTPS (permanent 301)

### 6.5 Backend Pool & Settings
- [ ] Create backend pool targeting the App Service FQDN (e.g. `fhirapi-prod-api.azurewebsites.net`)
- [ ] Create backend HTTP settings:

| Setting | Value |
|---|---|
| Protocol | HTTPS |
| Port | 443 |
| Use well-known CA certificate | Yes |
| Override with new host name | Yes — pick from backend target |
| Request timeout | 120 seconds |
| Cookie-based affinity | Disabled |

- [ ] Create a custom health probe:

| Setting | Value |
|---|---|
| Protocol | HTTPS |
| Path | `/metadata` |
| Host | Pick from backend HTTP settings |
| Interval | 30 seconds |
| Timeout | 30 seconds |
| Unhealthy threshold | 3 |
| Match status codes | `200` |

### 6.6 WAF Configuration (WAF_v2 SKU)
- [ ] Enable WAF in "Prevention" mode
- [ ] Use OWASP 3.2 managed rule set
- [ ] Add exclusion for FHIR JSON payloads that may trigger false positives on body inspection
- [ ] Set max request body size to 8 MB
- [ ] Enable bot protection rule set
- [ ] Review and tune WAF logs after initial deployment

### 6.7 Post-Configuration Verification
- [ ] Verify Application Gateway health status shows "Healthy" for the backend pool
- [ ] Test `https://fhir.api.example.com/metadata` returns the FHIR CapabilityStatement
- [ ] Confirm HTTP → HTTPS redirect works
- [ ] Verify the App Service is not directly accessible (only via Application Gateway)

## 7. Security & Compliance

- [ ] Store connection strings and API keys in Azure Key Vault
- [ ] Enable Key Vault references in App Service settings
- [ ] Enable Managed Identity on the App Service
- [ ] Configure CORS to allow only trusted frontend origins
- [ ] Enable Azure Defender for App Service
- [ ] Enable Azure Defender for SQL
- [ ] Review HIPAA compliance requirements for healthcare data
- [ ] Enable audit logging for all FHIR resource access
- [ ] Configure data encryption at rest (Azure SQL TDE is on by default)
- [ ] Enable Azure SQL Advanced Threat Protection

## 8. Pre-Deployment Verification

- [ ] Run `dotnet build` locally — no errors
- [ ] Run `dotnet publish` locally — output is clean
- [ ] Verify `appsettings.json` does not contain production secrets
- [ ] Confirm `azure-pipelines.yml` trigger paths are correct
- [ ] Verify .NET version in pipeline matches App Service runtime

## 9. First Pipeline Run

- [ ] Trigger pipeline (push to `main` or manual run)
- [ ] Verify CI stage: restore, build, and publish succeed
- [ ] Verify build artifact is published
- [ ] Approve deployment in the `production` environment (if gates configured)
- [ ] Verify CD stage: deployment succeeds without errors

## 10. Post-Deployment Smoke Test

- [ ] Hit `GET /metadata` — expect FHIR CapabilityStatement JSON
- [ ] Hit `GET /Patient` without auth — expect `401`
- [ ] Authenticate and hit `GET /Patient` — expect `200` with FHIR Bundle
- [ ] Test `POST /Patient` with a valid FHIR Patient resource — expect `201`
- [ ] Verify SQL database has the new record
- [ ] Test Redis caching is working (check response headers or logs)

## 11. Monitoring & Logging

- [ ] Enable Application Insights on the App Service
- [ ] Configure log streaming or diagnostic settings
- [ ] Set up alerts for 5xx error rate, response time > 3s, and CPU > 80%
- [ ] Enable SQL Database auditing
- [ ] Configure Redis monitoring metrics
- [ ] Set up availability tests in Application Insights for `/metadata`

## 12. Production Hardening

- [ ] Configure auto-scaling rules on the App Service Plan
- [ ] Enable Azure SQL geo-replication for disaster recovery
- [ ] Configure Redis persistence and failover
- [ ] Set up Azure Backup for the SQL database
- [ ] Document rollback procedure (redeploy previous artifact)
- [ ] Configure rate limiting via Application Gateway or middleware
- [ ] Review and enable all relevant FHIR compliance audit trails
