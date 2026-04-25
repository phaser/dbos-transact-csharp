ALTER TABLE "{0}"."workflow_status" ADD COLUMN "delay_until_epoch_ms" BIGINT DEFAULT NULL;
CREATE INDEX "idx_workflow_status_delayed" ON "{0}"."workflow_status" ("delay_until_epoch_ms") WHERE status = 'DELAYED';
