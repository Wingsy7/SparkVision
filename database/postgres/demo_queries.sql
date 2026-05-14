SELECT target_table, rows_imported, imported_at
FROM import_runs
ORDER BY imported_at DESC;

SELECT code, libelle, valeur, unite, etat
FROM v_kpi_summary
ORDER BY code;

SELECT mois, total_kwh, tendance, evolution_kwh
FROM v_rse_transition
ORDER BY mois;

SELECT horodatage, valeur_kwh, seuil_anomalie
FROM v_technician_anomalies_7d
WHERE anomalie = true
ORDER BY valeur_kwh DESC
LIMIT 10;
