using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace Firefly.Api.Models.Dto.Users
{
    public class UserDto
    {
        public Guid? UserId { get; set; } = Guid.Empty;

        [Required]
        public string UserName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        public string? AvatarUrl { get; set; }

        [Required]
        public Guid? RoleId { get; set; } = Guid.Empty;
        public string RoleName { get; set; } = string.Empty;

        public bool IsLocked { get; set; }
        public bool EmailConfirmed { get; set; }
    }
}