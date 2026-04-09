-- 008_unique_member.sql
-- Prevent duplicate members with same name and DOB

CREATE UNIQUE INDEX idx_members_unique_name_dob
ON members(first_name, last_name, date_of_birth)
WHERE date_of_birth IS NOT NULL;
