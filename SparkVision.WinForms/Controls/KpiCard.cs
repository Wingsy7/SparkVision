namespace SparkVision.WinForms.Controls;

public class KpiCard : Panel
{
    private readonly Label _titre = new() { Font = new Font("Segoe UI", 9), AutoSize = true };
    private readonly Label _valeur = new() { Font = new Font("Segoe UI", 18, FontStyle.Bold), AutoSize = true };
    private readonly Label _unite = new() { Font = new Font("Segoe UI", 9), AutoSize = true };
    private readonly Color _normalBack = Color.White;
    private readonly Color _normalText = Color.FromArgb(30, 35, 40);
    private readonly Color _accentColor;

    public KpiCard(string titre, Color? accentColor = null)
    {
        _accentColor = accentColor ?? Color.FromArgb(55, 138, 221);
        BackColor = _normalBack;
        BorderStyle = BorderStyle.FixedSingle;
        Padding = new Padding(10);
        Margin = new Padding(6);
        _titre.Text = titre;
        _titre.ForeColor = _normalText;
        _valeur.ForeColor = _normalText;
        _unite.ForeColor = Color.FromArgb(90, 96, 105);
        Controls.AddRange(new Control[] { _titre, _valeur, _unite });
        Layout += (_, _) => Positionner();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var brush = new SolidBrush(_accentColor);
        e.Graphics.FillRectangle(brush, 0, 0, Width, 5);
    }

    public void Mettre(double? valeur, string unite, double? warning = null, double? danger = null)
    {
        _valeur.Text = valeur.HasValue
            ? string.IsNullOrWhiteSpace(unite) ? $"{valeur.Value:N0}" : $"{valeur.Value:N1}"
            : "-";
        _unite.Text = unite;
        AppliquerEtat(valeur, warning, danger);
    }

    private void AppliquerEtat(double? valeur, double? warning, double? danger)
    {
        if (!valeur.HasValue || !warning.HasValue)
        {
            BackColor = _normalBack;
            _valeur.ForeColor = _normalText;
            Invalidate();
            return;
        }

        if (danger.HasValue && valeur.Value >= danger.Value)
        {
            BackColor = Color.FromArgb(255, 230, 230);
            _valeur.ForeColor = Color.FromArgb(176, 35, 24);
            Invalidate();
            return;
        }

        if (valeur.Value >= warning.Value)
        {
            BackColor = Color.FromArgb(255, 244, 214);
            _valeur.ForeColor = Color.FromArgb(151, 84, 0);
            Invalidate();
            return;
        }

        BackColor = Color.FromArgb(232, 248, 239);
        _valeur.ForeColor = Color.FromArgb(32, 112, 63);
        Invalidate();
    }

    private void Positionner()
    {
        _titre.Location = new Point(10, 8);
        _valeur.Location = new Point(10, 28);
        var uniteY = Math.Min(_valeur.Bottom + 2, Math.Max(8, Height - _unite.Height - 8));
        _unite.Location = new Point(10, uniteY);
    }
}
