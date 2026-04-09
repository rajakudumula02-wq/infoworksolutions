# Deployment Checklist — Member SMS Campaign API

## 1. Azure DevOps Setup

- [ ] Create Azure DevOps project (or use existing one)
- [ ] Push repo to Azure Repos (or connect to GitHub repo)
- [ ] Create a new pipeline pointing to `MemberSmsCampaign/azure-pipelines.yml`
- [ ] Create `production` environment in Pipelines → Environments
- [ ] Configure approval gates on the `production` environment (recommended)

## 2. Azure Resource Provisioning

- [ ] Create Azure Resource Group (e.g. `rg-member-sms-campaign`)
- [ ] Create Azure App Service Plan (Linux, .NET 10)
- [ ] Create Azure Web App (Linux, .NET 10 runtime)
- [ ] Note the Web App name and update `webAppName` in `azure-pipelines.yml`
- [ ] Create Azure SQL Database for campaign data
- [ ] Create Azure Communication Services resource for SMS delivery
- [ ] Note the ACS connection string and sender phone number

## 3. Service Connection

- [ ] In Azure DevOps: Project Settings → Service connections → New → Azure Resource Manager
- [ ] Choose "Service principal (automatic)" or "Workload Identity federation"
- [ ] Name it `MemberSmsCampaign-ServiceConnection` (must match `azure-pipelines.yml`)
- [ ] Grant the service principal "Contributor" role on the resource group

## 4. Database Setup

- [ ] Run `sql/001_initial_schema.sql` against the Azure SQL Database
- [ ] Verify all 6 tables created: `members`, `coverages`, `campaigns`, `campaign_runs`, `manual_sms_logs`, `delivery_records`
- [ ] Verify all indexes created
- [ ] Configure firewall rules to allow App Service access to SQL Server

## 5. App Service Configuration

- [ ] Set the following Application Settings:

| Setting | Value |
|---|---|
| `ConnectionStrings__DefaultConnection` | (Azure SQL connection string) |
| `AzureCommunicationServices__ConnectionString` | (ACS connection string) |
| `AzureCommunicationServices__FromNumber` | (ACS sender phone number, e.g. `+1234567890`) |
| `Scheduler__Enabled` | `true` |
| `Scheduler__IntervalSeconds` | `60` |
| `ASPNETCORE_ENVIRONMENT` | `Production` |

- [ ] Enable "Always On" to keep the scheduler running
- [ ] Configure HTTPS Only = On
- [ ] Set minimum TLS version to 1.2
- [ ] Configure health check path to `/health`

## 6. Azure Application Gateway Configuration

### 6.1 Network Prerequisites
- [ ] Create or use existing VNet (e.g. `vnet-sms-campaign`, address space `10.2.0.0/16`)
- [ ] Create a dedicated subnet for Application Gateway (e.g. `snet-appgw`, `10.2.1.0/24`)
- [ ] Create a separate subnet for App Service VNet integration (e.g. `snet-app`, `10.2.2.0/24`)
- [ ] Enable VNet integration on the App Service using `snet-app`
- [ ] Set App Service access restriction to allow traffic only from the AppGw subnet

### 6.2 SSL/TLS Certificate
- [ ] Obtain SSL certificate for your domain (e.g. `sms.campaign.example.com`)
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
- [ ] Create backend pool targeting the App Service FQDN
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
| Path | `/health` |
| Host | Pick from backend HTTP settings |
| Interval | 30 seconds |
| Timeout | 30 seconds |
| Unhealthy threshold | 3 |
| Match status codes | `200` |

### 6.6 WAF Configuration (WAF_v2 SKU)
- [ ] Enable WAF in "Prevention" mode
- [ ] Use OWASP 3.2 managed rule set
- [ ] Set max request body size to 1 MB
- [ ] Enable bot protection rule set

## 7. Azure Communication Services Setup

- [ ] Verify ACS resource is provisioned
- [ ] Purchase or port a phone number for SMS sending
- [ ] Verify the phone number supports SMS (check capabilities)
- [ ] Test sending a single SMS from the Azure portal to confirm the number works
- [ ] Note the connection string and from number for App Service settings

## 8. Security

- [ ] Store connection strings and ACS credentials in Azure Key Vault
- [ ] Enable Key Vault references in App Service settings
- [ ] Enable Managed Identity on the App Service
- [ ] Configure CORS to allow only trusted frontend origins
- [ ] Review HIPAA compliance requirements for member phone data
- [ ] Enable audit logging for all SMS sends
- [ ] Ensure member phone numbers are encrypted at rest in SQL

## 9. Pre-Deployment Verification

- [ ] Run `dotnet build` locally — no errors
- [ ] Run `dotnet test` locally — all 46 tests pass
- [ ] Verify `appsettings.json` does not contain production secrets
- [ ] Confirm `azure-pipelines.yml` trigger paths are correct
- [ ] Verify .NET version in pipeline matches App Service runtime

## 10. First Pipeline Run

- [ ] Trigger pipeline (push to `main` or manual run)
- [ ] Verify CI stage: restore, build, and all tests pass
- [ ] Verify build artifact is published
- [ ] Approve deployment in the `production` environment (if gates configured)
- [ ] Verify CD stage: deployment succeeds without errors

## 11. Post-Deployment Smoke Test

- [ ] Hit `GET /health` — expect `200 OK`
- [ ] `POST /members` — create a test member with phone number
- [ ] `POST /coverages` — create active coverage for the test member
- [ ] `POST /campaigns` — create a welcome campaign
- [ ] `PUT /campaigns/{id}/schedule` — schedule for a future time
- [ ] `POST /sms/send` — send a manual single SMS to the test member
- [ ] Verify SMS received on the test phone number
- [ ] `POST /sms/bulk` — send bulk SMS to active coverage segment
- [ ] Check campaign run report for delivery statistics

## 12. Monitoring & Logging

- [ ] Enable Application Insights on the App Service
- [ ] Configure log streaming or diagnostic settings
- [ ] Set up alerts for 5xx error rate, response time > 3s, and CPU > 80%
- [ ] Monitor SMS delivery failure rate via ACS metrics
- [ ] Set up alert for scheduler failures (campaigns stuck in "scheduled" status)
- [ ] Verify campaign execution logs appear

## 13. Production Hardening

- [ ] Configure auto-scaling rules on the App Service Plan
- [ ] Enable Azure SQL geo-replication for disaster recovery
- [ ] Set up Azure Backup for the SQL database
- [ ] Document rollback procedure (redeploy previous artifact)
- [ ] Configure rate limiting for SMS sends (avoid ACS throttling)
- [ ] Add SMS opt-out/consent tracking (TCPA compliance)
- [ ] Implement member phone number validation before SMS send
