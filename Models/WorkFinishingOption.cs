using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RapidoWorkingOrders.Models;

[Table("work_finishing_options")]
public class WorkFinishingOption
{
    [Key]
    public int Id { get; set; }

    [Column("work_id")]
    public int WorkId { get; set; }

    [Column("finishing_option_id")]
    public int FinishingOptionId { get; set; }

    [ForeignKey(nameof(WorkId))]
    public Work? Work { get; set; }

    [ForeignKey(nameof(FinishingOptionId))]
    public FinishingOption? FinishingOption { get; set; }
}
