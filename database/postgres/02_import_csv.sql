CREATE TEMP TABLE technician_raw (
    date_text TEXT,
    heure_text TEXT,
    valeur_text TEXT
);

COPY technician_raw (date_text, heure_text, valeur_text)
FROM '/docker-entrypoint-initdb.d/data/technician_dataset.csv'
WITH (FORMAT csv, HEADER true, DELIMITER ';');

WITH imported AS (
    INSERT INTO technician_readings (horodatage, valeur_kwh)
    SELECT
        to_timestamp(date_text || ' ' || heure_text, 'DD/MM/YYYY HH24:MI:SS')::timestamp,
        replace(nullif(trim(valeur_text), ''), ',', '.')::numeric
    FROM technician_raw
    WHERE nullif(trim(date_text), '') IS NOT NULL
      AND nullif(trim(heure_text), '') IS NOT NULL
      AND nullif(trim(valeur_text), '') IS NOT NULL
    ON CONFLICT (horodatage) DO UPDATE
    SET valeur_kwh = EXCLUDED.valeur_kwh,
        imported_at = now()
    RETURNING 1
)
INSERT INTO import_runs (source_file, target_table, rows_imported)
SELECT 'technician_dataset.csv', 'technician_readings', count(*)::integer
FROM imported;

CREATE TEMP TABLE rse_raw (
    mois TEXT,
    total_kwh NUMERIC(12, 2),
    heating_kwh NUMERIC(12, 2),
    water_heating_kwh NUMERIC(12, 2),
    appliances_kwh NUMERIC(12, 2),
    lighting_kwh NUMERIC(12, 2),
    other_kwh NUMERIC(12, 2)
);

COPY rse_raw (
    mois,
    total_kwh,
    heating_kwh,
    water_heating_kwh,
    appliances_kwh,
    lighting_kwh,
    other_kwh
)
FROM '/docker-entrypoint-initdb.d/data/rse_dataset.csv'
WITH (FORMAT csv, HEADER true, DELIMITER ',');

WITH normalized AS (
    SELECT mois, poste, valeur_kwh
    FROM rse_raw
    CROSS JOIN LATERAL (
        VALUES
            ('Chauffage', heating_kwh),
            ('Eau chaude', water_heating_kwh),
            ('Appareils', appliances_kwh),
            ('Eclairage', lighting_kwh),
            ('Autres', other_kwh)
    ) AS postes(poste, valeur_kwh)
),
imported AS (
    INSERT INTO rse_readings (mois, poste, valeur_kwh)
    SELECT mois, poste, valeur_kwh
    FROM normalized
    WHERE mois IS NOT NULL
      AND poste IS NOT NULL
      AND valeur_kwh IS NOT NULL
    ON CONFLICT (mois, poste) DO UPDATE
    SET valeur_kwh = EXCLUDED.valeur_kwh,
        imported_at = now()
    RETURNING 1
)
INSERT INTO import_runs (source_file, target_table, rows_imported)
SELECT 'rse_dataset.csv', 'rse_readings', count(*)::integer
FROM imported;
