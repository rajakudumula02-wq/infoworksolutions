-- 002_groups.sql
-- Named member groups for campaign targeting

CREATE TABLE member_groups (
  id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
  name NVARCHAR(255) NOT NULL,
  description NVARCHAR(500),
  created_at DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
  updated_at DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET()
);

CREATE TABLE group_members (
  group_id UNIQUEIDENTIFIER NOT NULL REFERENCES member_groups(id) ON DELETE CASCADE,
  member_id UNIQUEIDENTIFIER NOT NULL REFERENCES members(id) ON DELETE CASCADE,
  added_at DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
  PRIMARY KEY (group_id, member_id)
);

CREATE INDEX idx_group_members_group ON group_members(group_id);
CREATE INDEX idx_group_members_member ON group_members(member_id);
