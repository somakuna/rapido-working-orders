using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RapidoWorkingOrders.Models;

[Table("users")]
public class User
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(255)]
    [Column("windows_name")]
    public string WindowsName { get; set; } = "";

    [MaxLength(255)]
    [Column("show_name")]
    public string? ShowName { get; set; }

    [NotMapped]
    public string Name => !string.IsNullOrWhiteSpace(ShowName) ? ShowName : WindowsName;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
