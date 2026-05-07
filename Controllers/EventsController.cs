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
    public class EventsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public EventsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Events
        public async Task<IActionResult> Index()
        {
            return View(await _context.Events.ToListAsync());
        }

        // GET: Events/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var @event = await _context.Events
                .FirstOrDefaultAsync(m => m.EventId == id);
            if (@event == null)
            {
                return NotFound();
            }

            return View(@event);
        }

        // GET: Events/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Events/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("EventId,Name,Description,PlannedStartDateOnly,PlannedStartTime,PlannedEndDateOnly,PlannedEndTime")] Event @event)
        {
            // Combine date and time fields into DateTime properties
            if (@event.PlannedStartDateOnly.HasValue && @event.PlannedStartTime.HasValue)
            {
                @event.PlannedStartDate = @event.PlannedStartDateOnly.Value.Date + @event.PlannedStartTime.Value;
            }

            if (@event.PlannedEndDateOnly.HasValue && @event.PlannedEndTime.HasValue)
            {
                @event.PlannedEndDate = @event.PlannedEndDateOnly.Value.Date + @event.PlannedEndTime.Value;
            }

            // Remove validation errors for the NotMapped properties
            ModelState.Remove(nameof(@event.PlannedStartDateOnly));
            ModelState.Remove(nameof(@event.PlannedStartTime));
            ModelState.Remove(nameof(@event.PlannedEndDateOnly));
            ModelState.Remove(nameof(@event.PlannedEndTime));

            // Validate: End date/time must be after Start date/time
            if (@event.PlannedStartDate.HasValue && @event.PlannedEndDate.HasValue)
            {
                if (@event.PlannedEndDate <= @event.PlannedStartDate)
                {
                    ModelState.AddModelError("", "⚠️ End date/time must be AFTER the start date/time.");
                }
            }

            // Validate: Start date must be in the future
            if (@event.PlannedStartDate.HasValue && @event.PlannedStartDate <= DateTime.Now)
            {
                ModelState.AddModelError("", "⚠️ Start date/time must be in the future.");
            }

            // Validate: Minimum event duration of 30 minutes
            if (@event.PlannedStartDate.HasValue && @event.PlannedEndDate.HasValue)
            {
                var duration = @event.PlannedEndDate.Value - @event.PlannedStartDate.Value;
                if (duration.TotalMinutes < 30)
                {
                    ModelState.AddModelError("", "⚠️ Event duration must be at least 30 minutes.");
                }
            }

            // Validate: No duplicate event names
            bool duplicateName = await _context.Events.AnyAsync(e => e.Name == @event.Name);
            if (duplicateName)
            {
                ModelState.AddModelError("", "⚠️ An event with this name already exists.");
            }

            if (ModelState.IsValid)
            {
                _context.Add(@event);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(@event);
        }

        // GET: Events/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var @event = await _context.Events.FindAsync(id);
            if (@event == null)
            {
                return NotFound();
            }

            // Split DateTime into separate date and time fields for the form
            if (@event.PlannedStartDate.HasValue)
            {
                @event.PlannedStartDateOnly = @event.PlannedStartDate.Value.Date;
                @event.PlannedStartTime = @event.PlannedStartDate.Value.TimeOfDay;
            }

            if (@event.PlannedEndDate.HasValue)
            {
                @event.PlannedEndDateOnly = @event.PlannedEndDate.Value.Date;
                @event.PlannedEndTime = @event.PlannedEndDate.Value.TimeOfDay;
            }

            return View(@event);
        }

        // POST: Events/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("EventId,Name,Description,PlannedStartDateOnly,PlannedStartTime,PlannedEndDateOnly,PlannedEndTime")] Event @event)
        {
            if (id != @event.EventId)
            {
                return NotFound();
            }

            // Combine date and time fields into DateTime properties
            if (@event.PlannedStartDateOnly.HasValue && @event.PlannedStartTime.HasValue)
            {
                @event.PlannedStartDate = @event.PlannedStartDateOnly.Value.Date + @event.PlannedStartTime.Value;
            }
            else
            {
                @event.PlannedStartDate = null;
            }

            if (@event.PlannedEndDateOnly.HasValue && @event.PlannedEndTime.HasValue)
            {
                @event.PlannedEndDate = @event.PlannedEndDateOnly.Value.Date + @event.PlannedEndTime.Value;
            }
            else
            {
                @event.PlannedEndDate = null;
            }

            // Remove validation errors for the NotMapped properties
            ModelState.Remove(nameof(@event.PlannedStartDateOnly));
            ModelState.Remove(nameof(@event.PlannedStartTime));
            ModelState.Remove(nameof(@event.PlannedEndDateOnly));
            ModelState.Remove(nameof(@event.PlannedEndTime));

            // Validate: End date/time must be after Start date/time
            if (@event.PlannedStartDate.HasValue && @event.PlannedEndDate.HasValue)
            {
                if (@event.PlannedEndDate <= @event.PlannedStartDate)
                {
                    ModelState.AddModelError("", "⚠️ End date/time must be AFTER the start date/time.");
                }
            }

            // Validate: Minimum event duration of 30 minutes
            if (@event.PlannedStartDate.HasValue && @event.PlannedEndDate.HasValue)
            {
                var duration = @event.PlannedEndDate.Value - @event.PlannedStartDate.Value;
                if (duration.TotalMinutes < 30)
                {
                    ModelState.AddModelError("", "⚠️ Event duration must be at least 30 minutes.");
                }
            }

            // Validate: No duplicate event names (exclude current event)
            bool duplicateName = await _context.Events.AnyAsync(e => e.Name == @event.Name && e.EventId != @event.EventId);
            if (duplicateName)
            {
                ModelState.AddModelError("", "⚠️ An event with this name already exists.");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(@event);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!EventExists(@event.EventId))
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
            return View(@event);
        }

        // GET: Events/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var @event = await _context.Events
                .FirstOrDefaultAsync(m => m.EventId == id);
            if (@event == null)
            {
                return NotFound();
            }

            return View(@event);
        }

        // POST: Events/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var @event = await _context.Events.FindAsync(id);

            if (@event == null)
            {
                return NotFound();
            }

            // Restrict deletion if event has existing bookings
            bool hasBookings = await _context.Bookings.AnyAsync(b => b.EventId == id);

            if (hasBookings)
            {
                ModelState.AddModelError("", "Cannot delete this event because it is linked to existing bookings.");
                return View(@event);
            }

            _context.Events.Remove(@event);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool EventExists(int id)
        {
            return _context.Events.Any(e => e.EventId == id);
        }
    }
}