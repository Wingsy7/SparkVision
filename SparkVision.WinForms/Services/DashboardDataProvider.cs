using System.Globalization;
using System.Net.Http.Json;
using SparkVision.Data.Models;
using SparkVision.Data.Services;

namespace SparkVision.WinForms.Services;

public sealed class DashboardDataProvider : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly string _apiBaseUrl;
    private CsvDataService? _localService;
    private bool _apiAvailable;

    public DashboardDataProvider()
    {
        _apiBaseUrl = GetApiBaseUrl();
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(_apiBaseUrl),
            Timeout = TimeSpan.FromSeconds(2)
        };
        _apiAvailable = CheckApiAvailable();
    }

    public string SourceLabel =>
        _apiAvailable
            ? $"API {_apiBaseUrl.TrimEnd('/')}"
            : $"SQLite {Path.GetFileName(LocalService.DatabasePath)}";

    private CsvDataService LocalService => _localService ??= new CsvDataService();

    public List<KpiModel> GetKpis(double seuilMultiplicateur = 2.0) =>
        FromApiOrLocal(
            $"api/kpis?seuil={Format(seuilMultiplicateur)}",
            () => LocalService.GetKpis(seuilMultiplicateur));

    public List<TechnicienPointModel> GetTechnicien(int jours = 7, double seuilMultiplicateur = 2.0) =>
        FromApiOrLocal(
            $"api/technicien?jours={jours}&seuil={Format(seuilMultiplicateur)}",
            () => LocalService.GetTechnicien(jours, seuilMultiplicateur));

    public List<TechnicienPointModel> GetAgregationJournaliere(int jours = 30) =>
        FromApiOrLocal(
            $"api/technicien/jours?jours={jours}",
            () => LocalService.GetAgregationJournaliere(jours));

    public List<RseMoisModel> GetRse() =>
        FromApiOrLocal("api/rse", () => LocalService.GetRse());

    public DataStatusModel GetStatus() =>
        FromApiOrLocal("api/status", () => LocalService.GetStatus());

    public void Dispose()
    {
        _httpClient.Dispose();
    }

    private T FromApiOrLocal<T>(string path, Func<T> localFactory)
    {
        if (!_apiAvailable)
        {
            return localFactory();
        }

        try
        {
            var value = _httpClient.GetFromJsonAsync<T>(path).GetAwaiter().GetResult();
            return value ?? localFactory();
        }
        catch (Exception ex)
        {
            _apiAvailable = false;
            LogFallback(ex);
            return localFactory();
        }
    }

    private bool CheckApiAvailable()
    {
        try
        {
            using var response = _httpClient.GetAsync("health").GetAwaiter().GetResult();
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private static string GetApiBaseUrl()
    {
        var value = Environment.GetEnvironmentVariable("SPARKVISION_API_URL");
        if (string.IsNullOrWhiteSpace(value))
        {
            value = "http://127.0.0.1:8080";
        }

        return value.TrimEnd('/') + "/";
    }

    private static string Format(double value) =>
        value.ToString(CultureInfo.InvariantCulture);

    private static void LogFallback(Exception ex)
    {
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | API indisponible, fallback SQLite : {ex.Message}";
        File.AppendAllText(
            Path.Combine(AppContext.BaseDirectory, "sparkvision.log"),
            line + Environment.NewLine);
    }
}
