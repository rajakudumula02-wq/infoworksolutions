# Implementation Plan: Member SMS Campaign

## Overview

Build a completely standalone TypeScript/Node.js service in `MemberSmsCampaign/` at the workspace root. The service uses Express.js for REST APIs, SQL Server for persistence, node-cron for scheduling, and Azure Communication Services for SMS delivery. It manages its own member and coverage data via REST APIs — no external FHIR or healthcare API dependencies.

## Tasks

- [x] 1. Initialize project and set up core infrastructure
  - [x] 1.1 Scaffold the MemberSmsCampaign project
    - Create `MemberSmsCampaign/` directory at workspace root
    - Initialize `package.json` with `npm init`
    - Install dependencies: `express`, `mssql`, `node-cron`, `@azure/communication-sms`, `uuid`, `dotenv`
    - Install dev dependencies: `typescript`, `ts-jest`, `jest`, `@types/express`, `@types/node`, `@types/node-cron`, `@types/uuid`, `fast-check`, `supertest`, `@types/supertest`
    - Create `tsconfig.json` with strict mode, ES2020 target, outDir `dist/`
    - Create `jest.config.ts` configured with `ts-jest`
    - Create `.env.example` with placeholders for `DB_SERVER`, `DB_DATABASE`, `DB_USER`, `DB_PASSWORD`, `ACS_CONNECTION_STRING`, `ACS_FROM_NUMBER`, `PORT`
    - _Requirements: All (project foundation)_

  - [x] 1.2 Create database schema migration file
    - Create `MemberSmsCampaign/src/db/migrations/001_initial_schema.sql` with the full schema from the design: `members`, `coverages`, `campaigns`, `campaign_runs`, `manual_sms_logs`, `delivery_records` tables with all constraints and indexes
    - Create `MemberSmsCampaign/src/db/connection.ts` with a SQL Server connection pool using `mssql`
    - Create `MemberSmsCampaign/src/db/migrate.ts` script to run migrations
    - _Requirements: All (data layer foundation)_

  - [x] 1.3 Define TypeScript interfaces and types
    - Create `MemberSmsCampaign/src/types/member.ts` with `Member`, `Coverage`, `CreateMemberInput`, `UpdateMemberInput`, `CreateCoverageInput`, `UpdateCoverageInput`, `PaginatedResult`, `ImportResult` interfaces
    - Create `MemberSmsCampaign/src/types/campaign.ts` with `Campaign`, `CampaignType`, `CampaignStatus`, `CampaignRun`, `DeliveryRecord`, `DeliveryStatus`, `ManualSmsLog` interfaces
    - Create `MemberSmsCampaign/src/types/sms.ts` with `SmsDeliveryResult`, `ManualSingleSmsInput`, `ManualBulkSmsInput`, `BulkSmsReport`, `ManualSmsResult` interfaces
    - Create `MemberSmsCampaign/src/types/api.ts` with `CreateCampaignInput`, `ValidationError`, `ApiErrorResponse` interfaces
    - _Requirements: 1.1, 1.2, 1.4, 1.5, 12.1, 13.1_

- [x] 2. Implement campaign CRUD and validation
  - [x] 2.1 Implement campaign repository
    - Create `MemberSmsCampaign/src/repositories/campaignRepository.ts`
    - Implement `create`, `findById`, `findAll`, `updateStatus`, `updateSchedule` methods using `mssql` queries
    - _Requirements: 1.1, 7.1, 7.2_

  - [x] 2.2 Implement campaign validation logic
    - Create `MemberSmsCampaign/src/services/validation.ts`
    - Validate required fields (name, type, messageTemplate) — return specific missing field names on failure
    - Validate campaign type is one of "welcome", "referral", "utilization", "holiday"
    - Validate message template length is 1–160 characters
    - _Requirements: 1.2, 1.3, 1.4, 1.5_

  - [x] 2.3 Implement CampaignService — create and list
    - Create `MemberSmsCampaign/src/services/campaignService.ts`
    - Implement `createCampaign()`: validate input, persist with status "draft"
    - Implement `getCampaign()` and `listCampaigns()`
    - _Requirements: 1.1, 7.1, 7.2_

  - [ ]* 2.4 Write property tests for campaign creation and validation
    - **Property 1: Campaign creation produces draft status**
    - **Property 2: Campaign type validation**
    - **Property 3: Required field validation rejects incomplete input**
    - **Property 4: Message length validation**
    - **Validates: Requirements 1.1, 1.2, 1.3, 1.4, 1.5**

- [x] 3. Implement campaign scheduling and cancellation
  - [x] 3.1 Implement scheduling logic in CampaignService
    - Add `scheduleCampaign(campaignId, scheduledAt)` to CampaignService
    - Validate future date, validate campaign is in "draft" or "scheduled" status
    - For Holiday campaigns, validate scheduled date is in November or December
    - Update status to "scheduled" and store the schedule date
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 11.1, 11.2, 11.3_

  - [x] 3.2 Implement cancellation logic in CampaignService
    - Add `cancelCampaign(campaignId)` to CampaignService
    - Validate campaign is in "draft" or "scheduled" status; reject "running" or "completed" with 409 error
    - Update status to "cancelled"
    - _Requirements: 7.3, 7.4_

  - [ ]* 3.3 Write property tests for scheduling and cancellation
    - **Property 5: Scheduling transitions to scheduled status**
    - **Property 6: Past date scheduling rejection**
    - **Property 7: Scheduling status guard**
    - **Property 17: Campaign cancellation transitions to cancelled**
    - **Property 18: Cancellation status guard**
    - **Property 26: Holiday campaign scheduling date restriction**
    - **Validates: Requirements 2.1, 2.2, 2.3, 2.4, 7.3, 7.4, 11.1, 11.2, 11.3**

- [x] 4. Checkpoint — Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 5. Implement member/coverage management and eligibility service
  - [x] 5.1 Implement member repository and service
    - Create `MemberSmsCampaign/src/repositories/memberRepository.ts`
    - Implement `create`, `findById`, `findAll`, `update`, `search` methods using `mssql`
    - Create `MemberSmsCampaign/src/services/memberService.ts`
    - Implement `createMember()`, `updateMember()`, `getMember()`, `listMembers()`, `searchMembers()`, `importMembers()`
    - _Requirements: 12.1, 12.2, 12.3, 12.4, 12.5, 12.6_

  - [x] 5.2 Implement coverage repository and service
    - Create `MemberSmsCampaign/src/repositories/coverageRepository.ts`
    - Implement `create`, `findByMemberId`, `findAllActive`, `update` methods using `mssql`
    - Create `MemberSmsCampaign/src/services/coverageService.ts`
    - Implement `createCoverage()`, `updateCoverage()`, `getCoveragesByMember()`, `listActiveCoverages()`
    - _Requirements: 13.1, 13.2, 13.3, 13.4, 13.5_

  - [x] 5.3 Implement eligibility service
    - Create `MemberSmsCampaign/src/services/eligibilityService.ts`
    - Implement `checkEligibility(memberId)`: query local coverages table, return eligible=true iff active coverage with current period
    - Implement `filterEligibleMembers(memberIds)`: batch eligibility check, return eligible list and excluded list with reasons
    - _Requirements: 3.1, 3.2, 3.3_

  - [ ]* 5.4 Write property test for eligibility determination
    - **Property 8: Eligibility determination correctness**
    - **Validates: Requirements 3.2, 3.3**

- [x] 6. Implement targeting service
  - [x] 6.1 Implement targeting rules
    - Create `MemberSmsCampaign/src/services/targetingService.ts`
    - Welcome: coverage period.start within last 30 days
    - Referral: coverage period.start older than 30 days AND active
    - Utilization: all members with active coverage
    - Holiday: all members with active coverage (same as Utilization)
    - _Requirements: 6.1, 6.2, 6.3, 6.4, 6.5_

  - [ ]* 6.2 Write property tests for targeting rules
    - **Property 13: Welcome campaign targeting**
    - **Property 14: Referral campaign targeting**
    - **Property 15: Utilization campaign targeting**
    - **Property 27: Holiday campaign targeting**
    - **Validates: Requirements 6.1, 6.2, 6.3, 6.5**

- [x] 7. Implement SMS provider client
  - [x] 7.1 Implement Azure Communication Services SMS client
    - Create `MemberSmsCampaign/src/clients/smsProviderClient.ts`
    - Implement `sendSms(to, message)` wrapping `@azure/communication-sms`
    - Add retry logic: up to 3 retries with exponential backoff on provider unreachable errors
    - Return `SmsDeliveryResult` with success/failure and messageId or failureReason
    - _Requirements: 5.1, 5.2, 5.3_

  - [ ]* 7.2 Write property test for delivery status
    - **Property 11: Delivery status matches provider outcome**
    - **Validates: Requirements 5.1, 5.2**

- [x] 8. Implement campaign execution engine
  - [x] 8.1 Implement campaign run repository
    - Create `MemberSmsCampaign/src/repositories/campaignRunRepository.ts`
    - Implement `create`, `findById`, `findByCampaignId`, `updateStatistics`, `complete` methods
    - Create `MemberSmsCampaign/src/repositories/deliveryRecordRepository.ts`
    - Implement `create`, `findByRunId`, `findByManualSmsId` methods
    - _Requirements: 4.2, 4.4, 5.4, 8.1, 8.2_

  - [x] 8.2 Implement campaign execution in CampaignService
    - Add `executeCampaignRun(campaignId)` to CampaignService
    - Transition campaign to "running", create CampaignRun record
    - Call TargetingService to resolve target members by campaign type
    - Call EligibilityService to filter eligible members
    - For each eligible member: look up member record, check phone number, send SMS via provider, record delivery status
    - Skip members without phone numbers with reason "no phone number on file"
    - On completion: transition campaign to "completed", update run statistics (sent + failed + skipped = total)
    - _Requirements: 4.2, 4.3, 4.4, 4.5, 5.1, 5.2, 5.4_

  - [ ]* 8.3 Write property tests for campaign execution
    - **Property 9: Campaign run status lifecycle**
    - **Property 10: Members without phone numbers are skipped**
    - **Property 12: Run statistics invariant**
    - **Validates: Requirements 4.2, 4.4, 4.5, 5.4**

- [x] 9. Implement scheduler
  - [x] 9.1 Implement node-cron scheduler
    - Create `MemberSmsCampaign/src/services/scheduler.ts`
    - Run a cron job every minute that queries campaigns with status "scheduled" and `scheduledAt <= now`
    - For each matched campaign, call `CampaignService.executeCampaignRun()`
    - Implement `start()` and `stop()` methods
    - _Requirements: 4.1_

- [x] 10. Checkpoint — Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 11. Implement manual SMS services
  - [x] 11.1 Implement manual single SMS
    - Create `MemberSmsCampaign/src/services/manualSmsService.ts`
    - Implement `sendSingle(input)`: validate message length (1–160), check eligibility, check phone number, send via provider, record in `manual_sms_logs` and `delivery_records`
    - Reject if member has no active coverage (error: "no active coverage")
    - Reject if member has no phone number (error: "no phone number on file")
    - _Requirements: 9.1, 9.2, 9.3, 9.4, 9.5, 9.6, 9.7_

  - [x] 11.2 Implement manual bulk SMS
    - Add `sendBulk(input)` to ManualSmsService
    - Accept list of member IDs or a segment filter (e.g., all active coverage)
    - Resolve segment to member list if segment filter provided
    - Filter eligible members, skip ineligible with reason "no active coverage"
    - Skip members without phone numbers with reason "no phone number on file"
    - Send to each eligible member, continue on individual failures
    - Record all delivery records, return summary report (sent + failed + skipped = total)
    - _Requirements: 10.1, 10.2, 10.3, 10.4, 10.5, 10.6, 10.7, 10.8, 10.9_

  - [ ]* 11.3 Write property tests for manual SMS
    - **Property 21: Manual single SMS eligibility and phone guard**
    - **Property 22: Bulk SMS eligibility filtering**
    - **Property 23: Bulk SMS report consistency**
    - **Property 24: Bulk SMS continues on individual failure**
    - **Property 25: Segment resolution correctness**
    - **Validates: Requirements 9.3, 9.4, 9.5, 10.4, 10.5, 10.8, 10.9, 10.2**

- [x] 12. Implement campaign run reporting
  - [x] 12.1 Implement run report logic
    - Add `getRunReport(campaignId, runId)` to CampaignService
    - Return total eligible, sent, failed, skipped counts
    - Return per-member delivery status breakdown with member ID, status, and reason
    - Ensure campaign detail endpoint includes runs in reverse chronological order
    - _Requirements: 8.1, 8.2, 8.3_

  - [ ]* 12.2 Write property tests for reporting
    - **Property 16: Campaign list and detail completeness**
    - **Property 19: Run report completeness**
    - **Property 20: Campaign runs listed in reverse chronological order**
    - **Validates: Requirements 7.1, 7.2, 8.1, 8.2, 8.3**

- [x] 13. Implement Express REST API layer
  - [x] 13.1 Set up Express app and middleware
    - Create `MemberSmsCampaign/src/app.ts` with Express app setup
    - Add JSON body parsing, error handling middleware
    - Create `MemberSmsCampaign/src/server.ts` as the entry point that starts the HTTP server and scheduler
    - _Requirements: All (API layer)_

  - [x] 13.2 Implement member and coverage routes
    - Create `MemberSmsCampaign/src/routes/memberRoutes.ts`
    - `POST /members` — create member
    - `GET /members` — list members (paginated)
    - `GET /members/:id` — get member with coverages
    - `PUT /members/:id` — update member
    - `GET /members/search` — search by name or phone
    - `POST /members/import` — bulk CSV import
    - Create `MemberSmsCampaign/src/routes/coverageRoutes.ts`
    - `POST /coverages` — create coverage
    - `GET /coverages?memberId=:id` — get coverages for member
    - `PUT /coverages/:id` — update coverage
    - `GET /coverages?status=active` — list active coverages
    - _Requirements: 12.1, 12.2, 12.3, 12.4, 12.5, 12.6, 13.1, 13.2, 13.3, 13.4, 13.5_

  - [x] 13.3 Implement campaign routes
    - Create `MemberSmsCampaign/src/routes/campaignRoutes.ts`
    - `POST /campaigns` — create campaign
    - `GET /campaigns` — list campaigns
    - `GET /campaigns/:id` — get campaign details with run history
    - `PUT /campaigns/:id/schedule` — schedule or reschedule
    - `POST /campaigns/:id/cancel` — cancel campaign
    - `GET /campaigns/:id/runs/:runId/report` — get run report
    - Return validation errors as HTTP 400, state transition errors as HTTP 409
    - _Requirements: 1.1, 1.3, 1.5, 2.1, 2.2, 2.4, 7.1, 7.2, 7.3, 7.4, 8.1, 8.2, 8.3_

  - [x] 13.4 Implement manual SMS routes
    - Create `MemberSmsCampaign/src/routes/smsRoutes.ts`
    - `POST /sms/send` — send manual single SMS
    - `POST /sms/bulk` — send manual bulk SMS
    - Return validation errors as HTTP 400, eligibility errors with appropriate status
    - _Requirements: 9.1, 9.2, 9.3, 9.4, 9.5, 10.1, 10.2, 10.3, 10.4, 10.5, 10.8_

- [ ] 14. Wire everything together and final integration
  - [x] 14.1 Set up dependency injection and wiring
    - Create `MemberSmsCampaign/src/container.ts` to instantiate and wire all services, repositories, and clients
    - Connect routes to services in `app.ts`
    - Start scheduler in `server.ts`
    - Ensure `.env` is loaded via `dotenv` at startup
    - _Requirements: All (integration)_

  - [ ]* 14.2 Write integration tests for API endpoints
    - Test member and coverage CRUD via HTTP
    - Test campaign CRUD lifecycle via HTTP (create → schedule → execute → report)
    - Test manual single SMS endpoint with mocked SMS client
    - Test manual bulk SMS endpoint with mocked SMS client
    - Test validation error responses (400) and state transition errors (409)
    - Test Holiday campaign scheduling restriction via HTTP
    - _Requirements: All_

- [ ] 15. Final checkpoint — Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document (27 properties)
- All external dependencies (Azure Communication Services, SQL Server) should be mocked in tests
- The project is completely standalone in `MemberSmsCampaign/` with its own member/coverage data — no external FHIR or healthcare API dependencies
