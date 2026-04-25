ALTER TABLE "{0}"."notifications" ADD COLUMN "consumed" BOOLEAN NOT NULL DEFAULT FALSE;
CREATE INDEX "idx_notifications" ON "{0}"."notifications" ("destination_uuid", "topic");
