ALTER TABLE workflow_status ADD COLUMN parent_workflow_id TEXT DEFAULT NULL;
CREATE INDEX IF NOT EXISTS idx_workflow_status_parent_workflow_id ON workflow_status (parent_workflow_id);
