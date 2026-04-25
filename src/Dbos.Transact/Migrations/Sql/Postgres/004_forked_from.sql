ALTER TABLE "{0}".workflow_status ADD COLUMN forked_from TEXT;
CREATE INDEX "idx_workflow_status_forked_from" ON "{0}"."workflow_status" ("forked_from");
