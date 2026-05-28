using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RapidoWorkingOrders.Models;

[Table("clients")]
public class Client
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(255)]
    public string Name { get; set; } = "";

    [Column("oib")]
    [MaxLength(20)]
    public string? Oib { get; set; }

    [Column("address")]
    [MaxLength(500)]
    public string? Address { get; set; }

    [MaxLength(255)]
    public string? Email { get; set; }

    [MaxLength(100)]
    public string? Contact { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    // Navigacijska veza prema radnim nalozima
    public ICollection<Work>? Works { get; set; }
}
