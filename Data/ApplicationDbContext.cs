using EventEase.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace EventEase.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Venue> Venues { get; set; }
        public DbSet<Event> Events { get; set; }
        public DbSet<Booking> Bookings { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure Venue
            modelBuilder.Entity<Venue>(entity =>
            {
                entity.HasKey(e => e.VenueId);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
                entity.Property(e => e.Location).IsRequired().HasMaxLength(300);
                entity.Property(e => e.Capacity).IsRequired();
                entity.Property(e => e.ImageUrl).HasMaxLength(500);
            });

            // Configure Event
            modelBuilder.Entity<Event>(entity =>
            {
                entity.HasKey(e => e.EventId);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            });

            // Configure Booking
            modelBuilder.Entity<Booking>(entity =>
            {
                entity.HasKey(e => e.BookingId);
                entity.Property(e => e.StartDateTime).IsRequired();
                entity.Property(e => e.EndDateTime).IsRequired();
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETDATE()");
                entity.Property(e => e.Status).HasMaxLength(50).HasDefaultValue("Confirmed");

                // Configure relationships
                entity.HasOne(e => e.Venue)
                    .WithMany()
                    .HasForeignKey(e => e.VenueId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.Event)
                    .WithMany()
                    .HasForeignKey(e => e.EventId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
