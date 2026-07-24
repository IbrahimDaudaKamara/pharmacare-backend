using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MediCorePMS.Data;
using MediCorePMS.DTOs;
using MediCorePMS.Models;

namespace MediCorePMS.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MedicinesController(AppDbContext db) : ControllerBase
{
    private static MedicineDto ToDto(Medicine m) => new(
        m.Id, m.Name, m.GenericName,
        m.CategoryId, m.Category?.Name ?? string.Empty,
        m.Price, m.Cost, m.Stock, m.ReorderLevel,
        m.Unit, m.Expiry, m.Manufacturer, m.Barcode,
        m.Active, m.RequiresPrescription, m.CreatedAt);

    private IQueryable<Medicine> WithIncludes() =>
        db.Medicines.Include(m => m.Category);

    /// <summary>Get all medicines with optional filters.</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string?  search,
        [FromQuery] int?     categoryId,
        [FromQuery] bool?    activeOnly,
        [FromQuery] bool?    lowStock,
        [FromQuery] int      page     = 1,
        [FromQuery] int      pageSize = 50)
    {
        var q = WithIncludes().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
            q = q.Where(m => m.Name.Contains(search)
                           || m.GenericName.Contains(search)
                           || m.Barcode.Contains(search)
                           || m.Manufacturer.Contains(search));

        if (categoryId.HasValue) q = q.Where(m => m.CategoryId == categoryId);
        if (activeOnly == true)  q = q.Where(m => m.Active);
        if (lowStock   == true)  q = q.Where(m => m.Stock <= m.ReorderLevel && m.Active);

        var total = await q.CountAsync();
        var data  = await q.OrderBy(m => m.Name)
                           .Skip((page - 1) * pageSize)
                           .Take(pageSize)
                           .Select(m => ToDto(m))
                           .ToListAsync();

        return Ok(new PagedResult<MedicineDto>(data, total, page, pageSize));
    }

    /// <summary>Get a single medicine by ID.</summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var m = await WithIncludes().FirstOrDefaultAsync(m => m.Id == id);
        return m is null ? NotFound() : Ok(ToDto(m));
    }

    /// <summary>Create a new medicine (Admin/Pharmacist).</summary>
    [HttpPost]
    [Authorize(Roles = "Admin,Pharmacist")]
    public async Task<IActionResult> Create([FromBody] CreateMedicineRequest req)
    {
        if (!await db.Categories.AnyAsync(c => c.Id == req.CategoryId))
            return BadRequest(new MessageResult("Category not found."));

        var med = new Medicine
        {
            Name                 = req.Name,
            GenericName          = req.GenericName,
            CategoryId           = req.CategoryId,
            Price                = req.Price,
            Cost                 = req.Cost,
            Stock                = req.Stock,
            ReorderLevel         = req.ReorderLevel,
            Unit                 = req.Unit,
            Expiry               = req.Expiry.ToUniversalTime(),
            Manufacturer         = req.Manufacturer,
            Barcode              = req.Barcode,
            Active               = true,
            RequiresPrescription = req.RequiresPrescription,
        };

        db.Medicines.Add(med);
        await db.SaveChangesAsync();
        await db.Entry(med).Reference(m => m.Category).LoadAsync();
        return CreatedAtAction(nameof(GetById), new { id = med.Id }, ToDto(med));
    }

    /// <summary>Update a medicine (Admin/Pharmacist).</summary>
    [HttpPatch("{id:int}")]
    [Authorize(Roles = "Admin,Pharmacist")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateMedicineRequest req)
    {
        var med = await WithIncludes().FirstOrDefaultAsync(m => m.Id == id);
        if (med is null) return NotFound();

        if (req.Name                 is not null) med.Name                 = req.Name;
        if (req.GenericName          is not null) med.GenericName          = req.GenericName;
        if (req.CategoryId           is not null) med.CategoryId           = req.CategoryId.Value;
        if (req.Price                is not null) med.Price                = req.Price.Value;
        if (req.Cost                 is not null) med.Cost                 = req.Cost.Value;
        if (req.Stock                is not null) med.Stock                = req.Stock.Value;
        if (req.ReorderLevel         is not null) med.ReorderLevel         = req.ReorderLevel.Value;
        if (req.Unit                 is not null) med.Unit                 = req.Unit;
        if (req.Expiry               is not null) med.Expiry               = req.Expiry.Value.ToUniversalTime();
        if (req.Manufacturer         is not null) med.Manufacturer         = req.Manufacturer;
        if (req.Barcode              is not null) med.Barcode              = req.Barcode;
        if (req.Active               is not null) med.Active               = req.Active.Value;
        if (req.RequiresPrescription is not null) med.RequiresPrescription = req.RequiresPrescription.Value;

        await db.SaveChangesAsync();
        await db.Entry(med).Reference(m => m.Category).LoadAsync();
        return Ok(ToDto(med));
    }

    /// <summary>Soft-delete a medicine (Admin only).</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        var med = await db.Medicines.FindAsync(id);
        if (med is null) return NotFound();

        med.Active = false;
        await db.SaveChangesAsync();
        return Ok(new MessageResult("Medicine deactivated."));
    }

    /// <summary>Get all stock alerts: out of stock, low stock, and near expiry (90 days).</summary>
    [HttpGet("alerts")]
    public async Task<IActionResult> GetAlerts()
    {
        var nearExpiryDate = DateTime.UtcNow.AddDays(90);

        var stockAlerts = await db.Medicines
            .Where(m => m.Active && m.Stock <= m.ReorderLevel)
            .OrderBy(m => m.Stock)
            .Select(m => new StockAlertDto(
                m.Id, m.Name, m.Stock, m.ReorderLevel, m.Unit,
                m.Stock == 0 ? "out" : "low",
                m.Expiry))
            .ToListAsync();

        var expiryAlerts = await db.Medicines
            .Where(m => m.Active && m.Expiry <= nearExpiryDate && m.Expiry > DateTime.UtcNow && m.Stock > m.ReorderLevel)
            .OrderBy(m => m.Expiry)
            .Select(m => new StockAlertDto(
                m.Id, m.Name, m.Stock, m.ReorderLevel, m.Unit,
                "near_expiry",
                m.Expiry))
            .ToListAsync();

        return Ok(stockAlerts.Concat(expiryAlerts).ToList());
    }
}
