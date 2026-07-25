using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using MediCorePMS.Data;
using MediCorePMS.Services;
using MediCorePMS.Models;

var builder = WebApplication.CreateBuilder(args);

// ─────────────────────────────────────────────────────────────────
// Database Configuration
// ─────────────────────────────────────────────────────────────────

var useInMemoryDatabase = builder.Configuration.GetValue(
    "UseInMemoryDatabase",
    builder.Environment.IsDevelopment()
);

builder.Services.AddDbContext<AppDbContext>(options =>
{
    if (useInMemoryDatabase)
    {
        options.UseInMemoryDatabase("MediCorePMS");
    }
    else
    {
        options.UseSqlServer(
            builder.Configuration.GetConnectionString("DefaultConnection")
        );
    }
});

// ─────────────────────────────────────────────────────────────────
// JWT Authentication
// ─────────────────────────────────────────────────────────────────

var jwtKey = builder.Configuration["Jwt:Key"];

if (string.IsNullOrWhiteSpace(jwtKey))
{
    throw new InvalidOperationException(
        "JWT Key is not configured. Set Jwt:Key in appsettings.json or Jwt__Key in Render environment variables."
    );
}

var jwtIssuer = builder.Configuration["Jwt:Issuer"];
var jwtAudience = builder.Configuration["Jwt:Audience"];

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,

            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,

            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtKey)
            )
        };
    });

builder.Services.AddAuthorization();

// ─────────────────────────────────────────────────────────────────
// Application Services
// ─────────────────────────────────────────────────────────────────

builder.Services.AddScoped<TokenService>();

// ─────────────────────────────────────────────────────────────────
// Controllers
// ─────────────────────────────────────────────────────────────────

builder.Services.AddControllers();

// ─────────────────────────────────────────────────────────────────
// CORS
// ─────────────────────────────────────────────────────────────────

// Temporary configuration allowing your frontend to communicate
// with the backend from any origin.
//
// After your Vercel frontend is deployed, you should restrict this
// to your actual Vercel domain for better production security.

builder.Services.AddCors(options =>
{
    options.AddPolicy("DevCors", policy =>
    {
        policy
            .AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});

// ─────────────────────────────────────────────────────────────────
// Swagger / OpenAPI
// ─────────────────────────────────────────────────────────────────

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "MediCore PMS API",
        Version = "v1",
        Description = "Pharmacy Management System — Backend API"
    });

    // JWT Authentication in Swagger
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description =
            "JWT Authorization header. Enter: Bearer {token}",

        Name = "Authorization",

        In = ParameterLocation.Header,

        Type = SecuritySchemeType.ApiKey,

        Scheme = "Bearer"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },

            Array.Empty<string>()
        }
    });
});

// ─────────────────────────────────────────────────────────────────
// Build Application
// ─────────────────────────────────────────────────────────────────

var app = builder.Build();

// ─────────────────────────────────────────────────────────────────
// Database Migration + Seed
// ─────────────────────────────────────────────────────────────────

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;

    try
    {
        var context = services.GetRequiredService<AppDbContext>();

        if (useInMemoryDatabase)
        {
            context.Database.EnsureCreated();
        }
        else
        {
            context.Database.Migrate();
        }

        DbInitializer.Seed(context);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();

        logger.LogError(
            ex,
            "An error occurred while initializing the database."
        );

        throw;
    }
}

// ─────────────────────────────────────────────────────────────────
// Swagger
// ─────────────────────────────────────────────────────────────────

// Swagger is enabled in both Development and Production so that
// you can test the API from Render.

if (app.Environment.IsDevelopment() || app.Environment.IsProduction())
{
    app.UseSwagger();

    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint(
            "/swagger/v1/swagger.json",
            "MediCore PMS API v1"
        );

        // Swagger will be available at:
        // https://your-render-url.onrender.com/swagger

        options.RoutePrefix = "swagger";
    });
}

// ─────────────────────────────────────────────────────────────────
// Middleware
// ─────────────────────────────────────────────────────────────────

app.UseCors("DevCors");

app.UseAuthentication();

app.UseAuthorization();

// ─────────────────────────────────────────────────────────────────
// Render Health Check
// ─────────────────────────────────────────────────────────────────

app.MapGet(
    "/health",
    () => Results.Ok(new
    {
        status = "healthy",
        environment = app.Environment.EnvironmentName
    })
).AllowAnonymous();

// ─────────────────────────────────────────────────────────────────
// Controllers
// ─────────────────────────────────────────────────────────────────

app.MapControllers();

// ─────────────────────────────────────────────────────────────────
// Start Application
// ─────────────────────────────────────────────────────────────────

app.Run();

// ─────────────────────────────────────────────────────────────────
// Database Initializer
// ─────────────────────────────────────────────────────────────────

public static class DbInitializer
{
    public static void Seed(AppDbContext context)
    {
        // Prevent duplicate admin users
        if (!context.Users.Any())
        {
            var adminUser = new User
            {
                Name = "Admin User",

                Email = "admin@medicore.com",

                PasswordHash = BCrypt.Net.BCrypt.HashPassword(
                    "Admin123!"
                ),

                Role = "Admin",

                Active = true,

                Avatar = "admin-avatar.png",

                AvatarColor = "#4F46E5"
            };

            context.Users.Add(adminUser);

            context.SaveChanges();
        }
    }
}