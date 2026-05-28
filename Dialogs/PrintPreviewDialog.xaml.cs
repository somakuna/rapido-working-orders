using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows;
using Microsoft.Win32;
using RapidoWorkingOrders.Models;
using RapidoWorkingOrders.Services;

namespace RapidoWorkingOrders.Dialogs;

public partial class PrintPreviewDialog : Window
{
    private readonly Work _work;
    private readonly List<WorkFile> _files;

    public PrintPreviewDialog(Work work, List<WorkFile> files)
    {
        InitializeComponent();
        _work  = work;
        _files = files;
        TitleText.Text = $"Radni nalog  {work.FormattedNumber}";
    }

    private string BuildTempPath(bool a5)
    {
        var safe = _work.FormattedNumber.Replace("/", "_");
        var fmt  = a5 ? "A5" : "A4";
        return Path.Combine(Path.GetTempPath(), $"RNalog_{safe}_{fmt}.pdf");
    }

    private bool TryGenerate(out string path)
    {
        bool a5 = RbA5.IsChecked == true;
        path = BuildTempPath(a5);
        try
        {
            WorkOrderPdfGenerator.Generate(_work, _files, path, a5);
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Greška pri generiranju PDF-a:\n{ex.Message}",
                "PDF greška", MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
    }

    private void OpenPdf_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGenerate(out var path)) return;
        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Nije moguće otvoriti PDF:\n{ex.Message}",
                "Greška", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SavePdf_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGenerate(out var tempPath)) return;

        var dlg = new SaveFileDialog
        {
            Filter   = "PDF datoteke (*.pdf)|*.pdf",
            FileName = Path.GetFileName(tempPath),
            Title    = "Spremi radni nalog kao PDF",
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            File.Copy(tempPath, dlg.FileName, overwrite: true);
            MessageBox.Show("PDF je uspješno spremljen.", "Spremi",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Greška pri spremanju:\n{ex.Message}",
                "Greška", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
