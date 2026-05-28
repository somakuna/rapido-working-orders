using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using RapidoWorkingOrders.Models;

namespace RapidoWorkingOrders.ViewModels;

/// <summary>
/// ViewModel za jedan red u tablici fajlova (WorkFormDialog).
/// Implementira INotifyPropertyChanged za live update totala i prikaza.
/// </summary>
public class WorkFileRow : INotifyPropertyChanged
{
    // Statički lookupovi — postavljaju se jednom u WorkFormDialog konstruktoru
    public static List<Technology> AllTechs { get; set; } = new();
    public static List<Material> AllMats { get; set; } = new();

    public int Id { get; set; }

    private string _filename = "";
    public string Filename
    {
        get => _filename;
        set { _filename = value; OnProp(nameof(Filename)); }
    }

    private int? _technologyId;
    public int? TechnologyId
    {
        get => _technologyId;
        set
        {
            _technologyId = value;
            OnProp(nameof(TechnologyId));
            OnProp(nameof(TechnologyName));
        }
    }

    private int? _materialId;
    public int? MaterialId
    {
        get => _materialId;
        set
        {
            _materialId = value;
            OnProp(nameof(MaterialId));
            OnProp(nameof(MaterialName));
        }
    }

    private int _pageCount;
    public int PageCount
    {
        get => _pageCount;
        set { _pageCount = value; OnProp(nameof(PageCount)); Recalc(); }
    }

    private decimal _m2;
    public decimal M2
    {
        get => _m2;
        set { _m2 = value; OnProp(nameof(M2)); Recalc(); }
    }

    private string _unit = "m2";
    public string Unit
    {
        get => _unit;
        set { _unit = value; OnProp(nameof(Unit)); }
    }

    private decimal _amount = 1;
    public decimal Amount
    {
        get => _amount;
        set { _amount = value; OnProp(nameof(Amount)); Recalc(); }
    }

    private decimal _totalM2;
    public decimal TotalM2
    {
        get => _totalM2;
        set { _totalM2 = value; OnProp(nameof(TotalM2)); }
    }

    private string? _note;
    public string? Note
    {
        get => _note;
        set { _note = value; OnProp(nameof(Note)); }
    }

    // Computed display — razrješavaju se iz statičkih lookupova
    public string TechnologyName => AllTechs.FirstOrDefault(t => t.Id == TechnologyId)?.Name ?? "";
    public string MaterialName   => AllMats.FirstOrDefault(m => m.Id == MaterialId)?.Name ?? "";

    private void Recalc() => TotalM2 = Math.Round(M2 * Amount, 2);

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnProp(string n) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

    public WorkFileRow Clone() => new()
    {
        Id           = 0,
        Filename     = Filename,
        TechnologyId = TechnologyId,
        MaterialId   = MaterialId,
        PageCount    = PageCount,
        M2           = M2,
        Unit         = Unit,
        Amount       = Amount,
        TotalM2      = TotalM2,
        Note         = Note,
    };

    public static WorkFileRow FromModel(WorkFile f) => new()
    {
        Id           = f.Id,
        Filename     = f.Filename,
        TechnologyId = f.TechnologyId,
        MaterialId   = f.MaterialId,
        PageCount    = f.PageCount,
        M2           = f.M2,
        Unit         = f.Unit,
        Amount       = f.Amount,
        TotalM2      = f.TotalM2,
        Note         = f.Note,
    };

    public WorkFile ToModel(int workId) => new()
    {
        Id           = Id,
        WorkId       = workId,
        Filename     = Filename,
        TechnologyId = TechnologyId,
        MaterialId   = MaterialId,
        PageCount    = PageCount,
        M2           = M2,
        Unit         = Unit,
        Amount       = Amount,
        TotalM2      = TotalM2,
        Note         = Note,
    };
}
