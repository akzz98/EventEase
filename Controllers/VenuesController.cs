using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using EventEase.Data;
using EventEase.Models;
using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Http;

namespace EventEase.Controllers
{
    public class VenuesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly string _storageConnectionString;
        private readonly string _containerName;

        public VenuesController(ApplicationDbContext context, IConfiguration configuration)
        {
            _context = context;
            _storageConnectionString = configuration.GetValue<string>("AzureStorage:ConnectionString");
            _containerName = configuration.GetValue<string>("AzureStorage:ContainerName");
        }

        // GET: Venues
        public async Task<IActionResult> Index()
        {
            return View(await _context.Venues.ToListAsync());
        }

        // GET: Venues/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var venue = await _context.Venues
                .FirstOrDefaultAsync(m => m.VenueId == id);
            if (venue == null)
            {
                return NotFound();
            }

            return View(venue);
        }

        // GET: Venues/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Venues/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("VenueId,Name,Location,Capacity,Description,ImageFile")] Venue venue)
        {
            // Validate: No duplicate venue names
            bool duplicateName = await _context.Venues.AnyAsync(v => v.Name == venue.Name);
            if (duplicateName)
                ModelState.AddModelError("", "⚠️ A venue with this name already exists.");

            // Validate: Capacity must be greater than zero
            if (venue.Capacity <= 0)
                ModelState.AddModelError("", "⚠️ Capacity must be greater than zero.");

            // Validate: Capacity cannot exceed 100,000
            if (venue.Capacity > 100000)
                ModelState.AddModelError("", "⚠️ Capacity cannot exceed 100,000.");

            // Validate: Image file type and size
            if (venue.ImageFile != null && venue.ImageFile.Length > 0)
            {
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
                var extension = Path.GetExtension(venue.ImageFile.FileName).ToLowerInvariant();

                // Validate file type
                if (!allowedExtensions.Contains(extension))
                    ModelState.AddModelError("", "⚠️ Only image files are allowed (.jpg, .jpeg, .png, .gif, .webp).");

                // Validate file size (max 5MB)
                if (venue.ImageFile.Length > 5 * 1024 * 1024)
                    ModelState.AddModelError("", "⚠️ Image file size cannot exceed 5MB.");
            }

            if (ModelState.IsValid)
            {
                // --- BLOB UPLOAD LOGIC START ---
                if (venue.ImageFile != null && venue.ImageFile.Length > 0)
                {
                    // Create the blob client
                    BlobServiceClient blobServiceClient = new BlobServiceClient(_storageConnectionString);
                    BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient(_containerName);

                    // Ensure container exists
                    await containerClient.CreateIfNotExistsAsync(Azure.Storage.Blobs.Models.PublicAccessType.Blob);

                    // Create a unique name for the file
                    string fileName = Guid.NewGuid().ToString() + Path.GetExtension(venue.ImageFile.FileName);
                    BlobClient blobClient = containerClient.GetBlobClient(fileName);

                    // Upload the file
                    using (var stream = venue.ImageFile.OpenReadStream())
                    {
                        await blobClient.UploadAsync(stream, true);
                    }

                    // Save the URL to the database
                    venue.ImageUrl = blobClient.Uri.ToString();
                }
                // --- BLOB UPLOAD LOGIC END ---

                _context.Add(venue);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(venue);
        }

        // GET: Venues/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var venue = await _context.Venues.FindAsync(id);
            if (venue == null)
            {
                return NotFound();
            }
            return View(venue);
        }

        // POST: Venues/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("VenueId,Name,Location,Capacity,Description,ImageUrl,ImageFile")] Venue venue)
        {
            if (id != venue.VenueId)
            {
                return NotFound();
            }

            var existingVenue = await _context.Venues.AsNoTracking().FirstOrDefaultAsync(v => v.VenueId == id);
            if (existingVenue == null)
            {
                return NotFound();
            }

            // Validate: No duplicate venue names (exclude current venue)
            bool duplicateName = await _context.Venues.AnyAsync(v => v.Name == venue.Name && v.VenueId != venue.VenueId);
            if (duplicateName)
                ModelState.AddModelError("", "⚠️ A venue with this name already exists.");

            // Validate: Capacity must be greater than zero
            if (venue.Capacity <= 0)
                ModelState.AddModelError("", "⚠️ Capacity must be greater than zero.");

            // Validate: Capacity cannot exceed 100,000
            if (venue.Capacity > 100000)
                ModelState.AddModelError("", "⚠️ Capacity cannot exceed 100,000.");

            // Validate: Image file type and size
            if (venue.ImageFile != null && venue.ImageFile.Length > 0)
            {
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
                var extension = Path.GetExtension(venue.ImageFile.FileName).ToLowerInvariant();

                // Validate file type
                if (!allowedExtensions.Contains(extension))
                    ModelState.AddModelError("", "⚠️ Only image files are allowed (.jpg, .jpeg, .png, .gif, .webp).");

                // Validate file size (max 5MB)
                if (venue.ImageFile.Length > 5 * 1024 * 1024)
                    ModelState.AddModelError("", "⚠️ Image file size cannot exceed 5MB.");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    // Keep the old image if no new file is uploaded
                    venue.ImageUrl = existingVenue.ImageUrl;

                    if (venue.ImageFile != null && venue.ImageFile.Length > 0)
                    {
                        BlobServiceClient blobServiceClient = new BlobServiceClient(_storageConnectionString);
                        BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient(_containerName);

                        await containerClient.CreateIfNotExistsAsync(Azure.Storage.Blobs.Models.PublicAccessType.Blob);

                        string fileName = Guid.NewGuid().ToString() + Path.GetExtension(venue.ImageFile.FileName);
                        BlobClient blobClient = containerClient.GetBlobClient(fileName);

                        using (var stream = venue.ImageFile.OpenReadStream())
                        {
                            await blobClient.UploadAsync(stream, true);
                        }

                        venue.ImageUrl = blobClient.Uri.ToString();
                    }

                    _context.Update(venue);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!VenueExists(venue.VenueId))
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

            return View(venue);
        }

        // GET: Venues/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var venue = await _context.Venues
                .FirstOrDefaultAsync(m => m.VenueId == id);
            if (venue == null)
            {
                return NotFound();
            }

            return View(venue);
        }

        // POST: Venues/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var venue = await _context.Venues.FindAsync(id);

            if (venue == null)
            {
                return NotFound();
            }

            // Check for existing booking
            bool hasActiveBookings = await _context.Bookings.AnyAsync(b => b.VenueId == id);

            if (hasActiveBookings)
            {
                // Add a model error to display to the user
                ModelState.AddModelError("", "Cannot delete this venue because it has associated bookings. Please remove the bookings first.");

                // Return the Delete view again with the error message
                return View(venue);
            }

            _context.Venues.Remove(venue);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool VenueExists(int id)
        {
            return _context.Venues.Any(e => e.VenueId == id);
        }
    }
}