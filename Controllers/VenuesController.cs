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
using Azure.Storage.Blobs.Models;
using Microsoft.AspNetCore.Http;

namespace EventEase.Controllers
{
    public class VenuesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly string? _storageConnectionString;
        private readonly string? _containerName;

        public VenuesController(
            ApplicationDbContext context,
            IConfiguration configuration,
            IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
            _storageConnectionString = ResolveStorageConnectionString(configuration);
            _containerName = configuration["AzureStorage:ContainerName"]?.Trim();
        }

        // GET: Venues
        public async Task<IActionResult> Index(
            string searchString,
            int? minCapacity,
            int? maxCapacity,
            DateTime? fromDate,
            DateTime? toDate,
            bool availableOnly = false)
        {
            ViewData["CurrentFilter"] = searchString;
            ViewData["MinCapacityFilter"] = minCapacity;
            ViewData["MaxCapacityFilter"] = maxCapacity;
            ViewData["FromDateFilter"] = fromDate?.ToString("yyyy-MM-dd");
            ViewData["ToDateFilter"] = toDate?.ToString("yyyy-MM-dd");
            ViewData["AvailableOnlyFilter"] = availableOnly;

            var venues = _context.Venues.AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchString))
            {
                bool isNumeric = int.TryParse(searchString, out int venueIdSearch);

                venues = venues.Where(v =>
                    (isNumeric && v.VenueId == venueIdSearch) ||
                    v.Name.Contains(searchString) ||
                    v.Location.Contains(searchString) ||
                    (v.Description != null && v.Description.Contains(searchString)));
            }

            if (minCapacity.HasValue)
            {
                venues = venues.Where(v => v.Capacity >= minCapacity.Value);
            }

            if (maxCapacity.HasValue)
            {
                venues = venues.Where(v => v.Capacity <= maxCapacity.Value);
            }

            if (minCapacity.HasValue && maxCapacity.HasValue && minCapacity.Value > maxCapacity.Value)
            {
                ViewData["CapacityFilterError"] =
                    "Minimum capacity cannot be greater than maximum capacity.";
                venues = venues.Where(v => false);
            }

            if (availableOnly)
            {
                if (!fromDate.HasValue || !toDate.HasValue)
                {
                    ViewData["AvailabilityFilterError"] =
                        "Select both from and to dates to show available venues only.";
                }
                else
                {
                    var rangeStart = fromDate.Value.Date;
                    var rangeEnd = toDate.Value.Date.AddDays(1);

                    venues = venues.Where(v => !_context.Bookings.Any(b =>
                        b.VenueId == v.VenueId &&
                        b.StartDateTime < rangeEnd &&
                        b.EndDateTime > rangeStart));
                }
            }

            return View(await venues.ToListAsync());
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
                if (venue.ImageFile != null && venue.ImageFile.Length > 0)
                {
                    var uploadResult = await UploadVenueImageAsync(venue.ImageFile);
                    if (uploadResult.ErrorMessage != null)
                    {
                        ModelState.AddModelError("", uploadResult.ErrorMessage);
                    }
                    else
                    {
                        venue.ImageUrl = uploadResult.ImageUrl;
                    }
                }

                if (ModelState.IsValid)
                {
                    _context.Add(venue);
                    await _context.SaveChangesAsync();
                    return RedirectToAction(nameof(Index));
                }
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
                        var uploadResult = await UploadVenueImageAsync(venue.ImageFile);
                        if (uploadResult.ErrorMessage != null)
                        {
                            ModelState.AddModelError("", uploadResult.ErrorMessage);
                        }
                        else
                        {
                            venue.ImageUrl = uploadResult.ImageUrl;
                        }
                    }

                    if (!ModelState.IsValid)
                    {
                        return View(venue);
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

        private bool IsBlobStorageConfigured(out string errorMessage)
        {
            if (string.IsNullOrWhiteSpace(_storageConnectionString))
            {
                errorMessage = "⚠️ Azure Storage is not configured. In App Service → Configuration → Application settings, add AzureStorage__ConnectionString (full Access keys string). Or use Connection strings → Custom → name AzureStorage.";
                return false;
            }

            if (string.Equals(_storageConnectionString, "CONFIGURE_IN_AZURE_APP_SERVICE", StringComparison.OrdinalIgnoreCase))
            {
                errorMessage = "⚠️ Azure Storage still uses the production placeholder. Set AzureStorage__ConnectionString in App Service Application settings.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(_containerName))
            {
                errorMessage = "⚠️ Azure Storage container name is not configured.";
                return false;
            }

            if (!_environment.IsDevelopment() &&
                (_storageConnectionString.Contains("UseDevelopmentStorage", StringComparison.OrdinalIgnoreCase) ||
                 _storageConnectionString.Contains("AccountName=devstoreaccount1", StringComparison.OrdinalIgnoreCase)))
            {
                errorMessage = "⚠️ Production must use the Azure Storage account connection string (cldveventeasestg), not Azurite.";
                return false;
            }

            if (!_storageConnectionString.Contains("AccountName=", StringComparison.OrdinalIgnoreCase) &&
                !_storageConnectionString.Contains("UseDevelopmentStorage", StringComparison.OrdinalIgnoreCase))
            {
                errorMessage = "⚠️ Azure Storage connection string is invalid. Paste the full string from Storage account → Access keys.";
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }

        private async Task<(string? ImageUrl, string? ErrorMessage)> UploadVenueImageAsync(IFormFile imageFile)
        {
            if (!IsBlobStorageConfigured(out var configError))
            {
                return (null, configError);
            }

            try
            {
                var blobServiceClient = new BlobServiceClient(_storageConnectionString);
                var containerClient = blobServiceClient.GetBlobContainerClient(_containerName!);

                if (_environment.IsDevelopment())
                {
                    await containerClient.CreateIfNotExistsAsync(PublicAccessType.Blob);
                }

                string fileName = Guid.NewGuid().ToString() + Path.GetExtension(imageFile.FileName);
                var blobClient = containerClient.GetBlobClient(fileName);

                using (var stream = imageFile.OpenReadStream())
                {
                    await blobClient.UploadAsync(stream, overwrite: true);
                }

                return (blobClient.Uri.ToString(), null);
            }
            catch (Azure.RequestFailedException ex)
            {
                string accountHint = string.Empty;
                try
                {
                    accountHint = new BlobServiceClient(_storageConnectionString).AccountName;
                    accountHint = $" Account in use: {accountHint}.";
                }
                catch
                {
                    // Connection string could not be parsed for diagnostics.
                }

                string hint = ex.Status == 400
                    ? " The storage hostname is invalid — remove quotes, labels (e.g. \"Blob storage\"), and use the exact string from cldveventeasestg → Access keys → Connection string."
                    : $" Azure returned: {ex.Message}";

                return (null, $"⚠️ Image upload failed ({ex.Status}).{accountHint}{hint} Container setting: AzureStorage__ContainerName = venue-images.");
            }
        }

        private static string? ResolveStorageConnectionString(IConfiguration configuration)
        {
            string? value =
                configuration.GetConnectionString("AzureStorage")
                ?? configuration["AzureStorage:ConnectionString"];

            return NormalizeSettingValue(value);
        }

        private static string? NormalizeSettingValue(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            value = value.Trim();

            if ((value.StartsWith('"') && value.EndsWith('"')) ||
                (value.StartsWith('\'') && value.EndsWith('\'')))
            {
                value = value[1..^1].Trim();
            }

            return value;
        }
    }
}