# Requirements Document

## Introduction

The Member SMS Campaign application enables healthcare plan administrators to create, schedule, and send targeted SMS campaigns to members with active coverage. The application supports four campaign types: Welcome campaigns for new members, Referral campaigns to encourage member referrals, Encourage Utilization campaigns to promote use of covered benefits, and Holiday campaigns for seasonal greetings and health reminders. In addition to automated campaigns, the system supports manual sending of both single SMS messages to individual members and bulk SMS messages to multiple members or filtered segments. The system manages its own member and coverage data in a SQL Server database and verifies member eligibility based on active coverage status before sending messages.

## Glossary

- **Campaign_Service**: The core application service responsible for creating, scheduling, executing, and tracking SMS campaigns.
- **Member**: A healthcare plan participant stored in the application's members table, managed via the Member API.
- **Active_Coverage**: A coverage record in the application database with a status of "active" and a period that includes the current date, managed via the Coverage API.
- **Welcome_Campaign**: A campaign type targeting newly enrolled members to introduce them to their plan benefits.
- **Referral_Campaign**: A campaign type targeting existing members to encourage them to refer others to the healthcare plan.
- **Utilization_Campaign**: A campaign type targeting members who have active coverage to encourage them to use their covered benefits.
- **Holiday_Campaign**: A campaign type targeting all members with active coverage to send holiday greetings or seasonal health reminders.
- **SMS_Provider**: An external third-party service used to deliver SMS messages to member phone numbers.
- **Campaign_Schedule**: A date and time specification that defines when the Campaign_Service sends campaign messages.
- **Eligibility_Check**: The process of verifying that a Member has Active_Coverage by querying the application's coverage table.
- **Campaign_Run**: A single execution instance of a campaign that tracks the delivery status of all messages sent during that execution.
- **Manual_Single_SMS**: A one-off SMS message sent by a plan administrator to a specific Member identified by member ID, independent of any campaign.
- **Manual_Bulk_SMS**: An immediate SMS send initiated by a plan administrator targeting multiple Members specified by a list of member IDs or a filtered segment.
- **Member_Segment**: A filtered group of Members defined by criteria such as active coverage status, used for targeting Manual_Bulk_SMS sends.

## Requirements

### Requirement 1: Create Campaign

**User Story:** As a plan administrator, I want to create an SMS campaign with a specific type and message content, so that I can prepare targeted outreach to members.

#### Acceptance Criteria

1. WHEN a plan administrator submits a campaign creation request with a name, campaign type, and message template, THE Campaign_Service SHALL create a new campaign record with a status of "draft".
2. THE Campaign_Service SHALL support exactly four campaign types: Welcome_Campaign, Referral_Campaign, Utilization_Campaign, and Holiday_Campaign.
3. WHEN a campaign creation request is missing a required field (name, campaign type, or message template), THE Campaign_Service SHALL return a validation error specifying the missing field.
4. THE Campaign_Service SHALL enforce a maximum message template length of 160 characters to comply with single SMS segment limits.
5. IF a campaign creation request contains an unsupported campaign type, THEN THE Campaign_Service SHALL return a validation error indicating the allowed campaign types.

### Requirement 2: Schedule Campaign

**User Story:** As a plan administrator, I want to schedule a campaign for a future date and time, so that messages are sent at the optimal time for member engagement.

#### Acceptance Criteria

1. WHEN a plan administrator provides a valid future date and time for a draft campaign, THE Campaign_Service SHALL update the campaign status to "scheduled" and store the Campaign_Schedule.
2. IF a plan administrator provides a schedule date and time that is in the past, THEN THE Campaign_Service SHALL return a validation error indicating the schedule must be in the future.
3. WHEN a plan administrator updates the schedule of an already scheduled campaign, THE Campaign_Service SHALL replace the existing Campaign_Schedule with the new date and time.
4. IF a plan administrator attempts to schedule a campaign that is not in "draft" or "scheduled" status, THEN THE Campaign_Service SHALL return an error indicating only draft or scheduled campaigns can be scheduled.

### Requirement 3: Member Eligibility Verification

**User Story:** As a plan administrator, I want the system to verify member eligibility before sending messages, so that only members with active coverage receive campaign SMS messages.

#### Acceptance Criteria

1. WHEN a Campaign_Run begins, THE Campaign_Service SHALL query the coverage table to retrieve coverage records for each targeted Member.
2. THE Campaign_Service SHALL consider a Member eligible for a campaign only when the Member has at least one coverage record with status "active" and a period that includes the current date.
3. WHEN a Member does not have Active_Coverage, THE Campaign_Service SHALL exclude that Member from the Campaign_Run and record the exclusion reason as "no active coverage".
4. IF the database is unreachable during an Eligibility_Check, THEN THE Campaign_Service SHALL retry the query up to 3 times with exponential backoff before marking the Member as "eligibility check failed".

### Requirement 4: Campaign Execution

**User Story:** As a plan administrator, I want scheduled campaigns to execute automatically at the scheduled time, so that members receive timely SMS messages without manual intervention.

#### Acceptance Criteria

1. WHEN the current date and time matches a Campaign_Schedule, THE Campaign_Service SHALL initiate a Campaign_Run for the corresponding campaign.
2. WHEN a Campaign_Run is initiated, THE Campaign_Service SHALL update the campaign status to "running".
3. THE Campaign_Service SHALL send the campaign message template to each eligible Member via the SMS_Provider using the phone number from the Member record.
4. WHEN a Campaign_Run completes sending all messages, THE Campaign_Service SHALL update the campaign status to "completed" and record the completion timestamp.
5. IF a Member record does not contain a phone number, THEN THE Campaign_Service SHALL skip that Member and record the skip reason as "no phone number on file".

### Requirement 5: SMS Delivery

**User Story:** As a plan administrator, I want SMS messages delivered reliably to members, so that campaign outreach reaches the intended audience.

#### Acceptance Criteria

1. WHEN the Campaign_Service sends a message to the SMS_Provider, THE Campaign_Service SHALL record the delivery status as "sent" for that Member in the Campaign_Run.
2. IF the SMS_Provider returns a delivery failure for a Member message, THEN THE Campaign_Service SHALL record the delivery status as "failed" and store the failure reason.
3. IF the SMS_Provider is unreachable, THEN THE Campaign_Service SHALL retry the send request up to 3 times with exponential backoff before recording the delivery status as "failed" with reason "provider unreachable".
4. THE Campaign_Service SHALL track the total count of sent, failed, and skipped messages for each Campaign_Run.

### Requirement 6: Campaign Type Targeting Rules

**User Story:** As a plan administrator, I want each campaign type to target the appropriate member segment, so that messages are relevant to the recipients.

#### Acceptance Criteria

1. WHEN a Welcome_Campaign runs, THE Campaign_Service SHALL target only Members whose coverage record has a period start date within the last 30 days.
2. WHEN a Referral_Campaign runs, THE Campaign_Service SHALL target Members whose coverage record has a period start date older than 30 days and who have Active_Coverage.
3. WHEN a Utilization_Campaign runs, THE Campaign_Service SHALL target all Members with Active_Coverage.
4. THE Campaign_Service SHALL apply the campaign-type-specific targeting rules before performing the Eligibility_Check for each Member.
5. WHEN a Holiday_Campaign runs, THE Campaign_Service SHALL target all Members with Active_Coverage.

### Requirement 7: Campaign Management

**User Story:** As a plan administrator, I want to view and manage my campaigns, so that I can track campaign progress and make adjustments.

#### Acceptance Criteria

1. THE Campaign_Service SHALL provide a list of all campaigns with their current status, campaign type, and Campaign_Schedule.
2. WHEN a plan administrator requests details for a specific campaign, THE Campaign_Service SHALL return the campaign record including the message template, schedule, status, and Campaign_Run history.
3. WHEN a plan administrator cancels a scheduled campaign, THE Campaign_Service SHALL update the campaign status to "cancelled" and prevent the Campaign_Run from executing.
4. IF a plan administrator attempts to cancel a campaign that is currently in "running" or "completed" status, THEN THE Campaign_Service SHALL return an error indicating the campaign cannot be cancelled in its current status.

### Requirement 8: Campaign Run Reporting

**User Story:** As a plan administrator, I want to view delivery reports for each campaign run, so that I can measure campaign effectiveness.

#### Acceptance Criteria

1. WHEN a plan administrator requests a report for a Campaign_Run, THE Campaign_Service SHALL return the total number of eligible members, messages sent, messages failed, and members skipped.
2. THE Campaign_Service SHALL provide a per-member delivery status breakdown for each Campaign_Run including the Member identifier, delivery status, and failure or skip reason where applicable.
3. WHEN a campaign has multiple Campaign_Runs, THE Campaign_Service SHALL list all runs in reverse chronological order with their individual statistics.

### Requirement 9: Manual Single SMS

**User Story:** As a plan administrator, I want to send a one-off SMS to a specific member by member ID with a custom message, so that I can communicate directly with an individual member without creating a campaign.

#### Acceptance Criteria

1. WHEN a plan administrator submits a manual single SMS request with a member ID and a custom message, THE Campaign_Service SHALL send the SMS to the specified Member via the SMS_Provider.
2. THE Campaign_Service SHALL enforce a maximum custom message length of 160 characters for a Manual_Single_SMS.
3. WHEN a manual single SMS request is submitted, THE Campaign_Service SHALL perform an Eligibility_Check to verify the specified Member has Active_Coverage before sending the message.
4. IF the specified Member does not have Active_Coverage, THEN THE Campaign_Service SHALL reject the send request and return an error indicating the Member does not have active coverage.
5. IF the specified Member record does not contain a phone number, THEN THE Campaign_Service SHALL reject the send request and return an error indicating no phone number is on file for the Member.
6. WHEN a Manual_Single_SMS is sent, THE Campaign_Service SHALL record the send event including the member ID, message content, delivery status, and timestamp.
7. IF the SMS_Provider returns a delivery failure for a Manual_Single_SMS, THEN THE Campaign_Service SHALL record the delivery status as "failed" and store the failure reason.

### Requirement 10: Manual Bulk SMS

**User Story:** As a plan administrator, I want to send a bulk SMS to a list of specific members or a filtered segment with a custom message, so that I can reach multiple members immediately without creating a scheduled campaign.

#### Acceptance Criteria

1. WHEN a plan administrator submits a manual bulk SMS request with a list of member IDs and a custom message, THE Campaign_Service SHALL send the SMS to each specified Member via the SMS_Provider.
2. WHEN a plan administrator submits a manual bulk SMS request with a Member_Segment filter (such as all members with Active_Coverage) and a custom message, THE Campaign_Service SHALL resolve the segment to a list of matching Members and send the SMS to each.
3. THE Campaign_Service SHALL enforce a maximum custom message length of 160 characters for a Manual_Bulk_SMS.
4. WHEN a Manual_Bulk_SMS is initiated, THE Campaign_Service SHALL perform an Eligibility_Check for each targeted Member and send messages only to Members with Active_Coverage.
5. WHEN a Member in a Manual_Bulk_SMS does not have Active_Coverage, THE Campaign_Service SHALL exclude that Member and record the exclusion reason as "no active coverage".
6. IF a Member record does not contain a phone number, THEN THE Campaign_Service SHALL skip that Member and record the skip reason as "no phone number on file".
7. THE Campaign_Service SHALL send a Manual_Bulk_SMS immediately upon request and not store the request as a scheduled campaign.
8. WHEN a Manual_Bulk_SMS completes, THE Campaign_Service SHALL return a summary report including the total number of messages sent, failed, and skipped with reasons.
9. IF the SMS_Provider returns a delivery failure for a Member message during a Manual_Bulk_SMS, THEN THE Campaign_Service SHALL record the delivery status as "failed" for that Member and continue sending to the remaining Members.

### Requirement 11: Holiday Campaign Scheduling Restriction

**User Story:** As a plan administrator, I want Holiday campaigns to only be scheduled for dates in November or December, so that holiday messages are sent during the appropriate seasonal period.

#### Acceptance Criteria

1. WHEN a plan administrator schedules a Holiday_Campaign with a date in November (month 11) or December (month 12), THE Campaign_Service SHALL accept the schedule and update the campaign status to "scheduled".
2. IF a plan administrator attempts to schedule a Holiday_Campaign with a date outside November or December, THEN THE Campaign_Service SHALL return a validation error indicating Holiday campaigns can only be scheduled for dates in November or December.
3. WHEN a plan administrator reschedules an existing Holiday_Campaign, THE Campaign_Service SHALL validate that the new date is also in November or December before accepting the change.

### Requirement 12: Member Management API

**User Story:** As a plan administrator, I want to manage member records via API, so that the campaign service has the member data it needs to target and send SMS messages.

#### Acceptance Criteria

1. WHEN a plan administrator submits a member creation request with first name, last name, date of birth, and phone number, THE Campaign_Service SHALL create a new member record and return the member ID.
2. THE Campaign_Service SHALL support updating a member's phone number, name, and date of birth via a PUT request.
3. WHEN a plan administrator requests a member by ID, THE Campaign_Service SHALL return the full member record including name, date of birth, phone number, and associated coverage records.
4. THE Campaign_Service SHALL support listing all members with pagination (default page size 50, max 100).
5. WHEN a member creation request is missing a required field (first name, last name), THE Campaign_Service SHALL return a validation error specifying the missing field.
6. THE Campaign_Service SHALL support searching members by name or phone number.

### Requirement 13: Coverage Management API

**User Story:** As a plan administrator, I want to manage coverage records via API, so that the system can determine which members have active coverage for campaign targeting.

#### Acceptance Criteria

1. WHEN a plan administrator submits a coverage creation request with a member ID, plan name, status, period start date, and period end date, THE Campaign_Service SHALL create a new coverage record linked to the specified member.
2. THE Campaign_Service SHALL support updating a coverage record's status and period dates via a PUT request.
3. WHEN a plan administrator requests coverages for a specific member, THE Campaign_Service SHALL return all coverage records for that member.
4. THE Campaign_Service SHALL support listing all active coverages with pagination.
5. IF a coverage creation request references a member ID that does not exist, THEN THE Campaign_Service SHALL return a validation error indicating the member was not found.
6. THE Campaign_Service SHALL support bulk import of member and coverage records via a CSV upload endpoint.
