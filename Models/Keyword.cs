using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RapidoWorkingOrders.Models;

[Table("keywords")]
public class Keyword
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(255)]
    [Column("keyword")]
    public string Text { get; set; } = "";

    [Column("material_id")]
    public int? MaterialId { get; set; }

    [Column("technology_id")]
    public int? TechnologyId { get; set; }

    [ForeignKey(nameof(MaterialId))]
    public Material? Material { get; set; }

    [ForeignKey(nameof(TechnologyId))]
    public Technology? Technology { get; set; }
}
