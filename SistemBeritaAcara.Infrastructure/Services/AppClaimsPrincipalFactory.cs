using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using SistemBeritaAcara.Core.Entities;
using System.Security.Claims;

namespace SistemBeritaAcara.Infrastructure.Services;

/// <summary>
/// Menambahkan "nama" dan "app_role" sebagai claim saat login,
/// sehingga MainLayout tidak perlu query DB untuk mendapatkan info user.
/// </summary>
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
        // Tambahkan claim kustom agar tidak perlu DB lookup di layout
        identity.AddClaim(new Claim("nama", user.Nama ?? string.Empty));
        identity.AddClaim(new Claim("app_role", user.Role ?? string.Empty));
        return identity;
    }
}
