using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RapidoWorkingOrders.Models;

[Table("finishing_options")]
public class FinishingOption
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    [Column("name")]
    public string Name { get; set; } = "";

    [Column("sort_order")]
    public int SortOrder { get; set; }
}
