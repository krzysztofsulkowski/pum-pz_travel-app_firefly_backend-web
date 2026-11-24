using Firefly.Api.Extensions;
using Firefly.Api.Models;
using Firefly.Api.Models.DatabaseModels;
using Firefly.Api.Services;
using Firefly.Api.Services.Storage;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;

// ==========================================
// ŁADOWANIE KONFIGURACJI (.env)
// ==========================================
WebApplicationExtensions.LoadDotEnv();

var builder = WebApplication.CreateBuilder(args);

// ==========================================
// BAZA DANYCH I IDENTITY
// ==========================================
string? rawConnectionString = builder.Configuration.GetValue<bool>("IS_IN_CONTAINER")
    ? builder.Configuration.GetConnectionString("DefaultConnection")
    : builder.Configuration.GetConnectionString("DefaultConnection_LOCAL");

var connectionString = ConnectionStringConverter.Convert(rawConnectionString);

builder.Services.AddDbContext<FireflyDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 8;
    options.Password.RequireUppercase = true;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;
})
    .AddEntityFrameworkStores<FireflyDbContext>()
    .AddDefaultTokenProviders();

// ==========================================
// REJESTRACJA METOD ROZSZERZAJĄCYCH (DI)
// ==========================================
builder.Services.AddCustomAuthentication(builder.Configuration);
builder.Services.AddCustomCors(builder.Configuration);
builder.Services.AddCustomRateLimiter();
builder.Services.AddCustomSwagger();
builder.Services.AddBusinessServices(builder.Configuration);

builder.Services.AddMemoryCache();
builder.Services.AddHealthChecks();
builder.Services.AddResponseCompression();

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services.AddEndpointsApiExplorer();

// ==========================================
// PIPELINE APLIKACJI & INICJALIZACJA
// ==========================================

builder.Services.AddHttpContextAccessor();

var app = builder.Build();

app.UseCustomMiddlewarePipeline();

await app.InitializeDatabaseAsync();

app.Run();