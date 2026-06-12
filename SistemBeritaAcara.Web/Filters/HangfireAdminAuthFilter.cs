using Hangfire.Dashboard;

namespace SistemBeritaAcara.Web.Filters;

public class HangfireAdminAuthFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var http = context.GetHttpContext();
        return http.User.Identity?.IsAuthenticated == true
            && http.User.IsInRole("AdminIT");
    }
}
