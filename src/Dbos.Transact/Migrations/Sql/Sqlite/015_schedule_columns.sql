ALTER TABLE workflow_schedules ADD COLUMN last_fired_at TEXT DEFAULT NULL;
ALTER TABLE workflow_schedules ADD COLUMN automatic_backfill INTEGER NOT NULL DEFAULT 0;
ALTER TABLE workflow_schedules ADD COLUMN cron_timezone TEXT DEFAULT NULL;
