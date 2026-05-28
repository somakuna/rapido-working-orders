using System;
using System.Windows;
using MySqlConnector;
using RapidoWorkingOrders.Data;
using RapidoWorkingOrders.Models;
using RapidoWorkingOrders.Services;

namespace RapidoWorkingOrders.Dialogs;

public partial class DbSettingsDialog : Window
{
    public DbSettingsDialog(string? infoMessage = null)
    {
        InitializeComponent();

        var cfg = AppConfig.Load();
        ServerBox.Text       = cfg.Server;
        PortBox.Text         = cfg.Port.ToString();
        DatabaseBox.Text     = cfg.Database;
        UsernameBox.Text     = cfg.Username;
        PasswordBox.Password = cfg.Password;

        if (!string.IsNullOrWhiteSpace(infoMessage))
        {
            InfoText.Text        = infoMessage;
            InfoBanner.Visibility = System.Windows.Visibility.Visible;
        }

        LoadUsers();
    }

    private void LoadUsers()
    {
        try
        {
            using var db = new AppDbContext();
            UsersGrid.ItemsSource = db.Users
                .OrderBy(u => u.WindowsName)
                .ToList();
        }
        catch
        {
            // DB not yet configured — grid stays empty
        }
    }

    private AppConfig BuildConfig() => new()
    {
        Server   = ServerBox.Text.Trim(),
        Port     = int.TryParse(PortBox.Text.Trim(), out var p) ? p : 3306,
        Database = DatabaseBox.Text.Trim(),
        Username = UsernameBox.Text.Trim(),
        Password = PasswordBox.Password
    };

    private bool TryConnect(AppConfig cfg, out string error)
    {
        error = "";
        if (string.IsNullOrWhiteSpace(cfg.Server) || string.IsNullOrWhiteSpace(cfg.Database))
        {
            error = "Server i naziv baze podataka su obavezni.";
            return false;
        }
        try
        {
            var builder = new MySqlConnectionStringBuilder(cfg.GetConnectionString())
            {
                ConnectionTimeout = 5
            };
            using var conn = new MySqlConnection(builder.ToString());
            conn.Open();
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private void TestButton_Click(object sender, RoutedEventArgs e)
    {
        if (TryConnect(BuildConfig(), out var err))
        {
            MessageBox.Show("Konekcija je uspješna!", "Test",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        else
        {
            MessageBox.Show($"Konekcija nije uspjela:\n\n{err}", "Greška",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void AddUser_Click(object sender, RoutedEventArgs e)
    {
        var name = NewUserBox.Text.Trim();
        if (string.IsNullOrEmpty(name)) return;

        try
        {
            using var db = new AppDbContext();
            if (db.Users.Any(u => u.WindowsName == name))
            {
                MessageBox.Show($"Korisnik '{name}' već postoji.", "Korisnici",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            db.Users.Add(new User { WindowsName = name });
            db.SaveChanges();
            NewUserBox.Text = "";
            LoadUsers();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Greška pri dodavanju:\n{ex.Message}", "Greška",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SaveAliases_Click(object sender, RoutedEventArgs e)
    {
        if (UsersGrid.ItemsSource is not List<User> users) return;

        try
        {
            using var db = new AppDbContext();
            foreach (var u in users)
            {
                var target = db.Users.Find(u.Id);
                if (target == null) continue;
                target.ShowName = string.IsNullOrWhiteSpace(u.ShowName) ? null : u.ShowName.Trim();
            }
            db.SaveChanges();
            MessageBox.Show("Aliasi su spremljeni.", "Korisnici",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Greška pri spremanju:\n{ex.Message}", "Greška",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void RemoveUser_Click(object sender, RoutedEventArgs e)
    {
        if (UsersGrid.SelectedItem is not User user) return;

        var result = MessageBox.Show(
            $"Obrisati korisnika '{user.WindowsName}'?", "Brisanje korisnika",
            MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result != MessageBoxResult.Yes) return;

        try
        {
            using var db = new AppDbContext();
            var target = db.Users.Find(user.Id);
            if (target != null)
            {
                db.Users.Remove(target);
                db.SaveChanges();
            }
            LoadUsers();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Greška pri brisanju:\n{ex.Message}", "Greška",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        var cfg = BuildConfig();
        if (string.IsNullOrWhiteSpace(cfg.Server) || string.IsNullOrWhiteSpace(cfg.Database))
        {
            MessageBox.Show("Molim unesite server i naziv baze podataka.", "Greška",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        cfg.Save();
        App.Config = cfg;
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
