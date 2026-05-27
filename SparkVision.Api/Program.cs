using Microsoft.OpenApi.Models;
using SparkVision.Data.Models;
using SparkVision.Data.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton(_ =>
{
    var dataDir = builder.Configuration["SparkVision:DataDir"];
    return string.IsNullOrWhiteSpace(dataDir)
        ? new CsvDataService()
        : new CsvDataService(dataDir);
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "SparkVision API",
        Version = "v1",
        Description = "API REST pour exposer les KPI, anomalies et bilans energetiques SparkVision."
    });
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.DocumentTitle = "SparkVision API";
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "SparkVision API v1");
});

app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    service = "SparkVision.Api",
    time = DateTimeOffset.UtcNow
}))
.WithName("Health")
.WithTags("System");

var api = app.MapGroup("/api").WithTags("SparkVision");

api.MapGet("/status", (CsvDataService service) => Results.Ok(service.GetStatus()))
    .WithName("GetDataStatus");

api.MapGet("/kpis", (CsvDataService service, double seuil = 2.0) =>
{
    var validation = ValidateSeuil(seuil);
    return validation ?? Results.Ok(service.GetKpis(seuil));
})
.WithName("GetKpis");

api.MapGet("/technicien", (CsvDataService service, int jours = 7, double seuil = 2.0) =>
{
    var validation = ValidateJours(jours) ?? ValidateSeuil(seuil);
    return validation ?? Results.Ok(service.GetTechnicien(jours, seuil));
})
.WithName("GetTechnicienReadings");

api.MapGet("/technicien/jours", (CsvDataService service, int jours = 30) =>
{
    var validation = ValidateJours(jours);
    return validation ?? Results.Ok(service.GetAgregationJournaliere(jours));
})
.WithName("GetDailyTechnicienTotals");

api.MapGet("/rse", (CsvDataService service) => Results.Ok(service.GetRse()))
    .WithName("GetRseReadings");

api.MapGet("/rse/resume", (CsvDataService service) =>
{
    var data = service.GetRse();
    if (data.Count == 0)
    {
        return Results.Ok(new RseSummaryResponse(0, 0, null, null, Array.Empty<RseMonthTotalResponse>()));
    }

    var months = data
        .Select(m => new RseMonthTotalResponse(m.Mois, Math.Round(m.Postes.Values.Sum(), 2), m.Postes))
        .ToArray();

    return Results.Ok(new RseSummaryResponse(
        Math.Round(months.Sum(m => m.TotalKwh), 2),
        months.Length,
        months.MinBy(m => m.TotalKwh),
        months.MaxBy(m => m.TotalKwh),
        months));
})
.WithName("GetRseSummary");

app.Run();

static IResult? ValidateJours(int jours)
{
    return jours is < 1 or > 365
        ? Results.BadRequest(new { error = "Le parametre 'jours' doit etre compris entre 1 et 365." })
        : null;
}

static IResult? ValidateSeuil(double seuil)
{
    return seuil is < 0.1 or > 10
        ? Results.BadRequest(new { error = "Le parametre 'seuil' doit etre compris entre 0.1 et 10." })
        : null;
}

public record RseMonthTotalResponse(
    string Mois,
    double TotalKwh,
    Dictionary<string, double> Postes);

public record RseSummaryResponse(
    double TotalAnnuelKwh,
    int NombreMois,
    RseMonthTotalResponse? MoisMinimum,
    RseMonthTotalResponse? MoisMaximum,
    IReadOnlyList<RseMonthTotalResponse> Mois);
