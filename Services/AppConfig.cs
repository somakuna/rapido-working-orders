using System;
using System.IO;
using System.Text.Json;

namespace RapidoWorkingOrders.Services;

public class AppConfig
{
    public string Server   { get; set; } = "localhost";
    public int    Port     { get; set; } = 3306;
    public string Database { get; set; } = "";
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private static readonly string ConfigPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".rapido_working_orders.json"
    );

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Server) && !string.IsNullOrWhiteSpace(Database);

    public static AppConfig Load()
    {
        if (!File.Exists(ConfigPath))
            return new AppConfig();

        try
        {
            var json = File.ReadAllText(ConfigPath);
            return JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
        }
        catch
        {
            return new AppConfig();
        }
    }

    public void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(this, JsonOptions);
            File.WriteAllText(ConfigPath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Config save failed: {ex.Message}");
        }
    }

    public string GetConnectionString() =>
        $"Server={Server};Port={Port};Database={Database};User={Username};Password={Password};AllowPublicKeyRetrieval=true;SslMode=None;";
}
