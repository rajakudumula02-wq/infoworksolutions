-- 006_opt_out.sql
-- Add SMS opt-out tracking for members

ALTER TABLE members ADD sms_opt_out BIT NOT NULL DEFAULT 0;
ALTER TABLE members ADD sms_opt_out_date DATETIMEOFFSET;

CREATE INDEX idx_members_opt_out ON members(sms_opt_out);
