using Microsoft.AspNetCore.Identity;
using SistemBeritaAcara.Core.Entities;
using SistemBeritaAcara.Infrastructure.Data;

namespace SistemBeritaAcara.Web.Security;

/// <summary>
/// Middleware yang mendeteksi apakah aplikasi baru pertama kali dijalankan
/// (belum ada user sama sekali di database). Jika ya, redirect semua request
/// ke halaman /setup agar Admin IT dapat membuat akun pertamanya sendiri.
/// 
/// Setelah Admin IT dibuat, middleware ini hanya menjadi pass-through.
/// Jika ada user dan request menuju /setup, redirect ke /login.
/// </summary>
public class FirstRunMiddleware(RequestDelegate next)
{
    // Path yang dikecualikan dari redirect (aset statis, blazor internals, auth endpoints)
    private static readonly string[] _excluded =
    [
        "/_blazor",
        "/_framework",
        "/account/",
        "/_content/",
        "/favicon",
        "/images/",
        "/js/",
        "/files/",
    ];

    // Cache in-memory agar tidak query DB setiap request setelah ada user.
    // volatile: memastikan semua thread membaca nilai terbaru tanpa race condition.
    private static volatile bool _hasUsers = false;

    public async Task InvokeAsync(HttpContext context, IServiceProvider services)
    {
        var path = context.Request.Path.Value ?? "/";

        // Lewati aset statis dan endpoint internal
        if (IsExcluded(path))
        {
            await next(context);
            return;
        }

        // Jika cache sudah tahu ada user, skip pengecekan DB
        if (!_hasUsers)
        {
            using var scope = services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            _hasUsers = db.Users.Any();
        }

        if (!_hasUsers)
        {
            // Belum ada user sama sekali → semua request (kecuali /setup) diarahkan ke /setup
            if (!path.Equals("/setup", StringComparison.OrdinalIgnoreCase))
            {
                context.Response.Redirect("/setup");
                return;
            }
        }
        else
        {
            // Sudah ada user → /setup tidak boleh diakses lagi
            if (path.Equals("/setup", StringComparison.OrdinalIgnoreCase))
            {
                context.Response.Redirect("/login");
                return;
            }
        }

        await next(context);
    }

    /// <summary>
    /// Dipanggil dari SetupAdminIT.razor setelah Admin IT berhasil dibuat,
    /// untuk memperbarui cache tanpa perlu restart app.
    /// </summary>
    public static void InvalidateCache() => _hasUsers = false;

    /// <summary>
    /// Dipanggil dari SetupAdminIT.razor setelah Admin IT berhasil dibuat.
    /// </summary>
    public static void MarkHasUsers() => _hasUsers = true;

    private static bool IsExcluded(string path)
    {
        foreach (var prefix in _excluded)
            if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return true;

        // Exclude file extensions (CSS, JS, png, dll)
        var ext = Path.GetExtension(path);
        if (!string.IsNullOrEmpty(ext)) return true;

        return false;
    }
}
