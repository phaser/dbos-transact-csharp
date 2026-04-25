ALTER TABLE notifications ADD COLUMN consumed INTEGER NOT NULL DEFAULT 0;
CREATE INDEX IF NOT EXISTS idx_notifications ON notifications (destination_uuid, topic);
