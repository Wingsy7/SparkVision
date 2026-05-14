CREATE TABLE IF NOT EXISTS technician_readings (
    id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    horodatage TIMESTAMP NOT NULL UNIQUE,
    valeur_kwh NUMERIC(10, 3) NOT NULL CHECK (valeur_kwh >= 0),
    imported_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS ix_technician_readings_horodatage
    ON technician_readings (horodatage);

CREATE TABLE IF NOT EXISTS rse_readings (
    id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    mois CHAR(7) NOT NULL CHECK (mois ~ '^[0-9]{4}-[0-9]{2}$'),
    poste VARCHAR(40) NOT NULL,
    valeur_kwh NUMERIC(12, 2) NOT NULL CHECK (valeur_kwh >= 0),
    imported_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT uq_rse_readings_mois_poste UNIQUE (mois, poste)
);

CREATE INDEX IF NOT EXISTS ix_rse_readings_mois
    ON rse_readings (mois);

CREATE TABLE IF NOT EXISTS kpi_thresholds (
    code VARCHAR(40) PRIMARY KEY,
    libelle VARCHAR(120) NOT NULL,
    warning_value NUMERIC(12, 3),
    danger_value NUMERIC(12, 3),
    unite VARCHAR(20) NOT NULL DEFAULT '',
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS import_runs (
    id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    source_file VARCHAR(160) NOT NULL,
    target_table VARCHAR(80) NOT NULL,
    rows_imported INTEGER NOT NULL,
    imported_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

INSERT INTO kpi_thresholds (code, libelle, warning_value, danger_value, unite)
VALUES
    ('LAST_HOUR', 'Derniere heure', 1.5, 2.0, 'kWh'),
    ('DAY_TOTAL', 'Total 24h', 30, 40, 'kWh'),
    ('WEEK_TOTAL', 'Total 7j', 180, 240, 'kWh'),
    ('WEEK_PEAK', 'Pic 7j', 1.8, 2.5, 'kWh'),
    ('ANOMALIES', 'Anomalies 7j', 1, 3, ''),
    ('RSE_MONTH', 'Total annuel RSE', 4500, 5200, 'kWh')
ON CONFLICT (code) DO UPDATE
SET libelle = EXCLUDED.libelle,
    warning_value = EXCLUDED.warning_value,
    danger_value = EXCLUDED.danger_value,
    unite = EXCLUDED.unite,
    updated_at = now();
