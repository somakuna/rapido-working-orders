using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;
using Microsoft.EntityFrameworkCore;
using RapidoWorkingOrders.Data;
using RapidoWorkingOrders.Models;

namespace RapidoWorkingOrders.Dialogs;

public partial class CatalogDialog : Window
{
    public ObservableCollection<Material>   Materials    { get; } = new();
    public ObservableCollection<Technology> Technologies { get; } = new();

    private int? _editMatId;
    private int? _editTechId;
    private int? _editFinishId;
    private int? _editKwId;

    public CatalogDialog()
    {
        InitializeComponent();
        Reload();
        SetupGridEvents();
    }

    private void SetupGridEvents()
    {
        MatGrid.SelectionChanged    += (_, _) => LoadMatForm();
        TechGrid.SelectionChanged   += (_, _) => LoadTechForm();
        FinishGrid.SelectionChanged += (_, _) => LoadFinishForm();
        KwGrid.SelectionChanged     += (_, _) => LoadKwForm();
    }

    private void Reload()
    {
        try
        {
            using var db = new AppDbContext();

            var mats    = db.Materials.OrderBy(m => m.Name).ToList();
            var techs   = db.Technologies.OrderBy(t => t.Name).ToList();
            var finishes = db.FinishingOptions
                .OrderBy(f => f.SortOrder)
                .ThenBy(f => f.Name)
                .ToList();
            var kws = db.Keywords
                .Include(k => k.Material)
                .Include(k => k.Technology)
                .OrderBy(k => k.Text)
                .ToList();

            MatGrid.ItemsSource    = mats;
            TechGrid.ItemsSource   = techs;
            FinishGrid.ItemsSource = finishes;
            KwGrid.ItemsSource     = kws;

            Materials.Clear();
            Technologies.Clear();
            Materials.Add(new Material { Id = 0, Name = "(bez materijala)" });
            Technologies.Add(new Technology { Id = 0, Name = "(bez tehnologije)" });
            foreach (var m in mats)  Materials.Add(m);
            foreach (var t in techs) Technologies.Add(t);
        }
        catch (Exception ex)
        {
            var detail = BuildErrorDetail(ex);
            SetStatus($"Greška učitavanja: {detail}", error: true);
            MessageBox.Show($"Greška pri učitavanju podataka:\n\n{detail}",
                "Greška", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ── Materijali ────────────────────────────────────────────────────

    private void LoadMatForm()
    {
        if (MatGrid.SelectedItem is not Material m) return;
        _editMatId = m.Id;
        MatNameBox.Text = m.Name;
        SetStatus($"Odabrano: {m.Name}");
    }

    private void AddMat_Click(object sender, RoutedEventArgs e)
    {
        _editMatId = null;
        MatGrid.SelectedItem = null;
        MatNameBox.Text = "";
        MatNameBox.Focus();
        SetStatus("Novi materijal — upišite naziv i kliknite Spremi.");
    }

    private void SaveMat_Click(object sender, RoutedEventArgs e)
    {
        var name = MatNameBox.Text.Trim();
        if (string.IsNullOrEmpty(name)) { Warn("Naziv ne može biti prazan."); return; }

        bool ok = TrySave(() =>
        {
            using var db = new AppDbContext();
            Material mat = _editMatId.HasValue
                ? db.Materials.Find(_editMatId.Value) ?? throw new Exception("Materijal nije pronađen u bazi.")
                : new Material();
            mat.Name = name;
            if (!_editMatId.HasValue) db.Materials.Add(mat);
            db.SaveChanges();
        }, $"Materijal '{name}' već postoji.");

        if (ok)
        {
            SetStatus($"✔  Materijal '{name}' je uspješno spremljen.");
            Reload();
        }
    }

    private void DeleteMat_Click(object sender, RoutedEventArgs e)
    {
        if (!_editMatId.HasValue) { Warn("Odaberite materijal."); return; }
        if (!Confirm("Obrisati materijal?")) return;

        bool ok = TrySave(() =>
        {
            using var db = new AppDbContext();
            var m = db.Materials.Find(_editMatId.Value);
            if (m != null) { db.Materials.Remove(m); db.SaveChanges(); }
        });

        if (ok)
        {
            SetStatus("Materijal je obrisan.");
            _editMatId = null; MatNameBox.Text = "";
            Reload();
        }
    }

    // ── Tehnologije ───────────────────────────────────────────────────

    private void LoadTechForm()
    {
        if (TechGrid.SelectedItem is not Technology t) return;
        _editTechId = t.Id;
        TechNameBox.Text = t.Name;
        SetStatus($"Odabrano: {t.Name}");
    }

    private void AddTech_Click(object sender, RoutedEventArgs e)
    {
        _editTechId = null;
        TechGrid.SelectedItem = null;
        TechNameBox.Text = "";
        TechNameBox.Focus();
        SetStatus("Nova tehnologija — upišite naziv i kliknite Spremi.");
    }

    private void SaveTech_Click(object sender, RoutedEventArgs e)
    {
        var name = TechNameBox.Text.Trim();
        if (string.IsNullOrEmpty(name)) { Warn("Naziv ne može biti prazan."); return; }

        bool ok = TrySave(() =>
        {
            using var db = new AppDbContext();
            Technology tech = _editTechId.HasValue
                ? db.Technologies.Find(_editTechId.Value) ?? throw new Exception("Tehnologija nije pronađena u bazi.")
                : new Technology();
            tech.Name = name;
            if (!_editTechId.HasValue) db.Technologies.Add(tech);
            db.SaveChanges();
        }, $"Tehnologija '{name}' već postoji.");

        if (ok)
        {
            SetStatus($"✔  Tehnologija '{name}' je uspješno spremljena.");
            Reload();
        }
    }

    private void DeleteTech_Click(object sender, RoutedEventArgs e)
    {
        if (!_editTechId.HasValue) { Warn("Odaberite tehnologiju."); return; }
        if (!Confirm("Obrisati tehnologiju?")) return;

        bool ok = TrySave(() =>
        {
            using var db = new AppDbContext();
            var t = db.Technologies.Find(_editTechId.Value);
            if (t != null) { db.Technologies.Remove(t); db.SaveChanges(); }
        });

        if (ok)
        {
            SetStatus("Tehnologija je obrisana.");
            _editTechId = null; TechNameBox.Text = "";
            Reload();
        }
    }

    // ── Završna obrada ────────────────────────────────────────────────

    private void LoadFinishForm()
    {
        if (FinishGrid.SelectedItem is not FinishingOption fo) return;
        _editFinishId = fo.Id;
        FinishNameBox.Text = fo.Name;
        FinishSortBox.Text = fo.SortOrder.ToString();
        SetStatus($"Odabrano: {fo.Name}");
    }

    private void AddFinish_Click(object sender, RoutedEventArgs e)
    {
        _editFinishId = null;
        FinishGrid.SelectedItem = null;
        FinishNameBox.Text = "";
        try
        {
            using var db = new AppDbContext();
            int next = db.FinishingOptions.Any()
                ? db.FinishingOptions.Max(f => f.SortOrder) + 1
                : 0;
            FinishSortBox.Text = next.ToString();
        }
        catch { FinishSortBox.Text = "0"; }
        FinishNameBox.Focus();
        SetStatus("Nova završna obrada — upišite naziv i kliknite Spremi.");
    }

    private void SaveFinish_Click(object sender, RoutedEventArgs e)
    {
        var name = FinishNameBox.Text.Trim();
        if (string.IsNullOrEmpty(name)) { Warn("Naziv ne može biti prazan."); return; }
        int sort = int.TryParse(FinishSortBox.Text, out int s) ? s : 0;

        bool ok = TrySave(() =>
        {
            using var db = new AppDbContext();
            FinishingOption fo = _editFinishId.HasValue
                ? db.FinishingOptions.Find(_editFinishId.Value) ?? throw new Exception("Opcija nije pronađena u bazi.")
                : new FinishingOption();
            fo.Name      = name;
            fo.SortOrder = sort;
            if (!_editFinishId.HasValue) db.FinishingOptions.Add(fo);
            db.SaveChanges();
        });

        if (ok)
        {
            SetStatus($"✔  '{name}' je uspješno spremljeno.");
            Reload();
        }
    }

    private void DeleteFinish_Click(object sender, RoutedEventArgs e)
    {
        if (!_editFinishId.HasValue) { Warn("Odaberite opciju završne obrade."); return; }
        if (!Confirm("Obrisati opciju završne obrade?\n(Bit će uklonjena i s svih radnih naloga koji je koriste.)"))
            return;

        bool ok = TrySave(() =>
        {
            using var db = new AppDbContext();
            var fo = db.FinishingOptions.Find(_editFinishId.Value);
            if (fo != null) { db.FinishingOptions.Remove(fo); db.SaveChanges(); }
        });

        if (ok)
        {
            SetStatus("Opcija završne obrade je obrisana.");
            _editFinishId = null;
            FinishNameBox.Text = "";
            FinishSortBox.Text = "0";
            Reload();
        }
    }

    // ── Keywordovi ────────────────────────────────────────────────────

    private void LoadKwForm()
    {
        if (KwGrid.SelectedItem is not Keyword kw) return;
        _editKwId = kw.Id;
        KwTextBox.Text = kw.Text;
        KwMatCombo.SelectedValue  = kw.MaterialId   ?? 0;
        KwTechCombo.SelectedValue = kw.TechnologyId ?? 0;
        SetStatus($"Odabrano: {kw.Text}");
    }

    private void AddKw_Click(object sender, RoutedEventArgs e)
    {
        _editKwId = null;
        KwGrid.SelectedItem = null;
        KwTextBox.Text = "";
        KwMatCombo.SelectedIndex  = 0;
        KwTechCombo.SelectedIndex = 0;
        KwTextBox.Focus();
        SetStatus("Novi keyword — upišite tekst i kliknite Spremi.");
    }

    private void SaveKw_Click(object sender, RoutedEventArgs e)
    {
        var text = KwTextBox.Text.Trim();
        if (string.IsNullOrEmpty(text)) { Warn("Keyword ne može biti prazan."); return; }

        bool ok = TrySave(() =>
        {
            using var db = new AppDbContext();
            Keyword kw = _editKwId.HasValue
                ? db.Keywords.Find(_editKwId.Value) ?? throw new Exception("Keyword nije pronađen u bazi.")
                : new Keyword();

            kw.Text = text;
            kw.MaterialId   = (KwMatCombo.SelectedValue as int?) is int mi  && mi  > 0 ? mi  : null;
            kw.TechnologyId = (KwTechCombo.SelectedValue as int?) is int ti && ti > 0 ? ti : null;
            if (!_editKwId.HasValue) db.Keywords.Add(kw);
            db.SaveChanges();
        }, $"Keyword '{text}' već postoji.");

        if (ok)
        {
            SetStatus($"✔  Keyword '{text}' je uspješno spremljen.");
            Reload();
        }
    }

    private void DeleteKw_Click(object sender, RoutedEventArgs e)
    {
        if (!_editKwId.HasValue) { Warn("Odaberite keyword."); return; }
        if (!Confirm("Obrisati keyword?")) return;

        bool ok = TrySave(() =>
        {
            using var db = new AppDbContext();
            var kw = db.Keywords.Find(_editKwId.Value);
            if (kw != null) { db.Keywords.Remove(kw); db.SaveChanges(); }
        });

        if (ok)
        {
            SetStatus("Keyword je obrisan.");
            _editKwId = null; KwTextBox.Text = "";
            Reload();
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────

    private bool TrySave(Action action, string? duplicateMsg = null)
    {
        try
        {
            action();
            return true;
        }
        catch (Exception ex) when (duplicateMsg != null && IsDuplicateKey(ex))
        {
            SetStatus($"Greška: {duplicateMsg}", error: true);
            Warn(duplicateMsg);
            return false;
        }
        catch (Exception ex)
        {
            var detail = BuildErrorDetail(ex);
            SetStatus($"Greška: {detail}", error: true);
            MessageBox.Show($"Greška pri spremanju:\n\n{detail}",
                "Greška", MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
    }

    private static bool IsDuplicateKey(Exception ex)
    {
        var msg = (ex.InnerException ?? ex).Message;
        return msg.Contains("Duplicate entry", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildErrorDetail(Exception ex)
    {
        var msg = ex.Message;
        if (ex.InnerException != null)
            msg += $"\n\n→ {ex.InnerException.Message}";
        if (ex.InnerException?.InnerException != null)
            msg += $"\n→ {ex.InnerException.InnerException.Message}";
        return msg;
    }

    private void SetStatus(string msg, bool error = false)
    {
        StatusText.Text       = msg;
        StatusText.Foreground = error
            ? Brushes.Red
            : (Brush)FindResource("MutedBrush");
    }

    private static void Warn(string msg) =>
        MessageBox.Show(msg, "Upozorenje", MessageBoxButton.OK, MessageBoxImage.Warning);

    private static bool Confirm(string msg) =>
        MessageBox.Show(msg, "Potvrda", MessageBoxButton.YesNo, MessageBoxImage.Question)
        == MessageBoxResult.Yes;

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
