using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;
using RapidoWorkingOrders.Data;
using RapidoWorkingOrders.Models;
using RapidoWorkingOrders.Services;
using RapidoWorkingOrders.ViewModels;

namespace RapidoWorkingOrders.Dialogs;

public partial class WorkFormDialog : Window
{
    // Lookupovi za ComboBox kolone u DataGridu (DataContext = this)
    public List<Technology>           Technologies          { get; } = new();
    public List<Material>             Materials             { get; } = new();
    public List<FinishingOptionCheck> AllFinishingOptions   { get; } = new();

    private readonly ObservableCollection<WorkFileRow> _files = new();
    private readonly bool _isEdit;
    private int _workId;

    public int SavedWorkId { get; private set; }

    // ──────────────────────────────────────────────────────────────────
    // Konstruktori

    public WorkFormDialog()
    {
        InitializeComponent();
        DataContext = this;
        _isEdit = false;
        LoadLookups();
        FillNew();
    }

    public WorkFormDialog(Work work, bool isDuplicate = false)
    {
        InitializeComponent();
        DataContext = this;
        LoadLookups();

        if (isDuplicate)
        {
            _isEdit = false;
            FillDuplicate(work);
        }
        else
        {
            _isEdit = true;
            _workId = work.Id;
            FillEdit(work);
        }
    }

    // ──────────────────────────────────────────────────────────────────
    // Init

    private void LoadLookups()
    {
        using var db = new AppDbContext();
        Technologies.AddRange(db.Technologies.OrderBy(t => t.Name).ToList());
        Materials.AddRange(db.Materials.OrderBy(m => m.Name).ToList());

        WorkFileRow.AllTechs = Technologies;
        WorkFileRow.AllMats  = Materials;

        var clients = db.Clients.OrderBy(c => c.Name).ToList();
        var users   = db.Users.AsEnumerable().OrderBy(u => u.Name).ToList();

        ClientCombo.ItemsSource = clients;
        UserCombo.ItemsSource   = users;

        // Finishing options
        var options = db.FinishingOptions
            .OrderBy(f => f.SortOrder)
            .ThenBy(f => f.Name)
            .ToList();
        foreach (var fo in options)
            AllFinishingOptions.Add(FinishingOptionCheck.FromModel(fo));
        FinishingOptionsPanel.ItemsSource = AllFinishingOptions;

        FilesGrid.ItemsSource = _files;
        _files.CollectionChanged += (_, args) =>
        {
            if (args.NewItems != null)
                foreach (WorkFileRow row in args.NewItems)
                    row.PropertyChanged += (_, _) => UpdateTotals();
            UpdateTotals();
        };
    }

    private void FillNew()
    {
        Title = "Radni nalog — Novi";
        FormTitleText.Text = "Novi radni nalog";
        using var db = new AppDbContext();
        int year   = DateTime.Today.Year;
        int number = db.NextWorkNumber(year);

        WorkNumberBox.Text = number.ToString();
        WorkYearBox.Text   = year.ToString();
        WorkDatePicker.SelectedDate = DateTime.Today;

        if (App.CurrentUser != null)
            UserCombo.SelectedValue = App.CurrentUser.Id;
    }

    private void FillEdit(Work w)
    {
        Title = $"Radni nalog — Ispravak {w.FormattedNumber}";
        FormTitleText.Text = $"Ispravak naloga  {w.FormattedNumber}";

        WorkNumberBox.Text = w.WorkNumber.ToString();
        WorkYearBox.Text   = w.WorkYear.ToString();
        WorkDatePicker.SelectedDate     = w.WorkDate;
        OrderDatePicker.SelectedDate    = w.OrderDate;
        DeliveryDatePicker.SelectedDate = w.DeliveryDate;
        OfferNumberBox.Text = w.OfferNumber?.ToString() ?? "";
        LocationBox.Text    = w.Location ?? "";
        DeliveryBox.Text    = w.Delivery ?? "";

        SetComboByValue(OrderMethodCombo, w.OrderMethod);
        ClientCombo.SelectedValue = w.ClientId;
        UserCombo.SelectedValue   = w.UserId;

        using var db = new AppDbContext();
        var dbFiles = db.Files.Where(f => f.WorkId == w.Id).ToList();
        foreach (var f in dbFiles)
            _files.Add(WorkFileRow.FromModel(f));

        // Odabrane završne obrade
        var selected = db.WorkFinishingOptions
            .Where(wfo => wfo.WorkId == w.Id)
            .Select(wfo => wfo.FinishingOptionId)
            .ToHashSet();
        foreach (var fo in AllFinishingOptions)
            fo.IsChecked = selected.Contains(fo.Id);

        UpdateTotals();
    }

    private void FillDuplicate(Work w)
    {
        Title = "Radni nalog — Dupliciranje";
        FormTitleText.Text = "Dupliciranje — novi radni nalog";
        using var db = new AppDbContext();
        int year   = DateTime.Today.Year;
        int number = db.NextWorkNumber(year);

        WorkNumberBox.Text = number.ToString();
        WorkYearBox.Text   = year.ToString();
        WorkDatePicker.SelectedDate = DateTime.Today;

        OfferNumberBox.Text = w.OfferNumber?.ToString() ?? "";
        LocationBox.Text    = w.Location ?? "";
        DeliveryBox.Text    = w.Delivery ?? "";

        SetComboByValue(OrderMethodCombo, w.OrderMethod);
        ClientCombo.SelectedValue = w.ClientId;
        UserCombo.SelectedValue   = w.UserId;

        var dbFiles = db.Files.Where(f => f.WorkId == w.Id).ToList();
        foreach (var f in dbFiles)
        {
            var row = WorkFileRow.FromModel(f);
            row.Id = 0;
            _files.Add(row);
        }

        // Kopiraj odabrane završne obrade iz originalnog naloga
        var selected = db.WorkFinishingOptions
            .Where(wfo => wfo.WorkId == w.Id)
            .Select(wfo => wfo.FinishingOptionId)
            .ToHashSet();
        foreach (var fo in AllFinishingOptions)
            fo.IsChecked = selected.Contains(fo.Id);

        UpdateTotals();
    }

    // ──────────────────────────────────────────────────────────────────
    // Fajlovi

    private void FilesGrid_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void FilesGrid_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        var paths = (string[])e.Data.GetData(DataFormats.FileDrop)!;
        foreach (var p in paths)
            if (p.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                AddPdfFile(p);
    }

    private void AddPdfFiles_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Filter      = "PDF datoteke (*.pdf)|*.pdf",
            Multiselect = true,
            Title       = "Odaberi PDF datoteke",
        };
        if (dlg.ShowDialog() != true) return;
        foreach (var f in dlg.FileNames)
            AddPdfFile(f);
    }

    private void AddPdfFile(string path)
    {
        var (pageCount, m2) = PdfReaderService.ReadPdf(path);
        Keyword? kw = null;
        try
        {
            using var db = new AppDbContext();
            kw = db.FindKeywordMatch(Path.GetFileName(path));
        }
        catch { /* nema keywordova */ }

        var row = new WorkFileRow
        {
            Filename     = Path.GetFileName(path),
            PageCount    = pageCount,
            M2           = m2,
            Amount       = 1,
            TechnologyId = kw?.TechnologyId,
            MaterialId   = kw?.MaterialId,
        };
        row.TotalM2 = row.M2 * row.Amount;
        _files.Add(row);
        UpdateTotals();
    }

    private void AddRow_Click(object sender, RoutedEventArgs e)
    {
        _files.Add(new WorkFileRow { Filename = "", Amount = 1 });
        UpdateTotals();
        FilesGrid.SelectedIndex = _files.Count - 1;
        FilesGrid.ScrollIntoView(FilesGrid.SelectedItem);
    }

    private void RemoveRow_Click(object sender, RoutedEventArgs e)
    {
        var toRemove = FilesGrid.SelectedItems.OfType<WorkFileRow>().ToList();
        foreach (var row in toRemove)
            _files.Remove(row);
        if (toRemove.Count > 0)
            UpdateTotals();
    }

    private void FilesGrid_ContextMenuOpening(object sender, System.Windows.Controls.ContextMenuEventArgs e)
    {
        var dep = e.OriginalSource as DependencyObject;
        while (dep != null && dep is not System.Windows.Controls.DataGridRow)
            dep = System.Windows.Media.VisualTreeHelper.GetParent(dep);

        if (dep is System.Windows.Controls.DataGridRow dgRow)
        {
            if (!dgRow.IsSelected)
                FilesGrid.SelectedItem = dgRow.Item;
            if (FilesGrid.ContextMenu?.Items[0] is System.Windows.Controls.MenuItem menuDuplicate)
                menuDuplicate.IsEnabled = FilesGrid.SelectedItems.Count == 1;
        }
        else
        {
            e.Handled = true;
        }
    }

    private void DuplicateRow_Click(object sender, RoutedEventArgs e)
    {
        if (FilesGrid.SelectedItem is not WorkFileRow row) return;
        int index = _files.IndexOf(row);
        var clone = row.Clone();
        _files.Insert(index + 1, clone);
        FilesGrid.SelectedItem = clone;
        FilesGrid.ScrollIntoView(clone);
    }

    private void FilesGrid_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Delete && FilesGrid.CurrentCell.Column is not null && !FilesGrid.IsEditing())
        {
            var toRemove = FilesGrid.SelectedItems.OfType<WorkFileRow>().ToList();
            foreach (var row in toRemove)
                _files.Remove(row);
            if (toRemove.Count > 0)
                UpdateTotals();
            e.Handled = true;
        }
    }

    private void UpdateTotals()
    {
        int     totalPages = _files.Sum(f => f.PageCount);
        decimal totalM2    = _files.Sum(f => f.M2);
        decimal totalAmt   = _files.Sum(f => f.Amount);
        decimal totalTotal = _files.Sum(f => f.TotalM2);

        TotalPagesText.Text   = totalPages.ToString();
        TotalM2Text.Text      = totalM2.ToString("F2");
        TotalAmountText.Text  = totalAmt.ToString("F3");
        TotalTotalM2Text.Text = totalTotal.ToString("F2");
    }

    // ──────────────────────────────────────────────────────────────────
    // Spremanje

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (TrySave(out int savedId))
        {
            SavedWorkId  = savedId;
            DialogResult = true;
            Close();
        }
    }

    private void SaveAndPrint_Click(object sender, RoutedEventArgs e)
    {
        if (!TrySave(out int savedId)) return;
        SavedWorkId = savedId;

        try
        {
            Work fullWork;
            List<WorkFile> files;
            using (var db = new AppDbContext())
            {
                fullWork = db.Works
                    .Include(w => w.Client)
                    .Include(w => w.User)
                    .Include(w => w.SelectedFinishings!).ThenInclude(wfo => wfo.FinishingOption)
                    .First(w => w.Id == savedId);
                files = db.Files
                    .Include(f => f.Technology)
                    .Include(f => f.Material)
                    .Where(f => f.WorkId == savedId)
                    .ToList();
            }
            var printDlg = new PrintPreviewDialog(fullWork, files) { Owner = this };
            printDlg.ShowDialog();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Greška pri otvaranju Print dijaloga:\n{ex.Message}",
                "Greška", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        DialogResult = true;
        Close();
    }

    private bool TrySave(out int savedId)
    {
        savedId = 0;

        if (!int.TryParse(WorkNumberBox.Text.Trim(), out int workNum) || workNum <= 0)
        {
            MessageBox.Show("Broj naloga mora biti pozitivan cijeli broj.",
                "Greška", MessageBoxButton.OK, MessageBoxImage.Warning);
            WorkNumberBox.Focus();
            return false;
        }

        try
        {
            using var db = new AppDbContext();
            var checkedIds = AllFinishingOptions
                .Where(f => f.IsChecked)
                .Select(f => f.Id)
                .ToList();

            if (_isEdit)
            {
                var w = db.Works.Include(x => x.Files).First(x => x.Id == _workId);

                if (db.Works.Any(x => x.WorkYear == w.WorkYear && x.WorkNumber == workNum && x.Id != _workId))
                {
                    MessageBox.Show($"Radni nalog {workNum}/{w.WorkYear} već postoji.",
                        "Greška", MessageBoxButton.OK, MessageBoxImage.Warning);
                    WorkNumberBox.Focus();
                    return false;
                }

                w.WorkNumber = workNum;
                FillWorkModel(w);
                db.Files.RemoveRange(w.Files!);
                foreach (var row in _files)
                    db.Files.Add(row.ToModel(w.Id));

                db.WorkFinishingOptions.RemoveRange(
                    db.WorkFinishingOptions.Where(wfo => wfo.WorkId == w.Id));
                foreach (var id in checkedIds)
                    db.WorkFinishingOptions.Add(new WorkFinishingOption
                        { WorkId = w.Id, FinishingOptionId = id });

                db.SaveChanges();
                savedId = w.Id;
            }
            else
            {
                int year = int.Parse(WorkYearBox.Text);

                if (db.Works.Any(x => x.WorkYear == year && x.WorkNumber == workNum))
                {
                    MessageBox.Show($"Radni nalog {workNum}/{year} već postoji.",
                        "Greška", MessageBoxButton.OK, MessageBoxImage.Warning);
                    WorkNumberBox.Focus();
                    return false;
                }

                var w = new Work { WorkYear = year, WorkNumber = workNum };
                FillWorkModel(w);
                db.Works.Add(w);
                db.SaveChanges();

                foreach (var row in _files)
                    db.Files.Add(row.ToModel(w.Id));
                foreach (var id in checkedIds)
                    db.WorkFinishingOptions.Add(new WorkFinishingOption
                        { WorkId = w.Id, FinishingOptionId = id });
                db.SaveChanges();
                savedId = w.Id;
            }
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Greška pri spremanju:\n{ex.Message}",
                "Greška", MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
    }

    private void FillWorkModel(Work w)
    {
        w.WorkDate     = WorkDatePicker.SelectedDate;
        w.OrderDate    = OrderDatePicker.SelectedDate;
        w.DeliveryDate = DeliveryDatePicker.SelectedDate;
        w.Location     = NullIfEmpty(LocationBox.Text);
        w.Delivery     = NullIfEmpty(DeliveryBox.Text);
        w.OfferNumber  = int.TryParse(OfferNumberBox.Text, out int on) ? on : null;
        w.OrderMethod  = GetComboText(OrderMethodCombo);
        w.ClientId     = ClientCombo.SelectedValue as int?;
        w.UserId       = UserCombo.SelectedValue as int?;
        w.UpdatedAt    = DateTime.Now;
    }

    // ──────────────────────────────────────────────────────────────────
    // Helpers

    private static string? NullIfEmpty(string s) =>
        string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static string? GetComboText(System.Windows.Controls.ComboBox cb)
    {
        if (cb.SelectedItem is System.Windows.Controls.ComboBoxItem item)
            return NullIfEmpty(item.Content?.ToString() ?? "");
        return NullIfEmpty(cb.Text);
    }

    private static void SetComboByValue(System.Windows.Controls.ComboBox cb, string? value)
    {
        foreach (var item in cb.Items)
        {
            if (item is System.Windows.Controls.ComboBoxItem ci
                && ci.Content?.ToString() == value)
            {
                cb.SelectedItem = ci;
                return;
            }
        }
    }
}

internal static class DataGridExtensions
{
    internal static bool IsEditing(this System.Windows.Controls.DataGrid grid)
    {
        return grid.Items.Cast<object>().Any(item =>
        {
            var row = (System.Windows.Controls.DataGridRow?)
                grid.ItemContainerGenerator.ContainerFromItem(item);
            return row?.IsEditing == true;
        });
    }
}
