using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EventEase.Models
{
    public class Event
    {
        [Key]
        public int EventId { get; set; }

        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        [Required]
        [Display(Name = "Event Type")]
        public int EventTypeId { get; set; }

        [Display(Name = "Planned Start Date & Time")]
        public DateTime? PlannedStartDate { get; set; }

        [Display(Name = "Planned End Date & Time")]
        public DateTime? PlannedEndDate { get; set; }

        // --- NOT MAPPED: Used only for the form UI ---
        [NotMapped]
        [Display(Name = "Planned Start Date")]
        [DataType(DataType.Date)]
        public DateTime? PlannedStartDateOnly { get; set; }

        [NotMapped]
        [Display(Name = "Planned Start Time")]
        [DataType(DataType.Time)]
        public TimeSpan? PlannedStartTime { get; set; }

        [NotMapped]
        [Display(Name = "Planned End Date")]
        [DataType(DataType.Date)]
        public DateTime? PlannedEndDateOnly { get; set; }

        [NotMapped]
        [Display(Name = "Planned End Time")]
        [DataType(DataType.Time)]
        public TimeSpan? PlannedEndTime { get; set; }

        [ForeignKey(nameof(EventTypeId))]
        public EventType? EventType { get; set; }

        public ICollection<Booking>? Bookings { get; set; }
    }
}