using Microsoft.EntityFrameworkCore;
using Pomelo.EntityFrameworkCore.MySql;
using RapidoWorkingOrders.Models;

namespace RapidoWorkingOrders.Data;

public class AppDbContext : DbContext
{
    public DbSet<User>                 Users             => Set<User>();
    public DbSet<Client>               Clients           => Set<Client>();
    public DbSet<Material>             Materials         => Set<Material>();
    public DbSet<Technology>           Technologies      => Set<Technology>();
    public DbSet<Keyword>              Keywords          => Set<Keyword>();
    public DbSet<Work>                 Works             => Set<Work>();
    public DbSet<WorkFile>             Files             => Set<WorkFile>();
    public DbSet<FinishingOption>      FinishingOptions  => Set<FinishingOption>();
    public DbSet<WorkFinishingOption>  WorkFinishingOptions => Set<WorkFinishingOption>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        var serverVersion = new MySqlServerVersion(new Version(8, 0, 21));
        optionsBuilder.UseMySql(App.Config.GetConnectionString(), serverVersion);
    }

    protected override void OnModelCreating(ModelBuilder mb)
    {
        base.OnModelCreating(mb);

        // ── UNIQUE constrainti ──
        mb.Entity<User>().HasIndex(u => u.WindowsName).IsUnique();
        mb.Entity<Material>().HasIndex(m => m.Name).IsUnique();
        mb.Entity<Technology>().HasIndex(t => t.Name).IsUnique();
        mb.Entity<Keyword>().HasIndex(k => k.Text).IsUnique();

        // ── Cascade delete za fajlove kad se obriše radni nalog ──
        mb.Entity<WorkFile>()
            .HasOne(f => f.Work)
            .WithMany(w => w.Files)
            .HasForeignKey(f => f.WorkId)
            .OnDelete(DeleteBehavior.Cascade);

        // ── SetNull za client_id / user_id kad se obriše parent ──
        mb.Entity<Work>()
            .HasOne(w => w.Client)
            .WithMany(c => c.Works)
            .HasForeignKey(w => w.ClientId)
            .OnDelete(DeleteBehavior.SetNull);

        mb.Entity<Work>()
            .HasOne(w => w.User)
            .WithMany()
            .HasForeignKey(w => w.UserId)
            .OnDelete(DeleteBehavior.SetNull);

        mb.Entity<Keyword>()
            .HasOne(k => k.Material)
            .WithMany()
            .HasForeignKey(k => k.MaterialId)
            .OnDelete(DeleteBehavior.SetNull);

        mb.Entity<Keyword>()
            .HasOne(k => k.Technology)
            .WithMany()
            .HasForeignKey(k => k.TechnologyId)
            .OnDelete(DeleteBehavior.SetNull);

        mb.Entity<WorkFile>()
            .HasOne(f => f.Material)
            .WithMany()
            .HasForeignKey(f => f.MaterialId)
            .OnDelete(DeleteBehavior.SetNull);

        mb.Entity<WorkFile>()
            .HasOne(f => f.Technology)
            .WithMany()
            .HasForeignKey(f => f.TechnologyId)
            .OnDelete(DeleteBehavior.SetNull);

        // ── WorkFinishingOption relacije ──
        mb.Entity<WorkFinishingOption>()
            .HasOne(wfo => wfo.Work)
            .WithMany(w => w.SelectedFinishings)
            .HasForeignKey(wfo => wfo.WorkId)
            .OnDelete(DeleteBehavior.Cascade);

        mb.Entity<WorkFinishingOption>()
            .HasOne(wfo => wfo.FinishingOption)
            .WithMany()
            .HasForeignKey(wfo => wfo.FinishingOptionId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    public int NextWorkNumber(int year)
    {
        var max = Works
            .Where(w => w.WorkYear == year)
            .Select(w => (int?)w.WorkNumber)
            .Max() ?? 0;
        return max + 1;
    }

    public Keyword? FindKeywordMatch(string filename)
    {
        var lower = filename.ToLowerInvariant();
        var allKeywords = Keywords
            .Include(k => k.Material)
            .Include(k => k.Technology)
            .ToList();
        return allKeywords.FirstOrDefault(k =>
            !string.IsNullOrEmpty(k.Text) && lower.Contains(k.Text));
    }
}
