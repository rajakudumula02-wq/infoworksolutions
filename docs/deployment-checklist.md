# Deployment Checklist — Dental Image Score Analysis API

## 1. Azure DevOps Setup

- [ ] Create Azure DevOps project (or use existing one)
- [ ] Push repo to Azure Repos (or connect to GitHub repo)
- [ ] Create a new pipeline pointing to `azure-pipelines.yml`
- [ ] Create `production` environment in Pipelines → Environments
- [ ] Configure approval gates on the `production` environment (optional but recommended)

## 2. Azure Resource Provisioning

- [ ] Create Azure Resource Group (e.g. `rg-dental-image-api`)
- [ ] Create Azure App Service Plan (Linux, Node 22 LTS)
- [ ] Create Azure Web App (Linux, Node 22 LTS runtime)
- [ ] Note the Web App name and update `webAppName` in `azure-pipelines.yml`
- [ ] Create Azure OpenAI resource (or use existing AI Foundry deployment)
- [ ] Deploy GPT-4o model to Azure OpenAI (note deployment name)

## 3. Service Connection

- [ ] In Azure DevOps: Project Settings → Service connections → New → Azure Resource Manager
- [ ] Choose "Service principal (automatic)" or "Workload Identity federation"
- [ ] Name it `DentalImageAnalysis-ServiceConnection` (must match `azure-pipelines.yml`)
- [ ] Grant the service principal "Contributor" role on the resource group

## 4. App Service Configuration

- [ ] Set the following Application Settings in the Azure Web App:

| Setting | Value |
|---|---|
| `PORT` | `8080` |
| `NODE_ENV` | `production` |
| `JWT_SECRET` | (strong random secret, 32+ chars) |
| `AZURE_OPENAI_ENDPOINT` | `https://<your-resource>.openai.azure.com/` |
| `AZURE_OPENAI_API_KEY` | (API key from Azure OpenAI resource) |
| `AZURE_OPENAI_DEPLOYMENT` | (deployment name, e.g. `gpt-4o`) |
| `AZURE_OPENAI_API_VERSION` | `2024-02-15-preview` |

- [ ] Enable "Always On" to keep the worker loop running
- [ ] Set startup command: `node dist/index.js`
- [ ] Configure HTTPS Only = On
- [ ] Set minimum TLS version to 1.2

## 5. Security

- [ ] Store `JWT_SECRET` and `AZURE_OPENAI_API_KEY` as Azure Key Vault secrets (recommended)
- [ ] Enable Key Vault reference in App Service settings if using Key Vault
- [ ] Enable Managed Identity on the App Service
- [ ] Restrict CORS origins to your frontend domain
- [ ] Enable Azure App Service authentication if needed (beyond JWT)
- [ ] Review network access rules — consider VNet integration or IP restrictions

## 6. Azure Application Gateway Configuration

### 6.1 Network Prerequisites
- [ ] Create or use existing VNet (e.g. `vnet-dental-api`, address space `10.0.0.0/16`)
- [ ] Create a dedicated subnet for Application Gateway (e.g. `snet-appgw`, `10.0.1.0/24`)
- [ ] Create a separate subnet for App Service VNet integration (e.g. `snet-app`, `10.0.2.0/24`)
- [ ] Enable VNet integration on the App Service using `snet-app`
- [ ] Set App Service access restriction to allow traffic only from the AppGw subnet

### 6.2 SSL/TLS Certificate
- [ ] Obtain SSL certificate for your domain (e.g. `api.dental.example.com`)
- [ ] Upload PFX certificate to Azure Key Vault
- [ ] Create a User-Assigned Managed Identity for Application Gateway
- [ ] Grant the managed identity "Key Vault Secrets User" role on the Key Vault
- [ ] Or upload the PFX directly to Application Gateway (less recommended)

### 6.3 Provision Application Gateway
- [ ] Create Application Gateway v2 (Standard_v2 or WAF_v2 SKU)
- [ ] Select the `snet-appgw` subnet
- [ ] Assign a public IP (Static, Standard SKU) as the frontend IP
- [ ] Configure DNS A record pointing your domain to the public IP

### 6.4 Frontend Configuration
- [ ] Add HTTPS listener on port 443 with the SSL certificate
- [ ] Add HTTP listener on port 80 (for redirect only)
- [ ] Create a routing rule to redirect HTTP → HTTPS (permanent 301)

### 6.5 Backend Pool & Settings
- [ ] Create backend pool targeting the App Service FQDN (e.g. `dental-image-api.azurewebsites.net`)
- [ ] Create backend HTTP settings:

| Setting | Value |
|---|---|
| Protocol | HTTPS |
| Port | 443 |
| Use well-known CA certificate | Yes |
| Override with new host name | Yes — pick from backend target |
| Request timeout | 60 seconds |
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

### 6.6 Routing Rules
- [ ] Create routing rule for HTTPS listener → backend pool with the HTTP settings above
- [ ] Set priority (lower number = higher priority)
- [ ] Optionally add path-based routing if serving multiple APIs behind the same gateway

### 6.7 WAF Configuration (if using WAF_v2 SKU)
- [ ] Enable WAF in "Prevention" mode
- [ ] Use OWASP 3.2 managed rule set
- [ ] Add exclusion for `RequestBodyCheck` on `POST /analyses` (multipart file upload can trigger false positives)
- [ ] Set max request body size to 20 MB (matches the API's file size limit)
- [ ] Enable bot protection rule set
- [ ] Review and tune WAF logs after initial deployment

### 6.8 Post-Configuration Verification
- [ ] Verify Application Gateway health status shows "Healthy" for the backend pool
- [ ] Test `https://api.dental.example.com/health` returns `{ "status": "ok" }`
- [ ] Confirm HTTP → HTTPS redirect works
- [ ] Verify the App Service is not directly accessible (only via Application Gateway)

## 7. Pre-Deployment Verification

- [ ] Run `npm test` locally — all 90 tests pass
- [ ] Run `npm run build` locally — no TypeScript errors
- [ ] Verify `.env` is in `.gitignore` (never commit secrets)
- [ ] Confirm `azure-pipelines.yml` trigger paths are correct
- [ ] Verify Node.js version in pipeline matches App Service runtime

## 8. First Pipeline Run

- [ ] Trigger pipeline (push to `main` or manual run)
- [ ] Verify CI stage: type check passes, all tests pass on Node 20 and 22
- [ ] Verify build artifact is published
- [ ] Approve deployment in the `production` environment (if gates configured)
- [ ] Verify CD stage: deployment succeeds without errors

## 9. Post-Deployment Smoke Test

- [ ] Hit `GET /health` — expect `{ "status": "ok" }`
- [ ] Hit `GET /analyses` without auth — expect `401`
- [ ] Generate a test JWT and hit `POST /analyses` with a valid PNG — expect `202`
- [ ] Hit `GET /analyses/{analysisId}` — expect `202` (processing) then `200` (complete)
- [ ] Verify Azure OpenAI integration works (check `/debug` endpoint for env status)

## 10. Monitoring & Logging

- [ ] Enable Application Insights on the App Service
- [ ] Configure log streaming or diagnostic settings
- [ ] Set up alerts for 5xx error rate, response time > 3s, and CPU > 80%
- [ ] Verify worker loop logs appear (`[worker] Analysis complete: ...`)

## 11. Production Hardening

- [ ] Replace in-memory storage with persistent database (Azure Cosmos DB or PostgreSQL)
- [ ] Replace XOR encryption with AES-256-GCM using Azure Key Vault managed keys
- [ ] Add rate limiting configuration for production traffic
- [ ] Configure auto-scaling rules on the App Service Plan
- [ ] Set up backup and disaster recovery plan
- [ ] Document rollback procedure (redeploy previous artifact)
