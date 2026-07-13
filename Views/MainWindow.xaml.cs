using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.EntityFrameworkCore;
using RapidoWorkingOrders.Data;
using RapidoWorkingOrders.Dialogs;
using RapidoWorkingOrders.Models;
using Velopack;
using Velopack.Sources;

namespace RapidoWorkingOrders.Views;

public partial class MainWindow : Window
{
    private List<Work> _recentWorks = [];  // zadnjih 500, prikazano kad nema filtera
    private DispatcherTimer? _filterTimer;
    private DispatcherTimer? _pollTimer;
    private int _lastMaxWorkId;

    public MainWindow()
    {
        InitializeComponent();
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        Title = $"Rapido by ninaarm (v.{version?.Major}.{version?.Minor}.{version?.Build})";
        ReloadWorks();
        StartPollTimer();
        _ = CheckForUpdatesAsync();
    }

    private async Task CheckForUpdatesAsync()
    {
        try
        {
            var mgr = new UpdateManager(new GithubSource("https://github.com/somakuna/rapido-working-orders", null, false));
            var update = await mgr.CheckForUpdatesAsync();
            if (update == null) return;

            await mgr.DownloadUpdatesAsync(update);

            var result = MessageBox.Show(
                $"Dostupna je nova verzija ({update.TargetFullRelease.Version}).\nRestartaj aplikaciju da bi se update primijenio?",
                "Update dostupan", MessageBoxButton.OKCancel, MessageBoxImage.Information);

            if (result == MessageBoxResult.OK)
                mgr.ApplyUpdatesAndRestart(update.TargetFullRelease);
        }
        catch { /* silent — update nije kritičan */ }
    }

    protected override void OnClosed(EventArgs e)
    {
        _pollTimer?.Stop();
        base.OnClosed(e);
    }

    private void StartPollTimer()
    {
        _pollTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        _pollTimer.Tick += (_, _) => PollForNewWorks();
        _pollTimer.Start();
    }

    private void PollForNewWorks()
    {
        try
        {
            int maxId;
            using (var db = new AppDbContext())
                maxId = db.Works.Select(w => (int?)w.Id).Max() ?? 0;

            if (maxId != _lastMaxWorkId)
            {
                ReloadWorks();
                StatusText.Text += "  (automatski osvježeno)";
            }
        }
        catch { /* silent — DB może biti privremeno nedostupna */ }
    }

    // ─── Učitavanje ───────────────────────────────────────────────────

    private void ReloadWorks()
    {
        try
        {
            using var db = new AppDbContext();
            _recentWorks = db.Works
                .Include(w => w.Client)
                .Include(w => w.User)
                .OrderByDescending(w => w.WorkYear)
                .ThenByDescending(w => w.WorkNumber)
                .Take(500)
                .ToList();

            _lastMaxWorkId = db.Works.Select(w => (int?)w.Id).Max() ?? 0;
            int total = db.Works.Count();
            ApplyFilter();

            StatusText.Text = total > 500
                ? $"Prikazano zadnjih 500 od {total} radnih naloga."
                : $"Učitano {total} radnih naloga.";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Učitavanje radnih naloga nije uspjelo:\n\n{ex.Message}",
                "Greška", MessageBoxButton.OK, MessageBoxImage.Error);
            _recentWorks = [];
            WorksGrid.ItemsSource = null;
        }
    }

    // ─── Filter ───────────────────────────────────────────────────────

    private void ApplyFilter()
    {
        var text = FilterBox.Text?.Trim().ToLowerInvariant() ?? "";

        if (string.IsNullOrEmpty(text))
        {
            WorksGrid.ItemsSource = _recentWorks;
            return;
        }

        // Server-side pretraga — radi ispravno i za 100 000 naloga
        try
        {
            using var db = new AppDbContext();
            IQueryable<Work> query = db.Works
                .Include(w => w.Client)
                .Include(w => w.User);

            if (int.TryParse(text, out int num))
            {
                // Numerička pretraga: broj naloga ili godina
                query = query.Where(w => w.WorkNumber == num || w.WorkYear == num);
            }
            else
            {
                query = query.Where(w =>
                    (w.Client != null && w.Client.Name.ToLower().Contains(text)) ||
                    (w.User  != null && w.User.Name.ToLower().Contains(text))   ||
                    (w.Location != null && w.Location.ToLower().Contains(text)) ||
                    (w.Delivery != null && w.Delivery.ToLower().Contains(text)));
            }

            var results = query
                .OrderByDescending(w => w.WorkYear)
                .ThenByDescending(w => w.WorkNumber)
                .Take(500)
                .ToList();

            WorksGrid.ItemsSource = results;
            StatusText.Text = $"Filter: {results.Count} naloga.";
        }
        catch
        {
            // Fallback: klijentski filter na učitanih 500
            WorksGrid.ItemsSource = _recentWorks.Where(w =>
                string.Join(" ", w.FormattedNumber, w.Client?.Name ?? "",
                    w.User?.Name ?? "", w.Location ?? "", w.Delivery ?? "")
                .ToLowerInvariant().Contains(text)).ToList();
        }
    }

    private void Filter_TextChanged(object sender, TextChangedEventArgs e)
    {
        // Debounce 300 ms — ne šalje upit na svaki pritisak tipke
        _filterTimer?.Stop();
        _filterTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        _filterTimer.Tick += (_, _) => { _filterTimer.Stop(); ApplyFilter(); };
        _filterTimer.Start();
    }

    // ─── Helpers ─────────────────────────────────────────────────────

    private Work? GetSelectedWork() => WorksGrid.SelectedItem as Work;

    // ─── Toolbar i gumbi ──────────────────────────────────────────────

    private void ToolBar_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is ToolBar toolBar &&
            toolBar.Template.FindName("OverflowGrid", toolBar) is FrameworkElement overflowGrid)
        {
            overflowGrid.Visibility = Visibility.Collapsed;
        }
    }

    private void NewWork_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new WorkFormDialog { Owner = this };
        dlg.Closed += (_, _) => { if (dlg.SavedWorkId > 0) ReloadWorks(); };
        dlg.Show();
    }

    private void EditWork_Click(object sender, RoutedEventArgs e)
    {
        var work = GetSelectedWork();
        if (work == null) { MessageBox.Show("Odaberite radni nalog.", "Ispravka"); return; }

        try
        {
            Work fullWork;
            using (var db = new AppDbContext())
                fullWork = db.Works.First(w => w.Id == work.Id);

            var dlg = new WorkFormDialog(fullWork) { Owner = this };
            dlg.Closed += (_, _) => { if (dlg.SavedWorkId > 0) ReloadWorks(); };
            dlg.Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Greška pri otvaranju ispravka:\n\n{ex.Message}",
                "Greška", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void DuplicateWork_Click(object sender, RoutedEventArgs e)
    {
        var work = GetSelectedWork();
        if (work == null) { MessageBox.Show("Odaberite radni nalog za dupliciranje.", "Dupliciranje"); return; }

        try
        {
            Work fullWork;
            using (var db = new AppDbContext())
                fullWork = db.Works.First(w => w.Id == work.Id);

            var dlg = new WorkFormDialog(fullWork, isDuplicate: true) { Owner = this };
            dlg.Closed += (_, _) => { if (dlg.SavedWorkId > 0) ReloadWorks(); };
            dlg.Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Greška pri dupliciranju:\n\n{ex.Message}",
                "Greška", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void DeleteWork_Click(object sender, RoutedEventArgs e)
    {
        var work = GetSelectedWork();
        if (work == null) { MessageBox.Show("Odaberite radni nalog.", "Brisanje"); return; }

        var result = MessageBox.Show(
            $"Sigurno obrisati radni nalog {work.FormattedNumber}?\n" +
            "Svi povezani fajlovi će biti obrisani.",
            "Brisanje", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result != MessageBoxResult.Yes) return;

        try
        {
            using var db = new AppDbContext();
            var target = db.Works.Find(work.Id);
            if (target != null) { db.Works.Remove(target); db.SaveChanges(); }
            ReloadWorks();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Brisanje nije uspjelo:\n{ex.Message}",
                "Greška", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void PrintWork_Click(object sender, RoutedEventArgs e)
    {
        var work = GetSelectedWork();
        if (work == null) { MessageBox.Show("Odaberite radni nalog.", "Print"); return; }

        Work fullWork;
        List<WorkFile> files;
        try
        {
            using var db = new AppDbContext();
            fullWork = db.Works
                .Include(w => w.Client)
                .Include(w => w.User)
                .Include(w => w.SelectedFinishings!).ThenInclude(wfo => wfo.FinishingOption)
                .First(w => w.Id == work.Id);
            files = db.Files
                .Include(f => f.Technology)
                .Include(f => f.Material)
                .Where(f => f.WorkId == work.Id)
                .ToList();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Greška:\n{ex.Message}", "Greška", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        var dlg = new PrintPreviewDialog(fullWork, files) { Owner = this };
        dlg.ShowDialog();
    }

    private void WorksGrid_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        EditWork_Click(sender, e);
    }

    private void WorksGrid_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter)
        {
            EditWork_Click(sender, e);
            e.Handled = true;
        }
        else if (e.Key == System.Windows.Input.Key.Delete)
        {
            DeleteWork_Click(sender, e);
            e.Handled = true;
        }
    }

    private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        var mods = System.Windows.Input.Keyboard.Modifiers;

        if (mods == System.Windows.Input.ModifierKeys.None && e.Key == System.Windows.Input.Key.F5)
        {
            RefreshWorks_Click(sender, e);
            e.Handled = true;
            return;
        }

        if (mods != System.Windows.Input.ModifierKeys.Control) return;

        switch (e.Key)
        {
            case System.Windows.Input.Key.N: NewWork_Click(sender, e);        e.Handled = true; break;
            case System.Windows.Input.Key.D: DuplicateWork_Click(sender, e);  e.Handled = true; break;
            case System.Windows.Input.Key.P: PrintWork_Click(sender, e);      e.Handled = true; break;
            case System.Windows.Input.Key.K: OpenClients_Click(sender, e);    e.Handled = true; break;
            case System.Windows.Input.Key.T: OpenCatalog_Click(sender, e);    e.Handled = true; break;
            case System.Windows.Input.Key.M: OpenDbSettings_Click(sender, e); e.Handled = true; break;
        }
    }

    private void OpenClients_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new ClientsDialog { Owner = this };
        dlg.ShowDialog();
    }

    private void OpenCatalog_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new CatalogDialog { Owner = this };
        dlg.ShowDialog();
    }

    private void OpenDbSettings_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new DbSettingsDialog { Owner = this };
        if (dlg.ShowDialog() is true)
        {
            MessageBox.Show(
                "Postavke su spremljene. Restartajte aplikaciju da bi se nova konekcija primijenila.",
                "MySQL postavke", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void RefreshWorks_Click(object sender, RoutedEventArgs e)
    {
        ReloadWorks();
    }
}
