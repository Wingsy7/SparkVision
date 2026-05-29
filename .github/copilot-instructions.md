# SparkVision — Instructions GitHub Copilot
# Fichier : .github/copilot-instructions.md
# Placer ce fichier à la racine du dépôt pour que Copilot l'utilise automatiquement.

---

## Projet

**SparkVision** est une application Windows de visualisation et d'analyse de consommation énergétique.
- Langage : C# .NET 10
- Interface : Windows Forms (pas WPF, pas MAUI, pas Blazor)
- Base de données : SQLite locale via Microsoft.Data.Sqlite (pas Entity Framework)
- Graphiques : LiveCharts2 (NuGet)
- API : ASP.NET Core Minimal API
- Infra optionnelle : PostgreSQL via Docker Compose

---

## Architecture — 3 projets Visual Studio

```
SparkVision.sln
├── SparkVision.Data/          ← Couche métier PARTAGÉE (class library)
│   ├── Services/CsvDataService.cs
│   └── Models/Models.cs
├── SparkVision.WinForms/      ← Interface utilisateur
│   ├── Forms/FormDashboard.cs
│   └── Controls/KpiCard.cs
└── SparkVision.Api/           ← API REST
    ├── Program.cs
    └── Dockerfile
```

**Règle fondamentale** : SparkVision.Data ne référence ni WinForms ni l'API.
SparkVision.WinForms et SparkVision.Api référencent tous les deux SparkVision.Data.

---

## Modèles (records immuables — ne jamais utiliser des classes mutables)

```csharp
public record TechnicienPointModel(DateTime Horodatage, double Valeur, bool Anomalie);
public record RseMoisModel(string Mois, double Chauffage, double EauChaude, double Appareils, double Eclairage, double Autres);
public record KpiModel(string Code, string Libelle, double Valeur, string Unite, bool Warning, bool Danger);
public record DataStatusModel(string DatabasePath, int TechnicienCount, int RseCount);
```

---

## CsvDataService — méthodes existantes

```csharp
// Initialise la base SQLite et importe les CSV si vide
public void InitializeDatabase()

// Retourne les relevés avec anomalies calculées (μ + seuilMultiplicateur × σ)
public List<TechnicienPointModel> GetTechnicien(int jours = 7, double seuilMultiplicateur = 2.0)

// Retourne les 6 KPI
public List<KpiModel> GetKpis(double seuilMultiplicateur = 2.0)

// Retourne le bilan RSE mensuel
public List<RseMoisModel> GetRse()

// Retourne l'agrégation journalière (GROUP BY date)
public List<TechnicienPointModel> GetAgregationJournaliere(int jours = 30)

// Retourne le nombre de lignes par table
public DataStatusModel GetDataStatus()
```

---

## Conventions de code OBLIGATOIRES

- **Requêtes SQLite** : toujours paramétrées avec `$param` — jamais de concaténation de chaîne
- **Modèles** : toujours des `record` immuables, jamais des `class` avec setters
- **Gestion d'erreurs** : `try/catch` dans les méthodes de CsvDataService, log dans `sparkvision.log`
- **Log** : méthode statique `Log(string message)` — purge automatique à 500 lignes (conserver 300)
- **Langue** : commentaires en français, noms de variables en anglais ou français cohérent avec l'existant
- **Pas de ORM** : pas d'Entity Framework, pas de Dapper — Microsoft.Data.Sqlite uniquement

---

## Conventions WinForms / LiveCharts2

- Les `ObservableCollection<T>` pour LiveCharts2 sont des **champs de FormDashboard** (jamais réassignés — utiliser `.Clear()` puis `.Add()`)
- Les KpiCard sont des contrôles personnalisés — ne pas utiliser de Label standard pour les KPI
- Pas de polling/timer automatique — rechargement uniquement sur action utilisateur
- Les appels à CsvDataService se font dans des méthodes `ChargerXxx()` privées

---

## Conventions API (Minimal API .NET 10)

```csharp
// Pattern de validation obligatoire pour tous les endpoints avec paramètres
app.MapGet("/api/technicien", (double seuil = 2.0, int jours = 7) =>
{
    if (seuil < 0.1 || seuil > 10) return Results.BadRequest("seuil doit être entre 0.1 et 10");
    if (jours < 1 || jours > 365) return Results.BadRequest("jours doit être entre 1 et 365");
    return Results.Ok(dataService.GetTechnicien(jours, seuil));
});
```

---

## Améliorations prioritaires à implémenter

### 1. Async/await sur CsvDataService
Toutes les méthodes de CsvDataService doivent devenir async :

```csharp
// AVANT (synchrone — à remplacer)
public List<TechnicienPointModel> GetTechnicien(int jours = 7, double seuil = 2.0)

// APRÈS (async — cible)
public async Task<List<TechnicienPointModel>> GetTechnicienAsync(int jours = 7, double seuil = 2.0)
```

Utiliser `ExecuteReaderAsync`, `ExecuteNonQueryAsync`, `ExecuteScalarAsync`.
Dans FormDashboard, les méthodes `ChargerXxx()` deviennent `async Task` et appellent `await`.

### 2. Tests unitaires xUnit

Créer un projet `SparkVision.Tests` dans la solution.
Tests à couvrir en priorité :

```csharp
// Cas à tester dans CsvDataService
[Fact] void GetTechnicien_EmptyDatabase_ReturnsEmptyList()
[Fact] void GetTechnicien_AllSameValues_NoAnomalies()          // σ = 0
[Fact] void GetTechnicien_WithOutlier_DetectsAnomaly()
[Fact] void ParseCsvLine_ValidLine_ReturnsParsedValues()
[Fact] void ParseCsvLine_MalformedLine_ReturnsNull()
[Fact] void GetRse_Returns12Months_ForFullYear()
```

### 3. Configuration YAML des seuils KPI

Créer `config/seuils.yml` :
```yaml
kpi:
  warning_multiplier: 1.5
  danger_multiplier: 2.5
  max_daily_kwh: 50.0
```

Charger au démarrage via `IConfiguration` ou parsing YAML manuel.
KpiCard doit accepter les seuils en paramètre au lieu de les avoir codés en dur.

---

## Structure des fichiers CSV attendus

### technician_dataset.csv
```
date;heure;valeur_kWh
15/01/2026;08:00:00;2.34
15/01/2026;09:00:00;2.51
```
- Séparateur : point-virgule
- Format date : `dd/MM/yyyy HH:mm:ss` (combiné date+heure)
- Encodage : UTF-8

### rse_dataset.csv
```
Month,Heating_kWh,WaterHeating_kWh,Appliances_kWh,Lighting_kWh,Other_kWh
2026-01,120.5,45.2,38.1,22.3,12.4
```
- Séparateur : virgule
- Format mois : `yyyy-MM`
- Encodage : UTF-8

---

## Schéma SQLite

```sql
CREATE TABLE IF NOT EXISTS TechnicianReadings (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Horodatage TEXT NOT NULL,
    Valeur REAL NOT NULL
);
CREATE INDEX IF NOT EXISTS IX_TechnicianReadings_Horodatage ON TechnicianReadings(Horodatage);

CREATE TABLE IF NOT EXISTS RseReadings (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Mois TEXT NOT NULL,
    Poste TEXT NOT NULL,
    Valeur REAL NOT NULL
);
CREATE INDEX IF NOT EXISTS IX_RseReadings_Mois ON RseReadings(Mois);
```

---

## Endpoints API existants (ne pas modifier les routes)

| Endpoint | Paramètres | Description |
|---|---|---|
| GET /health | — | Statut de l'API |
| GET /api/status | — | Compteurs SQLite |
| GET /api/kpis | seuil (0.1–10) | 6 indicateurs KPI |
| GET /api/technicien | jours (1–365), seuil (0.1–10) | Relevés + anomalies |
| GET /api/technicien/jours | jours (1–365) | Agrégation journalière |
| GET /api/energie | jours (1–365), seuil (0.1–10) | Énergie calculée en Wh par intégration trapèze |
| GET /api/rse | — | Bilan mensuel complet |
| GET /api/rse/resume | — | Résumé annuel min/max |

---

## Ce qu'il ne faut PAS faire

- Ne pas ajouter Entity Framework Core
- Ne pas remplacer WinForms par WPF ou MAUI
- Ne pas ajouter de timer/polling automatique dans FormDashboard
- Ne pas utiliser de classes mutables pour les modèles
- Ne pas concaténer des chaînes dans les requêtes SQL
- Ne pas créer de nouvelles dépendances cloud ou réseau externes
- Ne pas casser la compatibilité de l'interface CsvDataService (les méthodes async doivent coexister avec les synchrones le temps de la migration)
