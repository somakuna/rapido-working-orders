using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RapidoWorkingOrders.Models;

/// <summary>
/// Predstavlja jednu PDF datoteku ili ručno dodani red u radnom nalogu.
/// </summary>
[Table("files")]
public class WorkFile
{
    [Key]
    public int Id { get; set; }

    [Column("work_id")]
    public int WorkId { get; set; }

    [Required]
    [MaxLength(500)]
    [Column("filename")]
    public string Filename { get; set; } = "";

    [Column("technology_id")]
    public int? TechnologyId { get; set; }

    [Column("material_id")]
    public int? MaterialId { get; set; }

    [Column("page_count")]
    public int PageCount { get; set; }

    [Column("m2", TypeName = "decimal(10,4)")]
    public decimal M2 { get; set; }

    [MaxLength(20)]
    public string Unit { get; set; } = "m2";

    [Column("amount", TypeName = "decimal(10,3)")]
    public decimal Amount { get; set; } = 1;

    [Column("total_m2", TypeName = "decimal(12,4)")]
    public decimal TotalM2 { get; set; }

    [MaxLength(1000)]
    [Column("note")]
    public string? Note { get; set; }

    [ForeignKey(nameof(WorkId))]
    public Work? Work { get; set; }

    [ForeignKey(nameof(TechnologyId))]
    public Technology? Technology { get; set; }

    [ForeignKey(nameof(MaterialId))]
    public Material? Material { get; set; }
}
