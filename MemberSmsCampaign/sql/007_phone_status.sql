-- 007_phone_status.sql
-- Track phone number validation status

ALTER TABLE members ADD phone_status NVARCHAR(20) NOT NULL DEFAULT 'unknown'
  CHECK (phone_status IN ('unknown', 'valid', 'invalid', 'landline', 'disconnected', 'not_in_service'));
ALTER TABLE members ADD phone_status_updated_at DATETIMEOFFSET;
ALTER TABLE members ADD sms_failure_count INT NOT NULL DEFAULT 0;

CREATE INDEX idx_members_phone_status ON members(phone_status);
