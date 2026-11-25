using Firefly.Api.Extensions;
using Firefly.Api.Models;
using Firefly.Api.Models.DatabaseModels;
using Firefly.Api.Models.Dto;
using Firefly.Api.Models.Dto.Users;
using Firefly.Api.Services.Errors;
using Firefly.Api.Services.Interfaces;
using Firefly.Api.Services.Results;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Firefly.Api.Services.Auth;

public class UserManagementService : IUserManagementService
{
    private readonly FireflyDbContext _context;
    private readonly ILogger<UserManagementService> _logger;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole<Guid>> _roleManager;
    private readonly IEmailService _emailService;
    private readonly IConfiguration _configuration;

    private const string ObjectName = "User";

    public UserManagementService(
        FireflyDbContext context,
        ILogger<UserManagementService> logger,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole<Guid>> roleManager,
        IEmailService emailService,
        IConfiguration configuration)
    {
        _context = context;
        _logger = logger;
        _userManager = userManager;
        _roleManager = roleManager;
        _emailService = emailService;
        _configuration = configuration;
    }

    public async Task<ServiceResult<List<IdentityRole<Guid>>>> GetAllRolesAsync()
    {
        try
        {
            var roles = await _roleManager.Roles.AsNoTracking().ToListAsync();
            return ServiceResult<List<IdentityRole<Guid>>>.Success(roles);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Wystąpił błąd podczas pobierania ról.");
            return ServiceResult<List<IdentityRole<Guid>>>.Failure(CommonErrors.FetchFailed("Roles"));
        }
    }

    public async Task<ServiceResult<PagedResult<UserDto>>> GetAllUsersAsync(PagedQueryDto query)
    {
        try
        {
            var page = query.Page < 1 ? 1 : query.Page;
            var pageSize = Math.Clamp(query.PageSize <= 0 ? 10 : query.PageSize, 1, 100);
            var now = DateTimeOffset.UtcNow;

            var usersQuery =
                from u in _context.Users.AsNoTracking()
                join ur in _context.UserRoles on u.Id equals ur.UserId into userRoles
                from ur in userRoles.DefaultIfEmpty()
                join r in _context.Roles on ur.RoleId equals r.Id into roles
                from r in roles.DefaultIfEmpty()
                select new UserDto
                {
                    UserId = u.Id,
                    UserName = u.UserName ?? string.Empty,
                    Email = u.Email ?? string.Empty,
                    AvatarUrl = u.AvatarUrl,
                    RoleId = r != null ? (Guid?)r.Id : null,
                    RoleName = r == null ? string.Empty : r.Name ?? string.Empty,
                    EmailConfirmed = u.EmailConfirmed,
                    IsLocked = u.LockoutEnabled && u.LockoutEnd != null && u.LockoutEnd > now
                };

            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var pattern = query.Search.Trim().ToContainsPattern();
                usersQuery = usersQuery.Where(u =>
                    EF.Functions.ILike(u.UserName, pattern, "\\") ||
                    EF.Functions.ILike(u.Email, pattern, "\\"));
            }

            var descending = string.Equals(query.SortDirection, "desc", StringComparison.OrdinalIgnoreCase);
            var ordered = (query.SortBy?.Trim().ToLowerInvariant()) switch
            {
                "email" => descending ? usersQuery.OrderByDescending(u => u.Email) : usersQuery.OrderBy(u => u.Email),
                "rolename" or "role" => descending ? usersQuery.OrderByDescending(u => u.RoleName) : usersQuery.OrderBy(u => u.RoleName),
                "islocked" => descending ? usersQuery.OrderByDescending(u => u.IsLocked) : usersQuery.OrderBy(u => u.IsLocked),
                _ => descending ? usersQuery.OrderByDescending(u => u.UserName) : usersQuery.OrderBy(u => u.UserName)
            };

            var totalCount = await usersQuery.CountAsync();
            var pagedItems = await ordered
                .ThenBy(u => u.UserId)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return ServiceResult<PagedResult<UserDto>>.Success(new PagedResult<UserDto>
            {
                Items = pagedItems,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd pobierania listy użytkowników.");
            return ServiceResult<PagedResult<UserDto>>.Failure(CommonErrors.DataProcessingError("Błąd pobierania listy użytkowników."));
        }
    }

    public async Task<ServiceResult> LockUser(Guid userId, Guid currentUserId)
    {
        if (userId == currentUserId)
        {
            return ServiceResult.Failure(UserManagementErrors.CannotLockSelf());
        }

        try
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null)
                return ServiceResult.NotFound(CommonErrors.NotFound(ObjectName, userId.ToString()));

            await _userManager.SetLockoutEnabledAsync(user, true);
            await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);

            var activeTokens = await _context.RefreshTokens
                .Where(t => t.UserId == user.Id && !t.IsRevoked)
                .ToListAsync();

            var now = DateTime.UtcNow;
            foreach (var t in activeTokens)
            {
                t.IsRevoked = true;
                t.RevokedDate = now;
            }
            await _context.SaveChangesAsync();

            _logger.LogInformation("Użytkownik {UserId} został zablokowany, a jego sesje zerwane.", userId);
            return ServiceResult.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd blokowania konta użytkownika {UserId}", userId);
            return ServiceResult.Failure(CommonErrors.UpdateFailed(ObjectName, userId.ToString(), "Błąd blokowania konta."));
        }
    }

    public async Task<ServiceResult> UnlockUser(Guid userId)
    {
        try
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null)
                return ServiceResult.NotFound(CommonErrors.NotFound(ObjectName, userId.ToString()));

            await _userManager.SetLockoutEndDateAsync(user, null);

            _logger.LogInformation("Użytkownik {UserId} został odblokowany.", userId);
            return ServiceResult.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd odblokowywania użytkownika {UserId}", userId);
            return ServiceResult.Failure(CommonErrors.UpdateFailed(ObjectName, userId.ToString(), "Błąd odblokowania konta."));
        }
    }

    public async Task<ServiceResult<UserDto>> CreateUser(UserDto user, ClaimsPrincipal currentUser)
    {
        if (string.IsNullOrWhiteSpace(user.Email))
        {
            return ServiceResult<UserDto>.Failure(AuthErrors.EmailRequired());
        }

        if (user.RoleId is null || user.RoleId == Guid.Empty)
        {
            return ServiceResult<UserDto>.Failure("Nieprawidłowy identyfikator roli.");
        }

        var roleGuid = user.RoleId.Value;

        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var cleanEmail = user.Email.Trim().ToLowerInvariant();
            var existingUserByEmail = await _userManager.FindByEmailAsync(cleanEmail);
            if (existingUserByEmail != null)
                return ServiceResult<UserDto>.Failure(AuthErrors.UserAlreadyExists());

            var role = await _context.Roles.FindAsync(roleGuid);
            if (role == null || string.IsNullOrEmpty(role.Name))
                return ServiceResult<UserDto>.Failure(UserManagementErrors.RoleNotFound(roleGuid.ToString()));

            var isRoleAdmin = string.Equals(role.Name, "Administrator", StringComparison.OrdinalIgnoreCase);

            if (isRoleAdmin && (currentUser == null || !currentUser.IsInRole("Administrator")))
            {
                _logger.LogWarning("Użytkownik bez uprawnień próbował utworzyć admina.");
                return ServiceResult<UserDto>.Failure(UserManagementErrors.CannotCreateAdminWithoutPrivileges());
            }

            var newUser = new ApplicationUser
            {
                Email = cleanEmail,
                UserName = string.IsNullOrWhiteSpace(user.UserName) ? cleanEmail.Split('@')[0] : user.UserName.Trim(),
                EmailConfirmed = false
            };

            var createResult = await _userManager.CreateAsync(newUser);
            if (!createResult.Succeeded)
            {
                var errors = string.Join(", ", createResult.Errors.Select(e => e.Description));
                return ServiceResult<UserDto>.Failure(UserManagementErrors.CreateFailed(errors));
            }

            var roleResult = await _userManager.AddToRoleAsync(newUser, role.Name);
            if (!roleResult.Succeeded)
            {
                var errors = string.Join(", ", roleResult.Errors.Select(e => e.Description));
                return ServiceResult<UserDto>.Failure(UserManagementErrors.UpdateFailed("Nie udało się przypisać roli: " + errors));
            }

            var token = await _userManager.GeneratePasswordResetTokenAsync(newUser);
            var setupUrl = FrontendOriginResolver.BuildAccountSetupUrl(
                _configuration,
                token,
                newUser.Email!,
                isAdmin: isRoleAdmin
            );

            var emailResult = await _emailService.SendAccountSetupAsync(newUser.Email!, newUser.UserName ?? newUser.Email!, setupUrl);
            if (!emailResult.IsSuccess)
            {
                return ServiceResult<UserDto>.Failure("Nie udało się wysłać e-maila z linkiem. Konto nie zostało utworzone.");
            }

            await transaction.CommitAsync();

            user.UserId = newUser.Id;
            user.UserName = newUser.UserName;
            user.Email = newUser.Email;
            user.RoleName = role.Name;
            user.EmailConfirmed = false;
            return ServiceResult<UserDto>.Success(user);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd podczas tworzenia użytkownika {Email}", user.Email);
            return ServiceResult<UserDto>.Failure(CommonErrors.InternalServerError("Błąd zapisu nowego użytkownika."));
        }
    }

    public async Task<ServiceResult> UpdateUser(UserDto model)
    {
        if (model.UserId is null || model.UserId == Guid.Empty)
        {
            return ServiceResult.Failure(CommonErrors.BadRequest("Nieprawidłowy identyfikator użytkownika."));
        }

        if (string.IsNullOrWhiteSpace(model.Email))
        {
            return ServiceResult.Failure(AuthErrors.EmailRequired());
        }

        if (model.RoleId is null || model.RoleId == Guid.Empty)
        {
            return ServiceResult.Failure("Nieprawidłowy identyfikator roli.");
        }

        var userId = model.UserId.Value;
        var roleGuid = model.RoleId.Value;

        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null)
                return ServiceResult.NotFound(CommonErrors.NotFound(ObjectName, userId.ToString()));

            var role = await _context.Roles.FindAsync(roleGuid);
            if (role == null || string.IsNullOrEmpty(role.Name))
                return ServiceResult.Failure(UserManagementErrors.RoleNotFound(roleGuid.ToString()));

            var cleanEmail = model.Email.Trim().ToLowerInvariant();
            if (!string.Equals(user.Email, cleanEmail, StringComparison.OrdinalIgnoreCase))
            {
                var existingUserWithNewEmail = await _userManager.FindByEmailAsync(cleanEmail);
                if (existingUserWithNewEmail != null && existingUserWithNewEmail.Id != user.Id)
                {
                    return ServiceResult.Failure(AuthErrors.UserAlreadyExists());
                }

                var setEmailResult = await _userManager.SetEmailAsync(user, cleanEmail);
                if (!setEmailResult.Succeeded)
                {
                    var errors = string.Join(", ", setEmailResult.Errors.Select(e => e.Description));
                    return ServiceResult.Failure(UserManagementErrors.UpdateFailed(errors));
                }
            }

            if (!string.IsNullOrWhiteSpace(model.UserName))
            {
                var cleanUserName = model.UserName.Trim();
                if (!string.Equals(user.UserName, cleanUserName, StringComparison.Ordinal))
                {
                    var setNameResult = await _userManager.SetUserNameAsync(user, cleanUserName);
                    if (!setNameResult.Succeeded)
                    {
                        var errors = string.Join(", ", setNameResult.Errors.Select(e => e.Description));
                        return ServiceResult.Failure(UserManagementErrors.UpdateFailed(errors));
                    }
                }
            }

            var currentRoles = await _userManager.GetRolesAsync(user);
            if (!currentRoles.Contains(role.Name))
            {
                if (currentRoles.Count > 0)
                {
                    var removeRoleResult = await _userManager.RemoveFromRolesAsync(user, currentRoles);
                    if (!removeRoleResult.Succeeded)
                    {
                        var errors = string.Join(", ", removeRoleResult.Errors.Select(e => e.Description));
                        return ServiceResult.Failure(UserManagementErrors.UpdateFailed(errors));
                    }
                }

                var addRoleResult = await _userManager.AddToRoleAsync(user, role.Name);
                if (!addRoleResult.Succeeded)
                {
                    var errors = string.Join(", ", addRoleResult.Errors.Select(e => e.Description));
                    return ServiceResult.Failure(UserManagementErrors.UpdateFailed(errors));
                }

                var activeTokens = await _context.RefreshTokens
                    .Where(t => t.UserId == user.Id && !t.IsRevoked)
                    .ToListAsync();

                var now = DateTime.UtcNow;
                foreach (var t in activeTokens)
                {
                    t.IsRevoked = true;
                    t.RevokedDate = now;
                }
                await _context.SaveChangesAsync();
            }

            await transaction.CommitAsync();
            return ServiceResult.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd aktualizacji użytkownika {UserId}", userId);
            return ServiceResult.Failure(CommonErrors.UpdateFailed(ObjectName, userId.ToString()));
        }
    }

    public async Task<ServiceResult<UserDto>> GetUserById(Guid userId)
    {
        try
        {
            var now = DateTimeOffset.UtcNow;
            var userDto = await (
                from u in _context.Users.AsNoTracking()
                where u.Id == userId
                join ur in _context.UserRoles on u.Id equals ur.UserId into urGroup
                from ur in urGroup.DefaultIfEmpty()
                join r in _context.Roles on ur.RoleId equals r.Id into rGroup
                from r in rGroup.DefaultIfEmpty()
                select new UserDto
                {
                    UserId = u.Id,
                    UserName = u.UserName ?? string.Empty,
                    Email = u.Email ?? string.Empty,
                    AvatarUrl = u.AvatarUrl,
                    RoleId = r != null ? (Guid?)r.Id : null,
                    RoleName = r != null ? r.Name ?? string.Empty : string.Empty,
                    EmailConfirmed = u.EmailConfirmed,
                    IsLocked = u.LockoutEnabled && u.LockoutEnd != null && u.LockoutEnd > now
                }
            ).FirstOrDefaultAsync();

            if (userDto == null)
                return ServiceResult<UserDto>.NotFound(CommonErrors.NotFound(ObjectName, userId.ToString()));

            return ServiceResult<UserDto>.Success(userDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd pobierania użytkownika {UserId}", userId);
            return ServiceResult<UserDto>.Failure(CommonErrors.FetchFailed(ObjectName));
        }
    }

    public async Task<ServiceResult> ResendSetupLinkAsync(Guid userId)
    {
        try
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null)
            {
                return ServiceResult.NotFound(CommonErrors.NotFound(ObjectName, userId.ToString()));
            }

            if (user.EmailConfirmed)
            {
                return ServiceResult.Failure("To konto zostało już aktywowane. Użytkownik posiada aktywne hasło.");
            }

            var roles = await _userManager.GetRolesAsync(user);
            var isRoleAdmin = roles.Contains("Administrator", StringComparer.OrdinalIgnoreCase);

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var setupUrl = FrontendOriginResolver.BuildAccountSetupUrl(
                _configuration,
                token,
                user.Email!,
                isAdmin: isRoleAdmin
            );

            var emailResult = await _emailService.SendAccountSetupAsync(
                user.Email!,
                user.UserName ?? user.Email!,
                setupUrl
            );

            if (!emailResult.IsSuccess)
            {
                return ServiceResult.Failure("Nie udało się wysłać wiadomości e-mail. Spróbuj ponownie później.");
            }

            _logger.LogInformation("Wysłano ponownie link aktywacyjny dla użytkownika {Email}", user.Email);
            return ServiceResult.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd ponownego wysyłania linku aktywacyjnego dla {UserId}", userId);
            return ServiceResult.Failure(CommonErrors.InternalServerError("Błąd podczas ponownego wysyłania linku."));
        }
    }
}