ALTER TABLE "{0}"."workflow_status" ADD COLUMN "serialization" TEXT DEFAULT NULL;
ALTER TABLE "{0}"."notifications" ADD COLUMN "serialization" TEXT DEFAULT NULL;
ALTER TABLE "{0}"."workflow_events" ADD COLUMN "serialization" TEXT DEFAULT NULL;
ALTER TABLE "{0}"."workflow_events_history" ADD COLUMN "serialization" TEXT DEFAULT NULL;
ALTER TABLE "{0}"."operation_outputs" ADD COLUMN "serialization" TEXT DEFAULT NULL;
ALTER TABLE "{0}"."streams" ADD COLUMN "serialization" TEXT DEFAULT NULL;
