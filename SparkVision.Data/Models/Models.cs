namespace SparkVision.Data.Models;

public record TechnicienPointModel(DateTime Horodatage, double Valeur, bool Anomalie);

public record RseMoisModel(string Mois, Dictionary<string, double> Postes);

public record KpiModel(string Code, string Libelle, double Valeur, string Unite);

public record DataStatusModel(string DatabasePath, int TechnicianCount, int RseCount);
