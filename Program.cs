using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using MediCorePMS.Data;
using MediCorePMS.Services;
using MediCorePMS.Models; // <-- 1. ADDED THIS (Change to .Entities if your User class is there)

var builder = WebApplication.CreateBuilder(args);

// ── Database ──────────────────────────────────────────────────────
var useInMemoryDatabase = builder.Configuration.GetValue("UseInMemoryDatabase", builder.Environment.IsDevelopment());

builder.Services.AddDbContext<AppDbContext>(opt =>
{
    if (useInMemoryDatabase)
    {
        opt.UseInMemoryDatabase("MediCorePMS");
    }
    else
    {
        opt.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"));
    }
});

// ── JWT Authentication ─────────────────────────────────────────────
var jwtKey = builder.Configuration["Jwt:Key"]!;
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer              = builder.Configuration["Jwt:Issuer"],
            ValidAudience            = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        };
    });

builder.Services.AddAuthorization();

// ── Services ───────────────────────────────────────────────────────
builder.Services.AddScoped<TokenService>();

// ── Controllers ────────────────────────────────────────────────────
builder.Services.AddControllers();

// ── CORS (allow frontend on any port during dev) ──────────────────
builder.Services.AddCors(opt =>
    opt.AddPolicy("DevCors", p =>
        p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

// ── Swagger / OpenAPI ──────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title       = "MediCore PMS API",
        Version     = "v1",
        Description = "Pharmacy Management System — Backend API",
    });

    // Add JWT auth to Swagger UI
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header. Enter: **Bearer {token}**",
        Name        = "Authorization",
        In          = ParameterLocation.Header,
        Type        = SecuritySchemeType.ApiKey,
        Scheme      = "Bearer",
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

// ─────────────────────────────────────────────────────────────────
var app = builder.Build();
// ─────────────────────────────────────────────────────────────────

// ── Auto-migrate + seed on startup ────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    if (useInMemoryDatabase)
    {
        ctx.Database.EnsureCreated();
    }
    else
    {
        ctx.Database.Migrate();
    }

    DbInitializer.Seed(ctx);
}

// ── Middleware ────────────────────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "MediCore PMS API v1");
        c.RoutePrefix = string.Empty; // Swagger at root /
    });
}

app.UseCors("DevCors");
app.UseAuthentication();
app.UseAuthorization();
app.MapGet("/health", () => Results.Ok(new { status = "healthy" })).AllowAnonymous();
app.MapControllers();

app.Run();

// ── Declared Types (Must remain at the very bottom) ───────────────
public static class DbInitializer
{
    public static void Seed(AppDbContext ctx)
    {
        if (!ctx.Users.Any())
        {
            var adminUser = new User 
            {
                Name = "Admin User",
                Email = "admin@medicore.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin123!"), 
                Role = "Admin",
                Active = true,
                Avatar = "admin-avatar.png",
                AvatarColor = "#4F46E5"
            };

            ctx.Users.Add(adminUser);
            ctx.SaveChanges();
        }
    }
}
