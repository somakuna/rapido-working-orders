using System;
using System.Linq;
using System.Windows;
using QuestPDF.Infrastructure;
using RapidoWorkingOrders.Data;
using RapidoWorkingOrders.Dialogs;
using RapidoWorkingOrders.Services;

namespace RapidoWorkingOrders;

public partial class App : Application
{
    public static AppConfig Config { get; set; } = null!;
    public static Models.User? CurrentUser { get; set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += (_, args) =>
        {
            MessageBox.Show(
                $"Neočekivana greška:\n\n{args.Exception.Message}\n\n{args.Exception.StackTrace}",
                "Kritična greška", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };

        QuestPDF.Settings.License = LicenseType.Community;

        Config = AppConfig.Load();

        if (!TryConnect())
        {
            var dlg = new DbSettingsDialog("Konekcija na bazu podataka nije uspjela. Unesite podatke za spajanje na MySQL server.");
            if (dlg.ShowDialog() is not true)
            {
                Shutdown();
                return;
            }
            if (!TryConnect())
            {
                MessageBox.Show(
                    "Otvaranje baze nije uspjelo. Aplikacija će se zatvoriti.",
                    "Greška", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
                return;
            }
        }

        new Views.MainWindow().Show();
    }

    private static bool TryConnect()
    {
        if (!Config.IsConfigured)
            return false;

        try
        {
            using var db = new AppDbContext();
            db.Database.EnsureCreated();

            var winName = Environment.UserName;
            var user = db.Users.FirstOrDefault(u => u.WindowsName == winName);
            if (user == null)
            {
                user = new Models.User { WindowsName = winName };
                db.Users.Add(user);
                db.SaveChanges();
            }
            CurrentUser = user;

            if (!db.FinishingOptions.Any())
            {
                string[] defaults = ["Montaža", "Vanjska usluga"];
                for (int i = 0; i < defaults.Length; i++)
                    db.FinishingOptions.Add(new Models.FinishingOption { Name = defaults[i], SortOrder = i });
                db.SaveChanges();
            }
            return true;
        }
        catch
        {
            return false;
        }
    }
}
