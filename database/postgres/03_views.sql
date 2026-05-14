CREATE OR REPLACE VIEW v_rse_monthly_totals AS
SELECT
    mois,
    sum(valeur_kwh)::numeric(12, 2) AS total_kwh,
    sum(valeur_kwh) FILTER (WHERE poste = 'Chauffage')::numeric(12, 2) AS chauffage_kwh,
    sum(valeur_kwh) FILTER (WHERE poste = 'Eau chaude')::numeric(12, 2) AS eau_chaude_kwh,
    sum(valeur_kwh) FILTER (WHERE poste = 'Appareils')::numeric(12, 2) AS appareils_kwh,
    sum(valeur_kwh) FILTER (WHERE poste = 'Eclairage')::numeric(12, 2) AS eclairage_kwh,
    sum(valeur_kwh) FILTER (WHERE poste = 'Autres')::numeric(12, 2) AS autres_kwh
FROM rse_readings
GROUP BY mois;

CREATE OR REPLACE VIEW v_rse_transition AS
WITH ordered AS (
    SELECT
        mois,
        total_kwh,
        lag(total_kwh) OVER (ORDER BY mois) AS total_mois_precedent
    FROM v_rse_monthly_totals
)
SELECT
    mois,
    total_kwh,
    total_mois_precedent,
    (total_kwh - total_mois_precedent)::numeric(12, 2) AS evolution_kwh,
    CASE
        WHEN total_mois_precedent IS NULL THEN 'stable'
        WHEN total_kwh < total_mois_precedent THEN 'baisse'
        WHEN total_kwh > total_mois_precedent THEN 'hausse'
        ELSE 'stable'
    END AS tendance
FROM ordered;

CREATE OR REPLACE VIEW v_technician_anomalies_7d AS
WITH bounds AS (
    SELECT max(horodatage)::date AS max_day
    FROM technician_readings
),
windowed AS (
    SELECT t.*
    FROM technician_readings t
    CROSS JOIN bounds b
    WHERE t.horodatage >= b.max_day - interval '6 days'
),
stats AS (
    SELECT
        avg(valeur_kwh) AS moyenne,
        stddev_pop(valeur_kwh) AS ecart_type
    FROM windowed
)
SELECT
    w.horodatage,
    w.valeur_kwh,
    s.moyenne::numeric(10, 3) AS moyenne_7j,
    s.ecart_type::numeric(10, 3) AS ecart_type_7j,
    (s.moyenne + (2.0 * s.ecart_type))::numeric(10, 3) AS seuil_anomalie,
    w.valeur_kwh > (s.moyenne + (2.0 * s.ecart_type)) AS anomalie
FROM windowed w
CROSS JOIN stats s;

CREATE OR REPLACE VIEW v_kpi_summary AS
WITH bounds AS (
    SELECT max(horodatage)::date AS max_day
    FROM technician_readings
),
jour AS (
    SELECT t.*
    FROM technician_readings t
    CROSS JOIN bounds b
    WHERE t.horodatage >= b.max_day
),
semaine AS (
    SELECT t.*
    FROM technician_readings t
    CROSS JOIN bounds b
    WHERE t.horodatage >= b.max_day - interval '6 days'
),
semaine_stats AS (
    SELECT
        avg(valeur_kwh) AS moyenne,
        stddev_pop(valeur_kwh) AS ecart_type
    FROM semaine
),
kpis AS (
    SELECT
        'LAST_HOUR'::varchar(40) AS code,
        'Derniere heure'::varchar(120) AS libelle,
        COALESCE((SELECT valeur_kwh FROM technician_readings ORDER BY horodatage DESC LIMIT 1), 0)::numeric(12, 3) AS valeur,
        'kWh'::varchar(20) AS unite
    UNION ALL
    SELECT 'DAY_TOTAL', 'Total 24h', COALESCE((SELECT sum(valeur_kwh) FROM jour), 0)::numeric(12, 3), 'kWh'
    UNION ALL
    SELECT 'WEEK_TOTAL', 'Total 7j', COALESCE((SELECT sum(valeur_kwh) FROM semaine), 0)::numeric(12, 3), 'kWh'
    UNION ALL
    SELECT 'WEEK_PEAK', 'Pic 7j', COALESCE((SELECT max(valeur_kwh) FROM semaine), 0)::numeric(12, 3), 'kWh'
    UNION ALL
    SELECT
        'ANOMALIES',
        'Anomalies 7j',
        COALESCE((
            SELECT count(*)::numeric
            FROM semaine s
            CROSS JOIN semaine_stats st
            WHERE s.valeur_kwh > st.moyenne + (2.0 * st.ecart_type)
        ), 0)::numeric(12, 3),
        ''
    UNION ALL
    SELECT 'RSE_MONTH', 'Total annuel RSE', COALESCE((SELECT sum(valeur_kwh) FROM rse_readings), 0)::numeric(12, 3), 'kWh'
)
SELECT
    k.code,
    k.libelle,
    k.valeur,
    k.unite,
    t.warning_value,
    t.danger_value,
    CASE
        WHEN t.danger_value IS NOT NULL AND k.valeur >= t.danger_value THEN 'rouge'
        WHEN t.warning_value IS NOT NULL AND k.valeur >= t.warning_value THEN 'orange'
        ELSE 'vert'
    END AS etat
FROM kpis k
LEFT JOIN kpi_thresholds t ON t.code = k.code;
