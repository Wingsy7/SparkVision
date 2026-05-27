using SparkVision.WinForms.Controls;
using SparkVision.Data.Models;
using SparkVision.Data.Services;
using SparkVision.WinForms.Models;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.WinForms;
using SkiaSharp;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;

namespace SparkVision.WinForms.Forms;

public class FormDashboard : Form
{
    private readonly CsvDataService _service = new();
    private readonly Label _lblStatus = new()
    {
        Dock = DockStyle.Bottom,
        Height = 24,
        Font = new Font("Segoe UI", 9)
    };
    private readonly Label _lblWhJour = new();
    private readonly Label _lblWhSemaine = new();
    private readonly Label _lblTechTitre = new()
    {
        Dock = DockStyle.Fill,
        Font = new Font("Segoe UI", 11, FontStyle.Bold),
        TextAlign = ContentAlignment.MiddleLeft
    };
    private readonly ComboBox _cmbPeriode = new()
    {
        DropDownStyle = ComboBoxStyle.DropDownList,
        Width = 130
    };
    private readonly NumericUpDown _numSeuil = new()
    {
        DecimalPlaces = 1,
        Increment = 0.1M,
        Minimum = 0.5M,
        Maximum = 5.0M,
        Value = 2.0M,
        Width = 72
    };
    private readonly Button _btnExport = new()
    {
        Text = "Exporter",
        AutoSize = true
    };
    private readonly Label _lblRseResume = new()
    {
        Dock = DockStyle.Fill,
        Font = new Font("Segoe UI", 11, FontStyle.Bold),
        TextAlign = ContentAlignment.MiddleLeft
    };
    private readonly Label _lblJourTitre = new()
    {
        Dock = DockStyle.Fill,
        Font = new Font("Segoe UI", 11, FontStyle.Bold),
        TextAlign = ContentAlignment.MiddleLeft
    };
    private readonly DataGridView _gridRse = new()
    {
        Dock = DockStyle.Fill,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        ReadOnly = true,
        RowHeadersVisible = false,
        AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Color.FromArgb(247, 250, 253)
        }
    };
    private readonly ObservableCollection<DateTimePoint> _serieTech = new();
    private readonly ObservableCollection<DateTimePoint> _serieAnomalies = new();
    private readonly ObservableCollection<double> _serieJournaliere = new();

    private Dictionary<string, KpiCard> _kpis = new();
    private CartesianChart _chartRse = null!;
    private CartesianChart _chartJour = null!;
    private int _joursTechnicien = 7;
    private List<TechnicienPointModel> _donneesTechAffichees = new();

    public FormDashboard()
    {
        Text = "SparkVision - Dashboard Energetique";
        ChargerIconeFenetre();
        WindowState = FormWindowState.Maximized;
        Font = new Font("Segoe UI", 10);
        BackColor = Color.FromArgb(245, 247, 250);

        InitTabs();
        Load += (_, _) => ChargerDonnees();
    }

    private void InitTabs()
    {
        var tabs = new TabControl { Dock = DockStyle.Fill };
        var tabTech = new TabPage("Technicien - Diagnostic");
        var tabRse = new TabPage("RSE - Bilan mensuel");
        var tabJour = new TabPage("Vue journaliere");

        tabTech.Controls.Add(BuildLayoutTechnicien());
        tabRse.Controls.Add(BuildLayoutRse());
        tabJour.Controls.Add(BuildLayoutJournalier());

        tabs.TabPages.Add(tabTech);
        tabs.TabPages.Add(tabRse);
        tabs.TabPages.Add(tabJour);
        tabs.SelectedIndexChanged += (_, _) =>
        {
            if (tabs.SelectedTab == tabRse)
            {
                ChargerRse();
            }
            else if (tabs.SelectedTab == tabJour)
            {
                ChargerJournalier();
            }
        };

        Controls.Add(tabs);
        Controls.Add(_lblStatus);
    }

    private TableLayoutPanel BuildLayoutTechnicien()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 5,
            ColumnCount = 1,
            Padding = new Padding(8)
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 210));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 70));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 80));

        layout.Controls.Add(BuildKpiRow(), 0, 0);
        layout.Controls.Add(BuildTechnicienToolbar(), 0, 1);
        layout.Controls.Add(_lblTechTitre, 0, 2);
        layout.Controls.Add(BuildChartTechnicien(), 0, 3);
        layout.Controls.Add(BuildEnergyRow(), 0, 4);
        return layout;
    }

    private TableLayoutPanel BuildKpiRow()
    {
        var row = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 2,
            GrowStyle = TableLayoutPanelGrowStyle.FixedSize
        };

        for (var i = 0; i < 3; i++)
        {
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / 3));
        }
        row.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        row.RowStyles.Add(new RowStyle(SizeType.Percent, 50));

        _kpis = new Dictionary<string, KpiCard>
        {
            ["LAST_HOUR"] = new KpiCard("Derniere heure", Color.FromArgb(55, 138, 221)),
            ["DAY_TOTAL"] = new KpiCard("Total 24h", Color.FromArgb(55, 138, 221)),
            ["WEEK_TOTAL"] = new KpiCard("Total 7j", Color.FromArgb(55, 138, 221)),
            ["WEEK_PEAK"] = new KpiCard("Pic 7j", Color.FromArgb(55, 138, 221)),
            ["ANOMALIES"] = new KpiCard("Anomalies", Color.FromArgb(230, 126, 34)),
            ["RSE_MONTH"] = new KpiCard("Total annuel RSE", Color.FromArgb(39, 174, 96)),
        };

        var index = 0;
        foreach (var kpi in _kpis.Values)
        {
            kpi.Dock = DockStyle.Fill;
            row.Controls.Add(kpi, index % 3, index / 3);
            index++;
        }

        return row;
    }

    private Panel BuildTechnicienToolbar()
    {
        var row = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(8, 6, 0, 0),
            BackColor = BackColor
        };
        row.Controls.Add(new Label
        {
            Text = "Periode :",
            AutoSize = true,
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            Margin = new Padding(0, 6, 8, 0)
        });

        _cmbPeriode.Items.AddRange(new object[] { "1 jour", "7 jours", "30 jours" });
        _cmbPeriode.SelectedIndex = 1;
        _cmbPeriode.SelectedIndexChanged += (_, _) =>
        {
            _joursTechnicien = _cmbPeriode.SelectedIndex switch
            {
                0 => 1,
                2 => 30,
                _ => 7
            };
            ChargerKpis();
            ChargerTechnicien();
            _lblStatus.Text = $"Periode technicien : {_cmbPeriode.SelectedItem}";
        };
        row.Controls.Add(_cmbPeriode);

        row.Controls.Add(new Label
        {
            Text = "Seuil anomalies :",
            AutoSize = true,
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            Margin = new Padding(24, 6, 8, 0)
        });
        row.Controls.Add(_numSeuil);
        row.Controls.Add(new Label
        {
            Text = "x ecart-type",
            AutoSize = true,
            Margin = new Padding(4, 6, 16, 0)
        });
        _numSeuil.ValueChanged += (_, _) =>
        {
            ChargerKpis();
            ChargerTechnicien();
            _lblStatus.Text = $"Seuil anomalies : {_numSeuil.Value:N1}x ecart-type";
        };

        _btnExport.Click += (_, _) => ExporterTechnicien();
        row.Controls.Add(_btnExport);
        return row;
    }

    private CartesianChart BuildChartTechnicien()
    {
        var chart = new CartesianChart
        {
            Dock = DockStyle.Fill,
            TooltipPosition = LiveChartsCore.Measure.TooltipPosition.Top
        };
        chart.Series = new ISeries[]
        {
            new LineSeries<DateTimePoint>
            {
                Values = _serieTech,
                Name = "Consommation horaire (kWh)",
                GeometrySize = 4,
                Stroke = new SolidColorPaint(new SKColor(55, 138, 221), 3),
                Fill = null
            },
            new ScatterSeries<DateTimePoint>
            {
                Values = _serieAnomalies,
                Name = "Anomalies",
                GeometrySize = 12,
                Fill = new SolidColorPaint(SKColors.Red),
                Stroke = new SolidColorPaint(SKColors.DarkRed, 2)
            }
        };
        chart.XAxes = new[]
        {
            new Axis
            {
                Labeler = v => new DateTime((long)v).ToString("dd/MM HH'h'"),
                LabelsRotation = 15
            }
        };
        chart.YAxes = new[] { new Axis { Name = "kWh" } };

        return chart;
    }

    private Panel BuildLayoutRse()
    {
        var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) };
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 68));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 32));

        _chartRse = new CartesianChart
        {
            Dock = DockStyle.Fill,
            TooltipPosition = LiveChartsCore.Measure.TooltipPosition.Top
        };
        layout.Controls.Add(_lblRseResume, 0, 0);
        layout.Controls.Add(_chartRse, 0, 1);
        layout.Controls.Add(_gridRse, 0, 2);
        panel.Controls.Add(layout);
        return panel;
    }

    private Panel BuildLayoutJournalier()
    {
        var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) };
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        layout.Controls.Add(_lblJourTitre, 0, 0);
        layout.Controls.Add(BuildChartJournalier(), 0, 1);
        panel.Controls.Add(layout);
        return panel;
    }

    private CartesianChart BuildChartJournalier()
    {
        _chartJour = new CartesianChart
        {
            Dock = DockStyle.Fill,
            TooltipPosition = LiveChartsCore.Measure.TooltipPosition.Top
        };
        _chartJour.Series = new ISeries[]
        {
            new ColumnSeries<double>
            {
                Values = _serieJournaliere,
                Name = "Total journalier (kWh)",
                Fill = new SolidColorPaint(new SKColor(39, 174, 96))
            }
        };
        _chartJour.XAxes = new[]
        {
            new Axis
            {
                LabelsRotation = 15
            }
        };
        _chartJour.YAxes = new[] { new Axis { Name = "kWh" } };
        return _chartJour;
    }

    private Panel BuildEnergyRow()
    {
        var row = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12, 20, 0, 0),
            BackColor = BackColor
        };
        _lblWhJour.AutoSize = _lblWhSemaine.AutoSize = true;
        _lblWhJour.Font = _lblWhSemaine.Font = new Font("Segoe UI", 14, FontStyle.Bold);
        _lblWhJour.Text = "24h : ...";
        _lblWhSemaine.Text = "7j : ...";
        row.Controls.Add(_lblWhJour);
        row.Controls.Add(new Label { Text = "  |  ", AutoSize = true, Font = new Font("Segoe UI", 14) });
        row.Controls.Add(_lblWhSemaine);

        return row;
    }

    private void ChargerDonnees()
    {
        try
        {
            ChargerKpis();
            ChargerTechnicien();
            ChargerRse();
            MettreAJourStatut();
        }
        catch (Exception ex)
        {
            _lblStatus.Text = $"Erreur chargement : {ex.Message}";
            File.AppendAllText(
                Path.Combine(AppContext.BaseDirectory, "sparkvision.log"),
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | ERREUR : {ex.Message}{Environment.NewLine}");
        }
    }

    private void ChargerKpis()
    {
        foreach (var kpi in _service.GetKpis(GetSeuilMultiplicateur()))
        {
            if (_kpis.TryGetValue(kpi.Code, out var card))
            {
                var seuil = GetSeuil(kpi.Code);
                card.Mettre(kpi.Valeur, kpi.Unite, seuil.Warning, seuil.Danger);
            }
        }

        var jour = _service.GetTechnicien(1, GetSeuilMultiplicateur());
        var semaine = _service.GetTechnicien(7, GetSeuilMultiplicateur());
        _lblWhJour.Text = $"24h : {jour.Sum(p => p.Valeur):N1} kWh";
        _lblWhSemaine.Text = $"7j : {semaine.Sum(p => p.Valeur):N1} kWh";
    }

    private void ChargerTechnicien()
    {
        var data = _service.GetTechnicien(_joursTechnicien, GetSeuilMultiplicateur());
        _donneesTechAffichees = data;
        _serieTech.Clear();
        _serieAnomalies.Clear();

        foreach (var p in data)
        {
            var point = new DateTimePoint(p.Horodatage, p.Valeur);
            _serieTech.Add(point);
            if (p.Anomalie)
            {
                _serieAnomalies.Add(point);
            }
        }

        if (data.Count > 0)
        {
            _lblTechTitre.Text =
                $"Consommation horaire - {data.First().Horodatage:dd/MM/yyyy HH:mm} au {data.Last().Horodatage:dd/MM/yyyy HH:mm}";
        }
        else
        {
            _lblTechTitre.Text = "Consommation horaire - aucune donnee pour cette periode";
        }
    }

    private void ChargerRse()
    {
        var data = _service.GetRse();
        if (data.Count == 0)
        {
            _lblRseResume.Text = "Aucune donnee RSE chargee.";
            _gridRse.DataSource = null;
            _chartRse.Series = Array.Empty<ISeries>();
            return;
        }

        var postes = new[] { "Chauffage", "Eau chaude", "Appareils", "Eclairage", "Autres" };
        var couleurs = new[]
        {
            new SKColor(55, 138, 221),
            new SKColor(39, 174, 96),
            new SKColor(230, 126, 34),
            new SKColor(241, 196, 15),
            new SKColor(155, 89, 182)
        };

        _chartRse.Series = postes
            .Select((poste, index) => new StackedColumnSeries<double>
            {
                Name = poste,
                Values = data.Select(m => m.Postes.TryGetValue(poste, out var v) ? v : 0).ToArray(),
                Fill = new SolidColorPaint(couleurs[index])
            })
            .ToArray<ISeries>();

        _chartRse.LegendPosition = LiveChartsCore.Measure.LegendPosition.Right;
        _chartRse.XAxes = new[]
        {
            new Axis
            {
                Labels = data.Select(m => m.Mois).ToArray(),
                LabelsRotation = 15
            }
        };
        _chartRse.YAxes = new[] { new Axis { Name = "kWh" } };

        var lignes = data
            .Select(m => new RseGridRow(
                    m.Mois,
                    GetPoste(m, "Chauffage"),
                    GetPoste(m, "Eau chaude"),
                    GetPoste(m, "Appareils"),
                    GetPoste(m, "Eclairage"),
                    GetPoste(m, "Autres"),
                    m.Postes.Values.Sum()))
            .ToList();
        lignes.Add(new RseGridRow(
            "Total",
            lignes.Sum(r => r.Chauffage),
            lignes.Sum(r => r.EauChaude),
            lignes.Sum(r => r.Appareils),
            lignes.Sum(r => r.Eclairage),
            lignes.Sum(r => r.Autres),
            lignes.Sum(r => r.Total)));
        _gridRse.DataSource = lignes;

        FormaterGridRse();

        var totalAnnuel = data.Sum(m => m.Postes.Values.Sum());
        _lblRseResume.Text =
            $"Bilan RSE par poste - {data.First().Mois} a {data.Last().Mois} | total annuel {totalAnnuel:N1} kWh | {BuildTransitionText(data)}";
        _chartRse.Update();
    }

    private void ChargerJournalier()
    {
        var data = _service.GetAgregationJournaliere(30);
        _serieJournaliere.Clear();
        foreach (var point in data)
        {
            _serieJournaliere.Add(point.Valeur);
        }

        _chartJour.XAxes = new[]
        {
            new Axis
            {
                Labels = data.Select(p => p.Horodatage.ToString("dd/MM", CultureInfo.InvariantCulture)).ToArray(),
                LabelsRotation = 15
            }
        };
        _chartJour.YAxes = new[] { new Axis { Name = "kWh" } };

        if (data.Count == 0)
        {
            _lblJourTitre.Text = "Agregation journaliere - aucune donnee";
            return;
        }

        var total = data.Sum(p => p.Valeur);
        var moyenne = data.Average(p => p.Valeur);
        _lblJourTitre.Text =
            $"Agregation journaliere - {data.First().Horodatage:dd/MM/yyyy} au {data.Last().Horodatage:dd/MM/yyyy} | total {total:N1} kWh | moyenne {moyenne:N1} kWh/jour";
        _chartJour.Update();
    }

    private static double GetPoste(RseMoisModel mois, string poste) =>
        mois.Postes.TryGetValue(poste, out var valeur) ? valeur : 0;

    private void FormaterGridRse()
    {
        if (_gridRse.Columns.Count == 0)
        {
            return;
        }

        foreach (DataGridViewColumn col in _gridRse.Columns)
        {
            if (col.Name != "Mois")
            {
                col.DefaultCellStyle.Format = "N1";
                col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            }
        }

        var maxTotal = _gridRse.Rows
            .Cast<DataGridViewRow>()
            .Where(r => !r.IsNewRow && r.Cells["Total"].Value is not null && r.Cells["Mois"].Value?.ToString() != "Total")
            .Select(r => Convert.ToDouble(r.Cells["Total"].Value))
            .DefaultIfEmpty(0)
            .Max();

        foreach (DataGridViewRow row in _gridRse.Rows)
        {
            if (row.IsNewRow)
            {
                continue;
            }

            var total = Convert.ToDouble(row.Cells["Total"].Value);
            if (row.Cells["Mois"].Value?.ToString() == "Total")
            {
                row.DefaultCellStyle.Font = new Font(_gridRse.Font, FontStyle.Bold);
                row.DefaultCellStyle.BackColor = Color.FromArgb(226, 239, 232);
                row.DefaultCellStyle.SelectionBackColor = Color.FromArgb(185, 219, 196);
                continue;
            }

            if (Math.Abs(total - maxTotal) < 0.001)
            {
                row.DefaultCellStyle.BackColor = Color.FromArgb(255, 235, 204);
                row.DefaultCellStyle.SelectionBackColor = Color.FromArgb(245, 190, 120);
            }
        }
    }

    private static (double? Warning, double? Danger) GetSeuil(string code) =>
        code switch
        {
            "LAST_HOUR" => (1.5, 2.0),
            "DAY_TOTAL" => (30, 40),
            "WEEK_TOTAL" => (180, 240),
            "WEEK_PEAK" => (1.8, 2.5),
            "ANOMALIES" => (1, 3),
            "RSE_MONTH" => (4500, 5200),
            _ => (null, null)
        };

    private double GetSeuilMultiplicateur() => (double)_numSeuil.Value;

    private void MettreAJourStatut()
    {
        var status = _service.GetStatus();
        _lblStatus.Text =
            $"DB OK {Path.GetFileName(status.DatabasePath)} | {status.TechnicianCount:N0} releves technicien | {status.RseCount:N0} lignes RSE | maj {DateTime.Now:HH:mm:ss}";
    }

    private void ExporterTechnicien()
    {
        if (_donneesTechAffichees.Count == 0)
        {
            MessageBox.Show("Aucune donnee technicien a exporter.", "SparkVision", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var dialog = new SaveFileDialog
        {
            Filter = "CSV (*.csv)|*.csv",
            FileName = $"sparkvision-technicien-{_cmbPeriode.SelectedItem}.csv".Replace(' ', '-')
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var lignes = new List<string> { "Horodatage;Valeur_kWh;Anomalie" };
        lignes.AddRange(_donneesTechAffichees.Select(p =>
            $"{p.Horodatage:yyyy-MM-dd HH:mm:ss};{p.Valeur.ToString(CultureInfo.InvariantCulture)};{p.Anomalie}"));
        File.WriteAllLines(dialog.FileName, lignes, Encoding.UTF8);
        _lblStatus.Text = $"Export CSV cree : {dialog.FileName}";
    }

    private void ChargerIconeFenetre()
    {
        var iconPath = Path.Combine(AppContext.BaseDirectory, "Resources", "SparkVision.ico");
        if (File.Exists(iconPath))
        {
            Icon = new Icon(iconPath);
        }
    }

    private static string BuildTransitionText(List<RseMoisModel> data)
    {
        var dernier = data.LastOrDefault();
        if (dernier is null)
        {
            return "Transition energetique : aucune donnee";
        }

        var dernierMois = DateTime.ParseExact(dernier.Mois + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture);
        var moisNMoinsUn = dernierMois.AddYears(-1).ToString("yyyy-MM", CultureInfo.InvariantCulture);
        var precedentAnnuel = data.FirstOrDefault(m => m.Mois == moisNMoinsUn);
        if (precedentAnnuel is not null)
        {
            return BuildEvolutionText("N-1", dernier, precedentAnnuel);
        }

        var precedent = data.Count >= 2 ? data[^2] : null;
        return precedent is null
            ? "Transition energetique : comparaison indisponible"
            : BuildEvolutionText("mois precedent", dernier, precedent);
    }

    private static string BuildEvolutionText(string reference, RseMoisModel courant, RseMoisModel precedent)
    {
        var courantTotal = courant.Postes.Values.Sum();
        var precedentTotal = precedent.Postes.Values.Sum();
        if (precedentTotal <= 0)
        {
            return $"Transition energetique : reference {reference} indisponible";
        }

        var variation = (courantTotal - precedentTotal) / precedentTotal * 100.0;
        var fleche = variation <= 0 ? "↓" : "↑";
        return $"Transition energetique : {fleche} {Math.Abs(variation):N1}% vs {reference}";
    }
}
