-- 008_campaign_members.sql
-- Allow assigning specific members to campaigns

CREATE TABLE campaign_members (
  campaign_id UNIQUEIDENTIFIER NOT NULL REFERENCES campaigns(id) ON DELETE CASCADE,
  member_id UNIQUEIDENTIFIER NOT NULL REFERENCES members(id) ON DELETE CASCADE,
  added_at DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
  PRIMARY KEY (campaign_id, member_id)
);

CREATE INDEX idx_campaign_members_campaign ON campaign_members(campaign_id);
CREATE INDEX idx_campaign_members_member ON campaign_members(member_id);

-- Add targeting_mode to campaigns: 'auto' uses type-based rules, 'manual' uses selected members
ALTER TABLE campaigns ADD targeting_mode NVARCHAR(10) NOT NULL DEFAULT 'auto'
  CHECK (targeting_mode IN ('auto', 'manual'));
