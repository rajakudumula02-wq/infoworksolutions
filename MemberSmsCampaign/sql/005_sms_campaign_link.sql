-- 005_sms_campaign_link.sql
-- Add campaign reference to delivery_records and manual_sms_logs

ALTER TABLE delivery_records ADD campaign_id UNIQUEIDENTIFIER REFERENCES campaigns(id);
ALTER TABLE delivery_records ADD campaign_name NVARCHAR(255);
ALTER TABLE delivery_records ADD campaign_type NVARCHAR(20);
ALTER TABLE delivery_records ADD message_content NVARCHAR(160);

ALTER TABLE manual_sms_logs ADD campaign_id UNIQUEIDENTIFIER REFERENCES campaigns(id);

CREATE INDEX idx_delivery_records_campaign ON delivery_records(campaign_id);
