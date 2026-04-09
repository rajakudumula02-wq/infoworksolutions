-- MemberSmsCampaign Initial Schema
-- SQL Server

CREATE TABLE members (
  id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
  first_name NVARCHAR(100) NOT NULL,
  last_name NVARCHAR(100) NOT NULL,
  date_of_birth DATE,
  phone_number NVARCHAR(20),
  created_at DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
  updated_at DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET()
);

CREATE TABLE coverages (
  id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
  member_id UNIQUEIDENTIFIER NOT NULL REFERENCES members(id),
  plan_name NVARCHAR(255) NOT NULL,
  status NVARCHAR(20) NOT NULL DEFAULT 'active' CHECK (status IN ('active', 'inactive', 'cancelled')),
  period_start DATE NOT NULL,
  period_end DATE,
  created_at DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
  updated_at DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET()
);

CREATE TABLE campaigns (
  id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
  name NVARCHAR(255) NOT NULL,
  type NVARCHAR(20) NOT NULL CHECK (type IN ('welcome', 'referral', 'utilization', 'holiday')),
  message_template NVARCHAR(160) NOT NULL,
  status NVARCHAR(20) NOT NULL DEFAULT 'draft' CHECK (status IN ('draft', 'scheduled', 'running', 'completed', 'cancelled')),
  scheduled_at DATETIMEOFFSET,
  created_at DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
  updated_at DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET()
);

CREATE TABLE campaign_runs (
  id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
  campaign_id UNIQUEIDENTIFIER NOT NULL REFERENCES campaigns(id),
  status NVARCHAR(20) NOT NULL DEFAULT 'running',
  total_eligible INT NOT NULL DEFAULT 0,
  total_sent INT NOT NULL DEFAULT 0,
  total_failed INT NOT NULL DEFAULT 0,
  total_skipped INT NOT NULL DEFAULT 0,
  started_at DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
  completed_at DATETIMEOFFSET
);

CREATE TABLE manual_sms_logs (
  id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
  type NVARCHAR(10) NOT NULL CHECK (type IN ('single', 'bulk')),
  message NVARCHAR(160) NOT NULL,
  total_sent INT NOT NULL DEFAULT 0,
  total_failed INT NOT NULL DEFAULT 0,
  total_skipped INT NOT NULL DEFAULT 0,
  created_at DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
  completed_at DATETIMEOFFSET
);

CREATE TABLE delivery_records (
  id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
  campaign_run_id UNIQUEIDENTIFIER REFERENCES campaign_runs(id),
  manual_sms_id UNIQUEIDENTIFIER REFERENCES manual_sms_logs(id),
  member_id NVARCHAR(255) NOT NULL,
  phone_number NVARCHAR(20),
  status NVARCHAR(20) NOT NULL CHECK (status IN ('sent', 'failed', 'skipped', 'excluded')),
  reason NVARCHAR(MAX),
  sent_at DATETIMEOFFSET,
  created_at DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET()
);

-- Indexes
CREATE INDEX idx_members_name ON members(last_name, first_name);
CREATE INDEX idx_members_phone ON members(phone_number);
CREATE INDEX idx_coverages_member ON coverages(member_id);
CREATE INDEX idx_coverages_status ON coverages(status, period_start, period_end);
CREATE INDEX idx_campaigns_status_scheduled ON campaigns(status, scheduled_at);
CREATE INDEX idx_campaign_runs_campaign ON campaign_runs(campaign_id);
CREATE INDEX idx_delivery_records_run ON delivery_records(campaign_run_id);
CREATE INDEX idx_delivery_records_manual ON delivery_records(manual_sms_id);
