using Firefly.Api.Models.DatabaseModels;
using Microsoft.AspNetCore.Identity;

namespace Firefly.Api.Seeders;

public class AdminSeeder
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole<Guid>> _roleManager;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AdminSeeder> _logger;

    public AdminSeeder(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole<Guid>> roleManager,
        IConfiguration configuration,
        ILogger<AdminSeeder> logger)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SeedAdminAsync()
    {
        const string adminRole = "Administrator";

        if (!await _roleManager.RoleExistsAsync(adminRole))
        {
            var roleResult = await _roleManager.CreateAsync(new IdentityRole<Guid>(adminRole));
            if (!roleResult.Succeeded)
            {
                _logger.LogError("Nie udało się utworzyć roli {Role}.", adminRole);
                return;
            }
        }

        var existingAdmins = await _userManager.GetUsersInRoleAsync(adminRole);
        if (existingAdmins.Count > 0)
        {
            return;
        }

        var email = _configuration["ADMIN_EMAIL"]?.Trim();
        var username = _configuration["ADMIN_USERNAME"]?.Trim();
        if (string.IsNullOrWhiteSpace(username)) username = "admin";
        var password = _configuration["ADMIN_PASSWORD"];

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            _logger.LogCritical("Brak ADMIN_EMAIL lub ADMIN_PASSWORD w .env. Administrator nie został utworzony.");
            return;
        }

        var adminUser = new ApplicationUser
        {
            UserName = username,
            Email = email,
            EmailConfirmed = true
        };

        var createResult = await _userManager.CreateAsync(adminUser, password);
        if (!createResult.Succeeded)
        {
            var errors = string.Join(", ", createResult.Errors.Select(e => e.Description));
            _logger.LogError("Błąd tworzenia administratora: {Errors}", errors);
            return;
        }

        var addRoleResult = await _userManager.AddToRoleAsync(adminUser, adminRole);
        if (!addRoleResult.Succeeded)
        {
            await _userManager.DeleteAsync(adminUser);
            _logger.LogError("Nie udało się przypisać roli {Role} do {Email}.", adminRole, email);
            return;
        }

        _logger.LogInformation("Administrator startowy ({Email}) został utworzony.", email);
    }
}