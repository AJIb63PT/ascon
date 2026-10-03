-- Мини-PDM: состав изделия — схема SQLite.
-- Используется прежде всего для автономного запуска тестов (in-memory) и как
-- «лёгкий» режим приложения. Скрипт идемпотентен.

CREATE TABLE IF NOT EXISTS pdm_object (
    id                 INTEGER PRIMARY KEY AUTOINCREMENT,
    -- 0 = Assembly, 1 = Part, 2 = StandardPart
    object_type        INTEGER NOT NULL,
    designation        TEXT    NULL,
    name               TEXT    NOT NULL,
    source_file_name TEXT    NULL,
    current_version_id INTEGER NULL
);

-- Обозначение уникально среди сборок и деталей; у стандартных изделий оно NULL.
CREATE UNIQUE INDEX IF NOT EXISTS ux_pdm_object_designation
    ON pdm_object (designation)
    WHERE designation IS NOT NULL;

-- Внешний идентификатор документа уникален.
CREATE UNIQUE INDEX IF NOT EXISTS ux_pdm_object_source_file_name
    ON pdm_object (source_file_name)
    WHERE source_file_name IS NOT NULL;

-- Наименование уникально среди стандартных изделий.
CREATE UNIQUE INDEX IF NOT EXISTS ux_pdm_object_standard_part_name
    ON pdm_object (name)
    WHERE object_type = 2;

CREATE INDEX IF NOT EXISTS ix_pdm_object_name
    ON pdm_object (name);

CREATE TABLE IF NOT EXISTS object_version (
    id         INTEGER PRIMARY KEY AUTOINCREMENT,
    object_id  INTEGER NOT NULL REFERENCES pdm_object (id) ON DELETE CASCADE,
    version_no INTEGER NOT NULL,
    -- 0 = В работе, 1 = Утверждено, 2 = Аннулировано
    state      INTEGER NOT NULL,
    material   TEXT    NULL,
    -- NUMERIC в SQLite не ограничивает разрядность и не округляет значение при
    -- записи, чего достаточно для масс с точностью до 4 знаков (например, 0.0021 кг).
    mass_kg    NUMERIC NULL,
    created_at TEXT    NOT NULL,
    CONSTRAINT uq_object_version_per_object UNIQUE (object_id, version_no),
    CONSTRAINT ck_object_version_no_positive CHECK (version_no >= 1),
    CONSTRAINT ck_object_version_mass CHECK (mass_kg IS NULL OR mass_kg >= 0)
);

CREATE TABLE IF NOT EXISTS bom_link (
    id                INTEGER PRIMARY KEY AUTOINCREMENT,
    parent_version_id INTEGER NOT NULL REFERENCES object_version (id) ON DELETE CASCADE,
    child_object_id   INTEGER NOT NULL REFERENCES pdm_object (id),
    quantity          INTEGER NOT NULL,
    CONSTRAINT uq_bom_link_per_parent UNIQUE (parent_version_id, child_object_id),
    CONSTRAINT ck_bom_link_quantity_positive CHECK (quantity > 0)
);

CREATE INDEX IF NOT EXISTS ix_bom_link_child ON bom_link (child_object_id);
CREATE INDEX IF NOT EXISTS ix_object_version_object ON object_version (object_id, version_no DESC);

CREATE TABLE IF NOT EXISTS import_log (
    id         INTEGER PRIMARY KEY AUTOINCREMENT,
    started_at TEXT    NOT NULL,
    file_name  TEXT    NOT NULL,
    -- 0 = Info, 1 = Warning, 2 = Error
    severity   INTEGER NOT NULL,
    reason     TEXT    NULL
);

CREATE INDEX IF NOT EXISTS ix_import_log_started_at ON import_log (started_at);