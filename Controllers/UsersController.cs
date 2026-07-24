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
public class UsersController(AppDbContext db) : ControllerBase
{
    private static UserDto ToDto(User u) => new(
        u.Id, u.Name, u.Email, u.Role, u.Active, u.Avatar, u.AvatarColor, u.CreatedAt);

    private static readonly string[] AvatarColors =
    [
        "linear-gradient(135deg,#4f8ef7,#7c5cfc)",
        "linear-gradient(135deg,#00d4aa,#4f8ef7)",
        "linear-gradient(135deg,#f7614f,#f7c04f)",
        "linear-gradient(135deg,#7c5cfc,#f7614f)"
    ];

    /// <summary>Get all users (Admin only).</summary>
    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetAll([FromQuery] string? search)
    {
        var q = db.Users.AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
            q = q.Where(u => u.Name.Contains(search) || u.Email.Contains(search) || u.Role.Contains(search));

        var users = await q.OrderBy(u => u.Id).Select(u => ToDto(u)).ToListAsync();
        return Ok(users);
    }

    /// <summary>Get a single user.</summary>
    [HttpGet("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetById(int id)
    {
        var u = await db.Users.FindAsync(id);
        return u is null ? NotFound() : Ok(ToDto(u));
    }

    /// <summary>Create a new user (Admin only).</summary>
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create([FromBody] CreateUserRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Name) || string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password))
            return BadRequest(new MessageResult("Name, email, and password are required."));

        if (!System.Net.Mail.MailAddress.TryCreate(req.Email.Trim(), out _))
            return BadRequest(new MessageResult("Please provide a valid email address."));

        if (await db.Users.AnyAsync(u => u.Email.ToLower() == req.Email.Trim().ToLower()))
            return Conflict(new MessageResult("Email already exists."));

        if (!IsStrongPassword(req.Password))
            return BadRequest(new MessageResult("Password must be at least 8 characters and include uppercase, lowercase, number, and symbol."));

        var idx  = await db.Users.CountAsync() % AvatarColors.Length;
        var user = new User
        {
            Name         = req.Name,
            Email        = req.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password),
            Role         = req.Role,
            Avatar       = req.Name[..1].ToUpper(),
            AvatarColor  = AvatarColors[idx],
            Active       = true,
        };

        db.Users.Add(user);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = user.Id }, ToDto(user));
    }

    private static bool IsStrongPassword(string? password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < 8) return false;
        return password.Any(char.IsUpper)
            && password.Any(char.IsLower)
            && password.Any(char.IsDigit)
            && password.Any(ch => !char.IsLetterOrDigit(ch));
    }

    /// <summary>Update user details (Admin only).</summary>
    [HttpPatch("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateUserRequest req)
    {
        var user = await db.Users.FindAsync(id);
        if (user is null) return NotFound();

        if (req.Name    is not null) user.Name  = req.Name;
        if (req.Email   is not null)
        {
            if (!System.Net.Mail.MailAddress.TryCreate(req.Email.Trim(), out _))
                return BadRequest(new MessageResult("Please provide a valid email address."));
            user.Email = req.Email.Trim();
        }
        if (req.Role    is not null) user.Role  = req.Role;
        if (req.Password is not null)
        {
            if (!IsStrongPassword(req.Password))
                return BadRequest(new MessageResult("Password must be at least 8 characters and include uppercase, lowercase, number, and symbol."));
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password);
        }

        await db.SaveChangesAsync();
        return Ok(ToDto(user));
    }

    /// <summary>Toggle active / disabled status (Admin only).</summary>
    [HttpPatch("{id:int}/toggle")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Toggle(int id)
    {
        var user = await db.Users.FindAsync(id);
        if (user is null) return NotFound();

        user.Active = !user.Active;
        await db.SaveChangesAsync();
        return Ok(new MessageResult($"User {(user.Active ? "enabled" : "disabled")}."));
    }
}
