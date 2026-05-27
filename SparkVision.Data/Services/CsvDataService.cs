using System.Globalization;
using Microsoft.Data.Sqlite;
using SparkVision.Data.Models;

namespace SparkVision.Data.Services;

public class CsvDataService
{
    private static readonly string LogPath = Path.Combine(AppContext.BaseDirectory, "sparkvision.log");

    private readonly string _dataDir;
    private readonly string _dbPath;
    private readonly string _connectionString;

    public CsvDataService(string? dataDir = null, string? databasePath = null)
    {
        _dataDir = dataDir ?? Path.Combine(AppContext.BaseDirectory, "Data");
        _dbPath = databasePath ?? Path.Combine(AppContext.BaseDirectory, "sparkvision.db");
        _connectionString = $"Data Source={_dbPath}";
        InitializeDatabase();
    }

    public string DatabasePath => _dbPath;

    public List<TechnicienPointModel> GetTechnicien(int jours = 7, double seuilMultiplicateur = 2.0)
    {
        using var connection = OpenConnection();
        using var maxCommand = connection.CreateCommand();
        maxCommand.CommandText = "SELECT MAX(Horodatage) FROM TechnicianReadings;";
        var maxValue = maxCommand.ExecuteScalar()?.ToString();
        if (string.IsNullOrWhiteSpace(maxValue))
        {
            return new List<TechnicienPointModel>();
        }

        var maxDate = ParseDbDate(maxValue);
        var debut = maxDate.Date.AddDays(-Math.Max(1, jours) + 1);

        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Horodatage, Valeur
            FROM TechnicianReadings
            WHERE Horodatage >= $debut
            ORDER BY Horodatage;
            """;
        command.Parameters.AddWithValue("$debut", ToDbDate(debut));

        var points = new List<TechnicienPointModel>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            points.Add(new TechnicienPointModel(
                ParseDbDate(reader.GetString(0)),
                reader.GetDouble(1),
                false));
        }

        var moyenne = points.Count > 0 ? points.Average(p => p.Valeur) : 0;
        var ecartType = points.Count > 0
            ? Math.Sqrt(points.Average(p => Math.Pow(p.Valeur - moyenne, 2)))
            : 0;
        var seuil = moyenne + seuilMultiplicateur * ecartType;

        return points
            .Select(p => p with { Anomalie = p.Valeur > seuil })
            .ToList();
    }

    public List<TechnicienPointModel> GetAgregationJournaliere(int jours = 30)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Jour, Total
            FROM (
                SELECT date(Horodatage) AS Jour, SUM(Valeur) AS Total
                FROM TechnicianReadings
                GROUP BY date(Horodatage)
                ORDER BY Jour DESC
                LIMIT $jours
            )
            ORDER BY Jour;
            """;
        command.Parameters.AddWithValue("$jours", Math.Max(1, jours));

        var points = new List<TechnicienPointModel>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            points.Add(new TechnicienPointModel(
                DateTime.ParseExact(reader.GetString(0), "yyyy-MM-dd", CultureInfo.InvariantCulture),
                reader.GetDouble(1),
                false));
        }

        return points;
    }

    public List<RseMoisModel> GetRse()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Mois, Poste, Valeur
            FROM RseReadings
            ORDER BY Mois, Poste;
            """;

        var rows = new List<(string Mois, string Poste, double Valeur)>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            rows.Add((reader.GetString(0), reader.GetString(1), reader.GetDouble(2)));
        }

        return rows
            .GroupBy(r => r.Mois)
            .Select(g => new RseMoisModel(
                g.Key,
                g.ToDictionary(r => r.Poste, r => r.Valeur)))
            .OrderBy(m => m.Mois)
            .ToList();
    }

    public List<KpiModel> GetKpis(double seuilMultiplicateur = 2.0)
    {
        var jour = GetTechnicien(1, seuilMultiplicateur);
        var semaine = GetTechnicien(7, seuilMultiplicateur);
        var rse = GetRse();
        var totalAnnuelRse = rse.Sum(m => m.Postes.Values.Sum());

        return new List<KpiModel>
        {
            new("LAST_HOUR", "Derniere heure", jour.LastOrDefault()?.Valeur ?? 0, "kWh"),
            new("DAY_TOTAL", "Total 24h", jour.Sum(p => p.Valeur), "kWh"),
            new("WEEK_TOTAL", "Total 7j", semaine.Sum(p => p.Valeur), "kWh"),
            new("WEEK_PEAK", "Pic 7j", semaine.Count > 0 ? semaine.Max(p => p.Valeur) : 0, "kWh"),
            new("ANOMALIES", "Anomalies 7j", semaine.Count(p => p.Anomalie), ""),
            new("RSE_MONTH", "Total annuel RSE", totalAnnuelRse, "kWh")
        };
    }

    public DataStatusModel GetStatus()
    {
        using var connection = OpenConnection();
        return new DataStatusModel(
            _dbPath,
            (int)CountRows(connection, "TechnicianReadings"),
            (int)CountRows(connection, "RseReadings"));
    }

    private void InitializeDatabase()
    {
        using var connection = OpenConnection();
        CreateSchema(connection);
        Log("Verification base SQLite");

        if (CountRows(connection, "TechnicianReadings") == 0)
        {
            var imported = ImportTechnician(connection, Path.Combine(_dataDir, "technician_dataset.csv"));
            Log($"Import technicien : {imported} lignes");
        }

        if (CountRows(connection, "RseReadings") == 0)
        {
            var imported = ImportRse(connection, Path.Combine(_dataDir, "rse_dataset.csv"));
            Log($"Import RSE : {imported} lignes");
        }
    }

    private SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }

    private static void CreateSchema(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS TechnicianReadings (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Horodatage TEXT NOT NULL,
                Valeur REAL NOT NULL
            );

            CREATE INDEX IF NOT EXISTS IX_TechnicianReadings_Horodatage
                ON TechnicianReadings (Horodatage);

            CREATE TABLE IF NOT EXISTS RseReadings (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Mois TEXT NOT NULL,
                Poste TEXT NOT NULL,
                Valeur REAL NOT NULL
            );

            CREATE INDEX IF NOT EXISTS IX_RseReadings_Mois
                ON RseReadings (Mois);
            """;
        command.ExecuteNonQuery();
    }

    private static long CountRows(SqliteConnection connection, string tableName)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM {tableName};";
        return (long)command.ExecuteScalar()!;
    }

    private static int ImportTechnician(SqliteConnection connection, string csvPath)
    {
        if (!File.Exists(csvPath))
        {
            Log($"Import technicien impossible : fichier absent {csvPath}");
            return 0;
        }

        var imported = 0;
        using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO TechnicianReadings (Horodatage, Valeur)
            VALUES ($horodatage, $valeur);
            """;
        var horodatageParam = command.Parameters.Add("$horodatage", SqliteType.Text);
        var valeurParam = command.Parameters.Add("$valeur", SqliteType.Real);

        foreach (var line in File.ReadLines(csvPath).Skip(1))
        {
            var parts = line.Trim().Split(';');
            if (parts.Length < 3)
            {
                continue;
            }

            if (!DateTime.TryParseExact(
                    $"{parts[0]} {parts[1]}",
                    "dd/MM/yyyy HH:mm:ss",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var horodatage))
            {
                continue;
            }

            if (!double.TryParse(parts[2], NumberStyles.Any, CultureInfo.InvariantCulture, out var valeur))
            {
                continue;
            }

            horodatageParam.Value = ToDbDate(horodatage);
            valeurParam.Value = valeur;
            command.ExecuteNonQuery();
            imported++;
        }

        transaction.Commit();
        return imported;
    }

    private static int ImportRse(SqliteConnection connection, string csvPath)
    {
        if (!File.Exists(csvPath))
        {
            Log($"Import RSE impossible : fichier absent {csvPath}");
            return 0;
        }

        var lines = File.ReadAllLines(csvPath);
        if (lines.Length <= 1)
        {
            return 0;
        }

        var imported = 0;
        var headers = lines[0].Split(',');
        var postes = new Dictionary<string, string>
        {
            ["Heating_kWh"] = "Chauffage",
            ["WaterHeating_kWh"] = "Eau chaude",
            ["Appliances_kWh"] = "Appareils",
            ["Lighting_kWh"] = "Eclairage",
            ["Other_kWh"] = "Autres"
        };

        using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO RseReadings (Mois, Poste, Valeur)
            VALUES ($mois, $poste, $valeur);
            """;
        var moisParam = command.Parameters.Add("$mois", SqliteType.Text);
        var posteParam = command.Parameters.Add("$poste", SqliteType.Text);
        var valeurParam = command.Parameters.Add("$valeur", SqliteType.Real);

        foreach (var line in lines.Skip(1))
        {
            var cols = line.Trim().Split(',');
            if (cols.Length == 0)
            {
                continue;
            }

            foreach (var (colonne, libelle) in postes)
            {
                var index = Array.IndexOf(headers, colonne);
                if (index < 0 || index >= cols.Length)
                {
                    continue;
                }

                if (!double.TryParse(cols[index], NumberStyles.Any, CultureInfo.InvariantCulture, out var value))
                {
                    continue;
                }

                moisParam.Value = cols[0];
                posteParam.Value = libelle;
                valeurParam.Value = value;
                command.ExecuteNonQuery();
                imported++;
            }
        }

        transaction.Commit();
        return imported;
    }

    private static void Log(string message)
    {
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | {message}";
        File.AppendAllText(LogPath, line + Environment.NewLine);

        var lines = File.ReadAllLines(LogPath);
        if (lines.Length > 500)
        {
            File.WriteAllLines(LogPath, lines.TakeLast(300));
        }
    }

    private static string ToDbDate(DateTime value) =>
        value.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

    private static DateTime ParseDbDate(string value) =>
        DateTime.ParseExact(value, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
}

