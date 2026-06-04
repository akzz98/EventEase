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
        public DbSet<EventType> EventTypes { get; set; }

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

            // Configure EventType
            modelBuilder.Entity<EventType>(entity =>
            {
                entity.HasKey(e => e.EventTypeId);
                entity.Property(e => e.EventTypeId).ValueGeneratedNever();
                entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
                entity.HasIndex(e => e.Name).IsUnique();

                entity.HasData(
                    new EventType { EventTypeId = 1, Name = "Conference" },
                    new EventType { EventTypeId = 2, Name = "Wedding" },
                    new EventType { EventTypeId = 3, Name = "Corporate Meeting" },
                    new EventType { EventTypeId = 4, Name = "Concert" },
                    new EventType { EventTypeId = 5, Name = "Workshop" },
                    new EventType { EventTypeId = 6, Name = "Private Party" },
                    new EventType { EventTypeId = 7, Name = "Sports" },
                    new EventType { EventTypeId = 8, Name = "Exhibition" },
                    new EventType { EventTypeId = 9, Name = "Festival" },
                    new EventType { EventTypeId = 10, Name = "Seminar" },
                    new EventType { EventTypeId = 11, Name = "Charity Fundraiser" }
                );
            });

            // Configure Event
            modelBuilder.Entity<Event>(entity =>
            {
                entity.HasKey(e => e.EventId);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
                entity.Property(e => e.EventTypeId).IsRequired();

                entity.HasOne(e => e.EventType)
                    .WithMany(et => et.Events)
                    .HasForeignKey(e => e.EventTypeId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // Configure Booking
            modelBuilder.Entity<Booking>(entity =>
            {
                entity.HasKey(e => e.BookingId);

                entity.Property(e => e.VenueId).IsRequired();
                entity.Property(e => e.EventId).IsRequired();
                entity.Property(e => e.StartDateTime).IsRequired();
                entity.Property(e => e.EndDateTime).IsRequired();
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETDATE()");
                entity.Property(e => e.Status).HasMaxLength(50).HasDefaultValue("Confirmed");

                // Configure relationships
                entity.HasOne<Venue>(e => e.Venue)
                    .WithMany(v => v.Bookings)
                    .HasForeignKey(e => e.VenueId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne<Event>(e => e.Event)
                    .WithMany(ev => ev.Bookings)
                    .HasForeignKey(e => e.EventId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
