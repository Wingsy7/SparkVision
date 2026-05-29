using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace SparkVision.Tests;

public class ApiEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public ApiEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Health_ReturnsOk()
    {
        var health = await _client.GetFromJsonAsync<HealthResponse>("/health");

        Assert.NotNull(health);
        Assert.Equal("ok", health.Status);
        Assert.Equal("SparkVision.Api", health.Service);
    }

    [Fact]
    public async Task Technicien_InvalidJours_ReturnsBadRequest()
    {
        var response = await _client.GetAsync("/api/technicien?jours=0&seuil=2");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Kpis_ReturnsSixIndicators()
    {
        var kpis = await _client.GetFromJsonAsync<List<KpiResponse>>("/api/kpis?seuil=2");

        Assert.NotNull(kpis);
        Assert.Equal(6, kpis.Count);
        Assert.Contains(kpis, kpi => kpi.Code == "ANOMALIES");
    }

    [Fact]
    public async Task Energie_ReturnsTrapezeWh()
    {
        var energy = await _client.GetFromJsonAsync<EnergieResponse>("/api/energie?jours=7&seuil=2");

        Assert.NotNull(energy);
        Assert.Equal("trapeze", energy.Methode);
        Assert.True(energy.NombrePoints > 1);
        Assert.True(energy.EnergieWh > 0);
        Assert.True(energy.EnergieKwh > 0);
    }

    private sealed record HealthResponse(string Status, string Service, DateTimeOffset Time);

    private sealed record KpiResponse(string Code, string Libelle, double Valeur, string Unite);

    private sealed record EnergieResponse(
        int Jours,
        double Seuil,
        string Methode,
        int NombrePoints,
        DateTime? Debut,
        DateTime? Fin,
        double EnergieKwh,
        double EnergieWh);
}
