CREATE TABLE IF NOT EXISTS workflow_status (
    workflow_uuid TEXT PRIMARY KEY,
    status TEXT,
    name TEXT,
    authenticated_user TEXT,
    assumed_role TEXT,
    authenticated_roles TEXT,
    request TEXT,
    output TEXT,
    error TEXT,
    executor_id TEXT,
    created_at INTEGER NOT NULL DEFAULT (CAST(strftime('%s', 'now') AS INTEGER) * 1000),
    updated_at INTEGER NOT NULL DEFAULT (CAST(strftime('%s', 'now') AS INTEGER) * 1000),
    application_version TEXT,
    application_id TEXT,
    class_name TEXT DEFAULT NULL,
    config_name TEXT DEFAULT NULL,
    recovery_attempts INTEGER DEFAULT 0,
    queue_name TEXT,
    workflow_timeout_ms INTEGER,
    workflow_deadline_epoch_ms INTEGER,
    inputs TEXT,
    started_at_epoch_ms INTEGER,
    deduplication_id TEXT,
    priority INTEGER NOT NULL DEFAULT 0,
    UNIQUE (queue_name, deduplication_id)
);

CREATE INDEX IF NOT EXISTS workflow_status_created_at_index ON workflow_status (created_at);
CREATE INDEX IF NOT EXISTS workflow_status_executor_id_index ON workflow_status (executor_id);
CREATE INDEX IF NOT EXISTS workflow_status_status_index ON workflow_status (status);

CREATE TABLE IF NOT EXISTS operation_outputs (
    workflow_uuid TEXT NOT NULL,
    function_id INTEGER NOT NULL,
    function_name TEXT NOT NULL DEFAULT '',
    output TEXT,
    error TEXT,
    child_workflow_id TEXT,
    PRIMARY KEY (workflow_uuid, function_id),
    FOREIGN KEY (workflow_uuid) REFERENCES workflow_status(workflow_uuid)
        ON UPDATE CASCADE ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS notifications (
    message_uuid TEXT NOT NULL DEFAULT (lower(hex(randomblob(16)))) PRIMARY KEY,
    destination_uuid TEXT NOT NULL,
    topic TEXT,
    message TEXT NOT NULL,
    created_at_epoch_ms INTEGER NOT NULL DEFAULT (CAST(strftime('%s', 'now') AS INTEGER) * 1000),
    FOREIGN KEY (destination_uuid) REFERENCES workflow_status(workflow_uuid)
        ON UPDATE CASCADE ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS idx_workflow_topic ON notifications (destination_uuid, topic);

CREATE TABLE IF NOT EXISTS workflow_events (
    workflow_uuid TEXT NOT NULL,
    key TEXT NOT NULL,
    value TEXT NOT NULL,
    PRIMARY KEY (workflow_uuid, key),
    FOREIGN KEY (workflow_uuid) REFERENCES workflow_status(workflow_uuid)
        ON UPDATE CASCADE ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS streams (
    workflow_uuid TEXT NOT NULL,
    key TEXT NOT NULL,
    value TEXT NOT NULL,
    "offset" INTEGER NOT NULL,
    PRIMARY KEY (workflow_uuid, key, "offset"),
    FOREIGN KEY (workflow_uuid) REFERENCES workflow_status(workflow_uuid)
        ON UPDATE CASCADE ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS event_dispatch_kv (
    service_name TEXT NOT NULL,
    workflow_fn_name TEXT NOT NULL,
    key TEXT NOT NULL,
    value TEXT,
    update_seq NUMERIC,
    update_time NUMERIC,
    PRIMARY KEY (service_name, workflow_fn_name, key)
);
