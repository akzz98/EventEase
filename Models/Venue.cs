using System.ComponentModel.DataAnnotations;

namespace EventEase.Models
{
    public class Venue
    {
        [Key]
        public int VenueId { get; set; }

        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [MaxLength(300)]
        public string Location { get; set; } = string.Empty;

        [Required]
        public int Capacity { get; set; }

        public string? Description { get; set; }

        [MaxLength(500)]
        [Display(Name = "Venue Image URL")]
        public string? ImageUrl { get; set; } = "https://images.unsplash.com/photo-1519167758481-83f550bb49b3?auto=format&fit=crop&w=500&q=60";

        // Navigation Property
        public ICollection<Booking>? Bookings { get; set; }
    }
}
