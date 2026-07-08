using Microsoft.AspNetCore.Identity;
using SistemBeritaAcara.Core.Entities;
using SistemBeritaAcara.Infrastructure.Data;

namespace SistemBeritaAcara.Web.Security;









public class FirstRunMiddleware(RequestDelegate next)
{
    
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

    
    
    private static volatile bool _hasUsers = false;
    private static readonly SemaphoreSlim _checkLock = new(1, 1);

    public async Task InvokeAsync(HttpContext context, IServiceProvider services)
    {
        var path = context.Request.Path.Value ?? "/";

        
        if (IsExcluded(path))
        {
            await next(context);
            return;
        }

        
        if (!_hasUsers)
        {
            await _checkLock.WaitAsync();
            try
            {
                if (!_hasUsers) 
                {
                    using var scope = services.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    _hasUsers = db.Users.Any();
                }
            }
            finally
            {
                _checkLock.Release();
            }
        }

        if (!_hasUsers)
        {
            
            if (!path.Equals("/setup", StringComparison.OrdinalIgnoreCase))
            {
                context.Response.Redirect("/setup");
                return;
            }
        }
        else
        {
            
            if (path.Equals("/setup", StringComparison.OrdinalIgnoreCase))
            {
                context.Response.Redirect("/login");
                return;
            }
        }

        await next(context);
    }

    
    
    
    
    public static void InvalidateCache() => _hasUsers = false;

    
    
    
    public static void MarkHasUsers() => _hasUsers = true;

    private static bool IsExcluded(string path)
    {
        foreach (var prefix in _excluded)
            if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return true;

        
        var ext = Path.GetExtension(path);
        if (!string.IsNullOrEmpty(ext)) return true;

        return false;
    }
}
