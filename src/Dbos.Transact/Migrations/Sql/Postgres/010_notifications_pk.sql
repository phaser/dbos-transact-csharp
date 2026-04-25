DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.table_constraints
        WHERE table_schema = '{0}'
        AND table_name = 'notifications'
        AND constraint_type = 'PRIMARY KEY'
    ) THEN
        ALTER TABLE "{0}".notifications ADD PRIMARY KEY (message_uuid);
    END IF;
END $$;
