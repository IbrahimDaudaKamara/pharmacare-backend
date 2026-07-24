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
public class CategoriesController(AppDbContext db) : ControllerBase
{
    private async Task<CategoryDto> ToDto(Category c) => new(
        c.Id, c.Name, c.Description,
        await db.Medicines.CountAsync(m => m.CategoryId == c.Id && m.Active));

    /// <summary>Get all categories with medicine counts.</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var cats = await db.Categories.OrderBy(c => c.Name).ToListAsync();
        var dtos = new List<CategoryDto>();
        foreach (var c in cats) dtos.Add(await ToDto(c));
        return Ok(dtos);
    }

    /// <summary>Get a single category.</summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var c = await db.Categories.FindAsync(id);
        return c is null ? NotFound() : Ok(await ToDto(c));
    }

    /// <summary>Create a new category (Admin/Pharmacist).</summary>
    [HttpPost]
    [Authorize(Roles = "Admin,Pharmacist")]
    public async Task<IActionResult> Create([FromBody] CreateCategoryRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Name))
            return BadRequest(new MessageResult("Category name is required."));

        var cat = new Category { Name = req.Name, Description = req.Description };
        db.Categories.Add(cat);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = cat.Id }, await ToDto(cat));
    }

    /// <summary>Update a category (Admin/Pharmacist).</summary>
    [HttpPatch("{id:int}")]
    [Authorize(Roles = "Admin,Pharmacist")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateCategoryRequest req)
    {
        var cat = await db.Categories.FindAsync(id);
        if (cat is null) return NotFound();

        if (req.Name        is not null) cat.Name        = req.Name;
        if (req.Description is not null) cat.Description = req.Description;

        await db.SaveChangesAsync();
        return Ok(await ToDto(cat));
    }

    /// <summary>Delete a category only if it has no active medicines (Admin only).</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        var cat = await db.Categories.FindAsync(id);
        if (cat is null) return NotFound();

        var count = await db.Medicines.CountAsync(m => m.CategoryId == id && m.Active);
        if (count > 0)
            return Conflict(new MessageResult("Cannot delete a category that has active medicines."));

        db.Categories.Remove(cat);
        await db.SaveChangesAsync();
        return Ok(new MessageResult("Category deleted."));
    }
}
