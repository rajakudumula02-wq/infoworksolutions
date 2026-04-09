-- 004_audit_log.sql
-- Campaign audit trail for tracking all actions

CREATE TABLE audit_logs (
  id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
  entity_type NVARCHAR(50) NOT NULL,
  entity_id NVARCHAR(100) NOT NULL,
  action NVARCHAR(50) NOT NULL,
  details NVARCHAR(MAX),
  performed_by NVARCHAR(100) DEFAULT 'system',
  created_at DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET()
);

CREATE INDEX idx_audit_entity ON audit_logs(entity_type, entity_id);
CREATE INDEX idx_audit_action ON audit_logs(action);
CREATE INDEX idx_audit_date ON audit_logs(created_at DESC);
