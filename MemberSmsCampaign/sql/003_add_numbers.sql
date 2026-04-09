-- 003_add_numbers.sql
-- Add auto-incrementing member_number and group_number

ALTER TABLE members ADD member_number INT IDENTITY(1000, 1) NOT NULL;
CREATE UNIQUE INDEX idx_members_number ON members(member_number);

ALTER TABLE member_groups ADD group_number INT IDENTITY(100, 1) NOT NULL;
CREATE UNIQUE INDEX idx_groups_number ON member_groups(group_number);
