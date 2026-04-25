ALTER TABLE workflow_status ADD COLUMN forked_from TEXT;
CREATE INDEX IF NOT EXISTS idx_workflow_status_forked_from ON workflow_status (forked_from);
