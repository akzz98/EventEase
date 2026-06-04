using System.ComponentModel.DataAnnotations;

namespace EventEase.Models
{
    public class EventType
    {
        [Key]
        public int EventTypeId { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        // Navigation property
        public ICollection<Event>? Events { get; set; }
    }
}
