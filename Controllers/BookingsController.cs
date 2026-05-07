using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using EventEase.Data;
using EventEase.Models;

namespace EventEase.Controllers
{
    public class BookingsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public BookingsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Bookings
        public async Task<IActionResult> Index(string searchString)
        {
            // Pass the current search string back to the view
            ViewData["CurrentFilter"] = searchString;

            var bookings = _context.Bookings
                .Include(b => b.Event)
                .Include(b => b.Venue)
                .AsQueryable();

            // Search Functionality
            if (!string.IsNullOrEmpty(searchString))
            {
                // Try parse as int for BookingId search
                bool isNumeric = int.TryParse(searchString, out int bookingIdSearch);

                bookings = bookings.Where(b =>
                    (isNumeric && b.BookingId == bookingIdSearch) ||
                    b.Event.Name.Contains(searchString)
                );
            }

            return View(await bookings.ToListAsync());
        }

        // GET: Bookings/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var booking = await _context.Bookings
                .Include(b => b.Event)
                .Include(b => b.Venue)
                .FirstOrDefaultAsync(m => m.BookingId == id);
            if (booking == null)
            {
                return NotFound();
            }

            return View(booking);
        }

        // GET: Bookings/Create
        public IActionResult Create()
        {
            ViewData["EventId"] = new SelectList(_context.Events, "EventId", "Name");
            ViewData["VenueId"] = new SelectList(_context.Venues, "VenueId", "Location");
            return View();
        }

        // POST: Bookings/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("BookingId,VenueId,EventId,StartDate,StartTime,EndDate,EndTime,Status")] Booking booking)
        {
            // Combine date and time fields into DateTime properties
            if (booking.StartDate.HasValue && booking.StartTime.HasValue)
            {
                booking.StartDateTime = booking.StartDate.Value.Date + booking.StartTime.Value;
            }

            if (booking.EndDate.HasValue && booking.EndTime.HasValue)
            {
                booking.EndDateTime = booking.EndDate.Value.Date + booking.EndTime.Value;
            }

            // Remove validation errors for the NotMapped properties
            ModelState.Remove(nameof(booking.StartDate));
            ModelState.Remove(nameof(booking.StartTime));
            ModelState.Remove(nameof(booking.EndDate));
            ModelState.Remove(nameof(booking.EndTime));

            // Validate: End date/time must be after start date/time
            if (booking.EndDateTime <= booking.StartDateTime)
                ModelState.AddModelError("", "⚠️ End date/time must be AFTER the start date/time.");

            // Validate: Booking must be in the future
            if (booking.StartDateTime <= DateTime.Now)
                ModelState.AddModelError("", "⚠️ Booking start date/time must be in the future.");

            // Validate: Minimum booking duration of 30 minutes
            var duration = booking.EndDateTime - booking.StartDateTime;
            if (duration.TotalMinutes < 30)
                ModelState.AddModelError("", "⚠️ Booking duration must be at least 30 minutes.");

            // Validate: Booking dates must fall within the event's planned dates
            var selectedEvent = await _context.Events.FindAsync(booking.EventId);
            if (selectedEvent != null && selectedEvent.PlannedStartDate.HasValue && selectedEvent.PlannedEndDate.HasValue)
            {
                if (booking.StartDateTime < selectedEvent.PlannedStartDate.Value ||
                    booking.EndDateTime > selectedEvent.PlannedEndDate.Value)
                {
                    ModelState.AddModelError("", $"⚠️ Booking dates must fall within the event's planned dates " +
                        $"({selectedEvent.PlannedStartDate.Value:dd MMM yyyy HH:mm} - {selectedEvent.PlannedEndDate.Value:dd MMM yyyy HH:mm}).");
                }
            }

            // Validate: No duplicate booking for same event + venue
            bool duplicateBooking = await _context.Bookings.AnyAsync(b =>
                b.VenueId == booking.VenueId && b.EventId == booking.EventId);
            if (duplicateBooking)
                ModelState.AddModelError("", "⚠️ This event is already booked at this venue.");

            // --- PART 2: DOUBLE BOOKING PREVENTION ---
            bool isDoubleBooked = await _context.Bookings.AnyAsync(b =>
                b.VenueId == booking.VenueId &&
                booking.StartDateTime < b.EndDateTime &&
                booking.EndDateTime > b.StartDateTime
            );

            if (isDoubleBooked)
            {
                ModelState.AddModelError("", "⚠️ This venue is already booked for the selected date and time. Please choose a different time slot or venue.");
            }
            // --- END DOUBLE BOOKING CHECK ---

            if (ModelState.IsValid)
            {
                booking.CreatedAt = DateTime.Now;
                _context.Add(booking);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            ViewData["EventId"] = new SelectList(_context.Events, "EventId", "Name", booking.EventId);
            ViewData["VenueId"] = new SelectList(_context.Venues, "VenueId", "Location", booking.VenueId);
            return View(booking);
        }

        // GET: Bookings/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var booking = await _context.Bookings.FindAsync(id);
            if (booking == null)
            {
                return NotFound();
            }

            // Split DateTime into separate date and time fields for the form
            booking.StartDate = booking.StartDateTime.Date;
            booking.StartTime = booking.StartDateTime.TimeOfDay;
            booking.EndDate = booking.EndDateTime.Date;
            booking.EndTime = booking.EndDateTime.TimeOfDay;

            ViewData["EventId"] = new SelectList(_context.Events, "EventId", "Name", booking.EventId);
            ViewData["VenueId"] = new SelectList(_context.Venues, "VenueId", "Location", booking.VenueId);
            return View(booking);
        }

        // POST: Bookings/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("BookingId,VenueId,EventId,StartDate,StartTime,EndDate,EndTime,CreatedAt,Status")] Booking booking)
        {
            if (id != booking.BookingId)
            {
                return NotFound();
            }

            // Combine date and time fields into DateTime properties
            if (booking.StartDate.HasValue && booking.StartTime.HasValue)
            {
                booking.StartDateTime = booking.StartDate.Value.Date + booking.StartTime.Value;
            }

            if (booking.EndDate.HasValue && booking.EndTime.HasValue)
            {
                booking.EndDateTime = booking.EndDate.Value.Date + booking.EndTime.Value;
            }

            // Remove validation errors for the NotMapped properties
            ModelState.Remove(nameof(booking.StartDate));
            ModelState.Remove(nameof(booking.StartTime));
            ModelState.Remove(nameof(booking.EndDate));
            ModelState.Remove(nameof(booking.EndTime));

            // Validate: End date/time must be after start date/time
            if (booking.EndDateTime <= booking.StartDateTime)
                ModelState.AddModelError("", "⚠️ End date/time must be AFTER the start date/time.");

            // Validate: Minimum booking duration of 30 minutes
            var duration = booking.EndDateTime - booking.StartDateTime;
            if (duration.TotalMinutes < 30)
                ModelState.AddModelError("", "⚠️ Booking duration must be at least 30 minutes.");

            // Validate: Booking dates must fall within the event's planned dates
            var selectedEvent = await _context.Events.FindAsync(booking.EventId);
            if (selectedEvent != null && selectedEvent.PlannedStartDate.HasValue && selectedEvent.PlannedEndDate.HasValue)
            {
                if (booking.StartDateTime < selectedEvent.PlannedStartDate.Value ||
                    booking.EndDateTime > selectedEvent.PlannedEndDate.Value)
                {
                    ModelState.AddModelError("", $"⚠️ Booking dates must fall within the event's planned dates " +
                        $"({selectedEvent.PlannedStartDate.Value:dd MMM yyyy HH:mm} - {selectedEvent.PlannedEndDate.Value:dd MMM yyyy HH:mm}).");
                }
            }

            // Validate: No duplicate booking for same event + venue (exclude current booking)
            bool duplicateBooking = await _context.Bookings.AnyAsync(b =>
                b.BookingId != booking.BookingId &&
                b.VenueId == booking.VenueId && b.EventId == booking.EventId);
            if (duplicateBooking)
                ModelState.AddModelError("", "⚠️ This event is already booked at this venue.");

            // DOUBLE BOOKING PREVENTION (exclude current booking)
            bool isDoubleBooked = await _context.Bookings.AnyAsync(b =>
                b.BookingId != booking.BookingId && // Exclude the current booking
                b.VenueId == booking.VenueId &&
                booking.StartDateTime < b.EndDateTime &&
                booking.EndDateTime > b.StartDateTime
            );

            if (isDoubleBooked)
            {
                ModelState.AddModelError("", "⚠️ This venue is already booked for the selected date and time. Please choose a different time slot or venue.");
            }
            // --- END DOUBLE BOOKING CHECK ---

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(booking);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!BookingExists(booking.BookingId))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }

            ViewData["EventId"] = new SelectList(_context.Events, "EventId", "Name", booking.EventId);
            ViewData["VenueId"] = new SelectList(_context.Venues, "VenueId", "Location", booking.VenueId);
            return View(booking);
        }

        // GET: Bookings/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var booking = await _context.Bookings
                .Include(b => b.Event)
                .Include(b => b.Venue)
                .FirstOrDefaultAsync(m => m.BookingId == id);
            if (booking == null)
            {
                return NotFound();
            }

            return View(booking);
        }

        // POST: Bookings/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var booking = await _context.Bookings.FindAsync(id);
            if (booking != null)
            {
                _context.Bookings.Remove(booking);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool BookingExists(int id)
        {
            return _context.Bookings.Any(e => e.BookingId == id);
        }
    }
}