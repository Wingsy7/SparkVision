namespace SparkVision.WinForms.Models;

public record RseGridRow(
    string Mois,
    double Chauffage,
    double EauChaude,
    double Appareils,
    double Eclairage,
    double Autres,
    double Total);
