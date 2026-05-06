using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

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
        public string? ImageUrl { get; set; }

        //Add I from file property to recieve images from browser.
        [NotMapped]
        [Display(Name = "Upload Venue Image")]
        public IFormFile? ImageFile { get; set; }

        // Navigation Property
        public ICollection<Booking>? Bookings { get; set; }
    }
}
