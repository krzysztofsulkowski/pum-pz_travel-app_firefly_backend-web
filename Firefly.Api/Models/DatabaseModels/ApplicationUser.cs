using Microsoft.AspNetCore.Identity;

namespace Firefly.Api.Models.DatabaseModels
{
    public class ApplicationUser : IdentityUser<Guid>
    {
        public string? AvatarUrl { get; set; }
    }
}
