using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace RapidoWorkingOrders.Models;

[Table("works")]
[Index(nameof(WorkNumber), nameof(WorkYear), IsUnique = true, Name = "uniq_work_year")]
public class Work
{
    [Key]
    public int Id { get; set; }

    [Column("work_number")]
    public int WorkNumber { get; set; }

    [Column("work_year")]
    public int WorkYear { get; set; }

    [Column("work_date")]
    public DateTime? WorkDate { get; set; }

    [Column("offer_number")]
    public int? OfferNumber { get; set; }

    [Column("order_method")]
    [MaxLength(20)]
    public string? OrderMethod { get; set; }

    [Column("order_date")]
    public DateTime? OrderDate { get; set; }

    [Column("delivery_date")]
    public DateTime? DeliveryDate { get; set; }

    [Column("delivery", TypeName = "text")]
    public string? Delivery { get; set; }

    [Column("location", TypeName = "text")]
    public string? Location { get; set; }

    // ── Foreign keys ──
    [Column("client_id")]
    public int? ClientId { get; set; }

    [Column("user_id")]
    public int? UserId { get; set; }

    [ForeignKey(nameof(ClientId))]
    public Client? Client { get; set; }

    [ForeignKey(nameof(UserId))]
    public User? User { get; set; }

    // ── Timestamps ──
    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    // ── Navigacijske veze ──
    public ICollection<WorkFile>? Files { get; set; }
    public ICollection<WorkFinishingOption>? SelectedFinishings { get; set; }

    // ── Helper za prikaz ──
    [NotMapped]
    public string FormattedNumber => $"{WorkNumber}/{WorkYear}";
}
