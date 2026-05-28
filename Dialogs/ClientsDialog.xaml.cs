using System;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using RapidoWorkingOrders.Data;
using RapidoWorkingOrders.Models;

namespace RapidoWorkingOrders.Dialogs;

public partial class ClientsDialog : Window
{
    private int? _editingId;

    public ClientsDialog()
    {
        InitializeComponent();
        Reload();
    }

    private void Reload()
    {
        try
        {
            using var db = new AppDbContext();
            ClientsGrid.ItemsSource = db.Clients.OrderBy(c => c.Name).ToList();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Greška pri učitavanju:\n{ex.Message}", "Greška",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ClientsGrid_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (ClientsGrid.SelectedItem is not Client c) return;
        _editingId = c.Id;
        NameBox.Text    = c.Name;
        OibBox.Text     = c.Oib ?? "";
        EmailBox.Text   = c.Email ?? "";
        ContactBox.Text = c.Contact ?? "";
        AddressBox.Text = c.Address ?? "";
    }

    private void NewClient_Click(object sender, RoutedEventArgs e)
    {
        _editingId = null;
        ClientsGrid.SelectedItem = null;
        NameBox.Text = OibBox.Text = EmailBox.Text = ContactBox.Text = AddressBox.Text = "";
        NameBox.Focus();
    }

    private void SaveClient_Click(object sender, RoutedEventArgs e)
    {
        var name = NameBox.Text.Trim();
        if (string.IsNullOrEmpty(name))
        {
            MessageBox.Show("Naziv klijenta ne može biti prazan.", "Validacija",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            using var db = new AppDbContext();
            Client client;
            if (_editingId.HasValue)
            {
                client = db.Clients.Find(_editingId.Value)
                    ?? throw new Exception("Klijent nije pronađen.");
            }
            else
            {
                client = new Client();
                db.Clients.Add(client);
            }

            client.Name    = name;
            client.Oib     = NullIfEmpty(OibBox.Text);
            client.Email   = NullIfEmpty(EmailBox.Text);
            client.Contact = NullIfEmpty(ContactBox.Text);
            client.Address = NullIfEmpty(AddressBox.Text);
            db.SaveChanges();
            Reload();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Greška pri spremanju:\n{ex.Message}", "Greška",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void DeleteClient_Click(object sender, RoutedEventArgs e)
    {
        if (!_editingId.HasValue)
        {
            MessageBox.Show("Odaberite klijenta za brisanje.", "Brisanje");
            return;
        }

        var res = MessageBox.Show("Sigurno obrisati klijenta?", "Brisanje",
            MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (res != MessageBoxResult.Yes) return;

        try
        {
            using var db = new AppDbContext();
            var c = db.Clients.Find(_editingId.Value);
            if (c != null) { db.Clients.Remove(c); db.SaveChanges(); }
            _editingId = null;
            NameBox.Text = OibBox.Text = EmailBox.Text = ContactBox.Text = AddressBox.Text = "";
            Reload();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Greška pri brisanju:\n{ex.Message}", "Greška",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private static string? NullIfEmpty(string s) =>
        string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
