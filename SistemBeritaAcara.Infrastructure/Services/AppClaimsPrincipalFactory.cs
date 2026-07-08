using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using SistemBeritaAcara.Core.Entities;
using System.Security.Claims;

namespace SistemBeritaAcara.Infrastructure.Services;





public class AppClaimsPrincipalFactory
    : UserClaimsPrincipalFactory<ApplicationUser, IdentityRole<int>>
{
    public AppClaimsPrincipalFactory(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole<int>> roleManager,
        IOptions<IdentityOptions> options)
        : base(userManager, roleManager, options) { }

    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);
        
        identity.AddClaim(new Claim("nama", user.Nama ?? string.Empty));
        identity.AddClaim(new Claim("app_role", user.Role ?? string.Empty));
        return identity;
    }
}
