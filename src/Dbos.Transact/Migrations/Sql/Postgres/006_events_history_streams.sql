CREATE TABLE "{0}".workflow_events_history (
    workflow_uuid TEXT NOT NULL,
    function_id INT4 NOT NULL,
    key TEXT NOT NULL,
    value TEXT NOT NULL,
    PRIMARY KEY (workflow_uuid, function_id, key),
    FOREIGN KEY (workflow_uuid) REFERENCES "{0}".workflow_status(workflow_uuid)
        ON UPDATE CASCADE ON DELETE CASCADE
);
ALTER TABLE "{0}".streams ADD COLUMN function_id INT4 NOT NULL DEFAULT 0;
