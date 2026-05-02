using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RentVibe.Data;
using RentVibe.DTOs;
using RentVibe.Models;
using RentVibe.Models.Enums;
using RentVibe.Services;

namespace RentVibe.Controllers.Api;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ApplicationsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;
    private readonly NotificationService _notifications;

    public ApplicationsController(AppDbContext db, IWebHostEnvironment env, NotificationService notifications)
    {
        _db = db;
        _env = env;
        _notifications = notifications;
    }

    // Tenant — submit a rental application
    [HttpPost]
    [Authorize(Policy = "TenantOnly")]
    public async Task<IActionResult> Create([FromBody] CreateApplicationDto dto)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var property = await _db.Properties.Include(p => p.Landlord).FirstOrDefaultAsync(p => p.Id == dto.PropertyId);
        if (property is null) return NotFound(new { error = "Property not found." });

        if (property.RentalStatus == RentalStatus.Rented)
            return BadRequest(new { error = "Property is already rented." });

        var existing = await _db.RentalApplications
            .AnyAsync(a => a.PropertyId == dto.PropertyId && a.TenantId == userId && a.Status == ApplicationStatus.Pending);
        if (existing)
            return BadRequest(new { error = "You already have a pending application for this property." });

        var application = new RentalApplication
        {
            PropertyId = dto.PropertyId,
            TenantId = userId,
            Message = dto.Message
        };

        _db.RentalApplications.Add(application);
        await _db.SaveChangesAsync();

        var tenantName = User.FindFirstValue(ClaimTypes.Name) ?? "A tenant";
        await _notifications.SendAsync(property.LandlordId,
            $"{tenantName} submitted a rental application for \"{property.Title}\".",
            NotificationType.ApplicationSubmitted, application.Id);

        return Ok(new { application.Id, message = "Application submitted." });
    }

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".jpg", ".jpeg", ".png", ".doc", ".docx"
    };

    private const long MaxFileSize = 5 * 1024 * 1024; // 5 MB

    // Tenant — upload documents for an application
    [HttpPost("{id:int}/documents")]
    [Authorize(Policy = "TenantOnly")]
    public async Task<IActionResult> UploadDocuments(int id, [FromForm] List<IFormFile> files)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var app = await _db.RentalApplications.FirstOrDefaultAsync(a => a.Id == id && a.TenantId == userId);
        if (app is null) return NotFound();

        // Validate all files before saving any
        foreach (var file in files)
        {
            if (file.Length == 0) continue;

            var ext = Path.GetExtension(file.FileName);
            if (!AllowedExtensions.Contains(ext))
                return BadRequest(new { error = $"File type '{ext}' is not allowed. Accepted: {string.Join(", ", AllowedExtensions)}" });

            if (file.Length > MaxFileSize)
                return BadRequest(new { error = $"File '{file.FileName}' exceeds the maximum size of 5 MB." });
        }

        var uploadDir = Path.Combine(_env.WebRootPath ?? "wwwroot", "uploads", "documents");
        Directory.CreateDirectory(uploadDir);

        var urls = new List<string>();
        foreach (var file in files)
        {
            if (file.Length == 0) continue;
            var fileName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
            var filePath = Path.Combine(uploadDir, fileName);

            await using var stream = new FileStream(filePath, FileMode.Create);
            await file.CopyToAsync(stream);

            var url = $"/uploads/documents/{fileName}";
            _db.ApplicationDocuments.Add(new ApplicationDocument
            {
                RentalApplicationId = id,
                DocumentUrl = url,
                FileName = file.FileName
            });
            urls.Add(url);
        }

        await _db.SaveChangesAsync();
        return Ok(new { documentUrls = urls });
    }

    // Download document — only the tenant owner or property landlord can access
    [HttpGet("{id:int}/documents/{documentId:int}")]
    [Authorize]
    public async Task<IActionResult> DownloadDocument(int id, int documentId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var doc = await _db.ApplicationDocuments
            .Include(d => d.RentalApplication)
                .ThenInclude(a => a.Property)
            .FirstOrDefaultAsync(d => d.Id == documentId && d.RentalApplicationId == id);

        if (doc is null) return NotFound();

        // Authorization: only the tenant who submitted or the property landlord
        var application = doc.RentalApplication;
        if (application.TenantId != userId && application.Property.LandlordId != userId)
            return Forbid();

        var filePath = Path.Combine(
            _env.WebRootPath ?? "wwwroot",
            doc.DocumentUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));

        if (!System.IO.File.Exists(filePath))
            return NotFound(new { error = "Document file not found on server." });

        var contentType = Path.GetExtension(filePath).ToLowerInvariant() switch
        {
            ".pdf" => "application/pdf",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            _ => "application/octet-stream"
        };

        return PhysicalFile(filePath, contentType, doc.FileName);
    }

    // Tenant — get my applications
    [HttpGet("my")]
    [Authorize(Policy = "TenantOnly")]
    public async Task<IActionResult> GetMyApplications()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var apps = await _db.RentalApplications
            .Where(a => a.TenantId == userId)
            .Include(a => a.Property)
            .Include(a => a.Documents)
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new
            {
                a.Id, a.PropertyId,
                PropertyTitle = a.Property.Title,
                Status = a.Status.ToString(),
                a.Message, a.CreatedAt,
                Documents = a.Documents.Select(d => new { d.Id, d.FileName }).ToList()
            })
            .ToListAsync();
        return Ok(apps);
    }

    // Landlord — get applications for my properties
    [HttpGet("landlord")]
    [Authorize(Policy = "LandlordOnly")]
    public async Task<IActionResult> GetLandlordApplications()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var apps = await _db.RentalApplications
            .Where(a => a.Property.LandlordId == userId)
            .Include(a => a.Property)
            .Include(a => a.Tenant)
            .Include(a => a.Documents)
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new
            {
                a.Id, a.PropertyId,
                PropertyTitle = a.Property.Title,
                TenantName = a.Tenant.FullName,
                TenantEmail = a.Tenant.Email,
                Status = a.Status.ToString(),
                a.Message, a.CreatedAt,
                Documents = a.Documents.Select(d => new { d.Id, d.FileName }).ToList()
            })
            .ToListAsync();
        return Ok(apps);
    }

    // Landlord — accept application (property becomes Rented)
    [HttpPost("{id:int}/accept")]
    [Authorize(Policy = "LandlordOnly")]
    public async Task<IActionResult> Accept(int id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var app = await _db.RentalApplications
            .Include(a => a.Property)
            .FirstOrDefaultAsync(a => a.Id == id && a.Property.LandlordId == userId);

        if (app is null) return NotFound();

        app.Status = ApplicationStatus.Accepted;
        app.Property.RentalStatus = RentalStatus.Rented;

        // Reject all other pending applications for this property
        var otherApps = await _db.RentalApplications
            .Where(a => a.PropertyId == app.PropertyId && a.Id != id && a.Status == ApplicationStatus.Pending)
            .ToListAsync();

        foreach (var other in otherApps)
        {
            other.Status = ApplicationStatus.Rejected;
            await _notifications.SendAsync(other.TenantId,
                $"Your application for \"{app.Property.Title}\" has been rejected (property rented to another tenant).",
                NotificationType.ApplicationRejected, other.Id);
        }

        await _db.SaveChangesAsync();

        await _notifications.SendAsync(app.TenantId,
            $"Congratulations! Your application for \"{app.Property.Title}\" has been accepted!",
            NotificationType.ApplicationAccepted, app.Id);

        return Ok(new { message = "Application accepted. Property status changed to Rented." });
    }

    // Landlord — reject application
    [HttpPost("{id:int}/reject")]
    [Authorize(Policy = "LandlordOnly")]
    public async Task<IActionResult> Reject(int id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var app = await _db.RentalApplications
            .Include(a => a.Property)
            .FirstOrDefaultAsync(a => a.Id == id && a.Property.LandlordId == userId);

        if (app is null) return NotFound();

        app.Status = ApplicationStatus.Rejected;
        await _db.SaveChangesAsync();

        await _notifications.SendAsync(app.TenantId,
            $"Your application for \"{app.Property.Title}\" has been rejected.",
            NotificationType.ApplicationRejected, app.Id);

        return Ok(new { message = "Application rejected." });
    }
}
