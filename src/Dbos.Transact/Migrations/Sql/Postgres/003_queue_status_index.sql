CREATE INDEX "idx_workflow_status_queue_status_started" ON "{0}"."workflow_status" ("queue_name", "status", "started_at_epoch_ms");
