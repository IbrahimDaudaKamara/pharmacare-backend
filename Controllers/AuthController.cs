using System.Net.Mail;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MediCorePMS.Data;
using MediCorePMS.DTOs;
using MediCorePMS.Services;

namespace MediCorePMS.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController(AppDbContext db, TokenService tokenService) : ControllerBase
{
    private const string AdminKey = "MEDICORE_ADMIN_SECRET";

    private static bool IsValidEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return false;
        try
        {
            var addr = new MailAddress(email.Trim());
            return addr.Address == email.Trim();
        }
        catch
        {
            return false;
        }
    }

    private static bool IsStrongPassword(string? password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < 8) return false;
        return password.Any(char.IsUpper)
            && password.Any(char.IsLower)
            && password.Any(char.IsDigit)
            && password.Any(ch => !char.IsLetterOrDigit(ch));
    }

    /// <summary>Authenticate and receive a JWT token.</summary>
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        if (!IsValidEmail(req.Email) || !IsStrongPassword(req.Password))
            return Unauthorized(new MessageResult("Invalid credentials or account disabled."));

        var normalizedEmail = req.Email.Trim().ToLowerInvariant();
        var user = await db.Users
            .FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail && u.Active);

        if (user is null || !BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
            return Unauthorized(new MessageResult("Invalid credentials or account disabled."));

        var token = tokenService.GenerateToken(user);

        return Ok(new LoginResponse(
            user.Id,
            user.Name,
            user.Email,
            user.Role,
            user.Avatar,
            user.AvatarColor,
            token
        ));
    }

    /// <summary>Register a new administrator account (requires admin secret key).</summary>
    [HttpPost("register-admin")]
    public async Task<IActionResult> RegisterAdmin([FromBody] RegisterAdminRequest req)
    {
        if (req.AdminKey != AdminKey)
            return Unauthorized(new MessageResult("Invalid admin registration key."));

        if (!IsValidEmail(req.Email))
            return BadRequest(new MessageResult("Please provide a valid email address."));

        if (await db.Users.AnyAsync(u => u.Email.ToLower() == req.Email.Trim().ToLower()))
            return Conflict(new MessageResult("An account with this email already exists."));

        if (string.IsNullOrWhiteSpace(req.Name))
            return BadRequest(new MessageResult("Name is required."));

        if (!IsStrongPassword(req.Password))
            return BadRequest(new MessageResult("Password must be at least 8 characters and include uppercase, lowercase, number, and symbol."));

        var user = new MediCorePMS.Models.User
        {
            Name         = req.Name,
            Email        = req.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password),
            Role         = "Admin",
            Avatar       = req.Name[..1].ToUpper(),
            AvatarColor  = "linear-gradient(135deg,#4f8ef7,#7c5cfc)",
            Active       = true,
        };

        db.Users.Add(user);
        await db.SaveChangesAsync();

        var token = tokenService.GenerateToken(user);

        return CreatedAtAction(nameof(Login), new MessageResult($"Admin account created successfully."));
    }

    /// <summary>Request a password reset code (sent to console for demo purposes).</summary>
    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest req)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == req.Email && u.Active);

        // Always return 200 to avoid user enumeration
        if (user is null)
            return Ok(new MessageResult("If this email is registered, a reset code has been generated."));

        // Generate 6-digit reset code
        var code = new Random().Next(100000, 999999).ToString();
        user.PasswordResetCode = code;
        user.ResetCodeExpires  = DateTime.UtcNow.AddMinutes(15);
        await db.SaveChangesAsync();

        // In production, send via email. For demo, return in response.
        Console.WriteLine($"[PharmaCare] Password reset code for {user.Email}: {code}");

        return Ok(new { message = "Reset code generated. Check server console for demo.", code });
    }

    /// <summary>Reset password using the verification code.</summary>
    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest req)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == req.Email && u.Active);

        if (user is null
            || user.PasswordResetCode != req.Code
            || user.ResetCodeExpires is null
            || user.ResetCodeExpires < DateTime.UtcNow)
            return BadRequest(new MessageResult("Invalid or expired reset code."));

        if (!IsStrongPassword(req.NewPassword))
            return BadRequest(new MessageResult("Password must be at least 8 characters and include uppercase, lowercase, number, and symbol."));

        user.PasswordHash      = BCrypt.Net.BCrypt.HashPassword(req.NewPassword);
        user.PasswordResetCode = null;
        user.ResetCodeExpires  = null;
        await db.SaveChangesAsync();

        return Ok(new MessageResult("Password reset successfully. You can now log in."));
    }
}
