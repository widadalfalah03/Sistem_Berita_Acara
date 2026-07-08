using Hangfire;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using SistemBeritaAcara.Core.Entities;
using SistemBeritaAcara.Infrastructure;
using SistemBeritaAcara.Infrastructure.Data;
using SistemBeritaAcara.Infrastructure.Jobs;
using SistemBeritaAcara.Web.Components;
using SistemBeritaAcara.Web.Filters;
using SistemBeritaAcara.Web.Hubs;
using SistemBeritaAcara.Web.Services;
using System.Globalization;
using System.Threading.RateLimiting;





var idCulture = new CultureInfo("id-ID");
CultureInfo.DefaultThreadCurrentCulture   = idCulture;
CultureInfo.DefaultThreadCurrentUICulture = idCulture;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddHubOptions(o => o.MaximumReceiveMessageSize = 10 * 1024 * 1024); 


builder.Services.AddScoped<Microsoft.AspNetCore.Components.Authorization.AuthenticationStateProvider, SistemBeritaAcara.Web.Security.IdentityRevalidatingAuthenticationStateProvider>();

builder.Services.AddInfrastructure(builder.Configuration);


builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("login", limiter =>
    {
        limiter.PermitLimit = 10;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        limiter.QueueLimit = 0;
    });
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});


using (var preScope = builder.Services.BuildServiceProvider().CreateScope())
{
    var db = preScope.ServiceProvider.GetRequiredService<AppDbContext>();
    var startupLogger = preScope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");

    try { db.Database.EnsureCreated(); }
    catch (Exception ex) { startupLogger.LogCritical(ex, "Gagal membuat/memverifikasi database. Pastikan SQL Server berjalan dan connection string benar."); throw; }

    
    try
    {
    await db.Database.ExecuteSqlRawAsync(@"
        IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'ProfilePicPath')
            ALTER TABLE [Users] ADD [ProfilePicPath] nvarchar(500) NULL;
        IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('BeritaAcara') AND name = 'Keterangan')
            ALTER TABLE [BeritaAcara] ADD [Keterangan] nvarchar(max) NULL;
        IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('PerangkatBA') AND name = 'Keterangan')
            ALTER TABLE [PerangkatBA] ADD [Keterangan] nvarchar(max) NULL;
        IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('BeritaAcara') AND name = 'PjNoTelp')
            ALTER TABLE [BeritaAcara] ADD [PjNoTelp] nvarchar(50) NULL;
        IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('BeritaAcara') AND name = 'IsReturned')
            ALTER TABLE [BeritaAcara] ADD [IsReturned] bit NOT NULL DEFAULT 0;
        IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('BeritaAcara') AND name = 'ReturnedAt')
            ALTER TABLE [BeritaAcara] ADD [ReturnedAt] datetime2 NULL;
        IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('BeritaAcara') AND name = 'PengunaAlihDaya')
            ALTER TABLE [BeritaAcara] ADD [PengunaAlihDaya] nvarchar(200) NULL;
        IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('BeritaAcara') AND name = 'WasRejected')
            ALTER TABLE [BeritaAcara] ADD [WasRejected] bit NOT NULL DEFAULT 0;

        -- MustChangePw dihapus dari entity C# — hapus kolom dari DB agar INSERT tidak gagal (NOT NULL tanpa DEFAULT)
        IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'MustChangePw')
        BEGIN
            -- Drop default constraint dulu jika ada, baru drop kolom
            DECLARE @dfName nvarchar(200)
            SELECT @dfName = dc.name
            FROM sys.default_constraints dc
            JOIN sys.columns c ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
            WHERE c.object_id = OBJECT_ID('Users') AND c.name = 'MustChangePw'
            IF @dfName IS NOT NULL
                EXEC('ALTER TABLE [Users] DROP CONSTRAINT [' + @dfName + ']')
            ALTER TABLE [Users] DROP COLUMN [MustChangePw]
        END
        
        -- Migrate legacy Tiket SSC to new No. Tiket My SSC
        UPDATE [BeritaAcara] SET [DasarAlokasi] = 'No. Tiket My SSC' WHERE [DasarAlokasi] = 'Tiket SSC';
        
        -- Migrate legacy Approver role to Reviewer
        UPDATE [AspNetRoles] SET [Name] = 'Reviewer', [NormalizedName] = 'REVIEWER' WHERE [Name] = 'Approver';
        UPDATE [Users] SET [Role] = 'Reviewer' WHERE [Role] = 'Approver';

        -- Migrate MenyerahkanId: ubah FK dari Pegawai ke Users
        -- Cek apakah FK lama (ke Pegawai) masih ada; jika ya, drop dan migrasi data
        IF EXISTS (
            SELECT 1 FROM sys.foreign_keys fk
            INNER JOIN sys.tables t  ON fk.parent_object_id    = t.object_id
            INNER JOIN sys.tables rt ON fk.referenced_object_id = rt.object_id
            WHERE t.name = 'BeritaAcara' AND rt.name = 'Pegawai'
              AND EXISTS (
                  SELECT 1 FROM sys.foreign_key_columns fkc
                  INNER JOIN sys.columns c ON fkc.parent_object_id = c.object_id AND fkc.parent_column_id = c.column_id
                  WHERE fkc.constraint_object_id = fk.object_id AND c.name = 'MenyerahkanId'
              )
        )
        BEGIN
            -- Drop FK lama ke Pegawai
            DECLARE @fkMenyerahkan nvarchar(200)
            SELECT TOP 1 @fkMenyerahkan = fk.name
            FROM sys.foreign_keys fk
            INNER JOIN sys.tables t  ON fk.parent_object_id    = t.object_id
            INNER JOIN sys.tables rt ON fk.referenced_object_id = rt.object_id
            WHERE t.name = 'BeritaAcara' AND rt.name = 'Pegawai'
              AND EXISTS (
                  SELECT 1 FROM sys.foreign_key_columns fkc
                  INNER JOIN sys.columns c ON fkc.parent_object_id = c.object_id AND fkc.parent_column_id = c.column_id
                  WHERE fkc.constraint_object_id = fk.object_id AND c.name = 'MenyerahkanId'
              )
            IF @fkMenyerahkan IS NOT NULL
                EXEC('ALTER TABLE [BeritaAcara] DROP CONSTRAINT [' + @fkMenyerahkan + ']')

            -- Normalisasi: set 0 → NULL
            UPDATE [BeritaAcara] SET [MenyerahkanId] = NULL WHERE [MenyerahkanId] = 0

            -- Konversi PegawaiId → UserId (via Users.PegawaiId)
            UPDATE ba
            SET ba.[MenyerahkanId] = u.[Id]
            FROM [BeritaAcara] ba
            INNER JOIN [Users] u ON u.[PegawaiId] = ba.[MenyerahkanId]
            WHERE ba.[MenyerahkanId] IS NOT NULL

            -- Nullify nilai sisa yang tidak cocok dengan Users.Id mana pun
            UPDATE [BeritaAcara]
            SET [MenyerahkanId] = NULL
            WHERE [MenyerahkanId] IS NOT NULL
              AND [MenyerahkanId] NOT IN (SELECT [Id] FROM [Users])

            -- Tambah FK baru ke Users
            ALTER TABLE [BeritaAcara] ADD CONSTRAINT [FK_BeritaAcara_Users_MenyerahkanId]
                FOREIGN KEY ([MenyerahkanId]) REFERENCES [Users]([Id]) ON DELETE NO ACTION
        END
    ");
    }
    catch (Exception ex)
    {
        startupLogger.LogError(ex, "Satu atau lebih migrasi SQL startup gagal. Aplikasi tetap berjalan namun beberapa kolom mungkin belum ada.");
    }

    var roleManager = preScope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<int>>>();
    var userManager = preScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

    
    foreach (var role in new[] { "AdminIT", "AdminBA", "Reviewer" })
    {
        if (!await roleManager.RoleExistsAsync(role))
            await roleManager.CreateAsync(new IdentityRole<int>(role));
    }

    
    
    var usersNeedJabatan = db.Users.Include(u => u.Pegawai).Where(u => u.Jabatan == null && u.PegawaiId != null).ToList();
    foreach (var u in usersNeedJabatan)
        u.Jabatan = u.Pegawai?.Jabatan;
    if (usersNeedJabatan.Any())
        await db.SaveChangesAsync();

    
    var allUsers = await db.Users.ToListAsync();
    foreach (var u in allUsers)
    {
        if (!string.IsNullOrEmpty(u.Role) && !await userManager.IsInRoleAsync(u, u.Role))
            await userManager.AddToRoleAsync(u, u.Role);
    }
}

builder.Services.ConfigureApplicationCookie(opt =>
{
    opt.LoginPath = "/login";
    opt.AccessDeniedPath = "/akses-ditolak";
    opt.ExpireTimeSpan = TimeSpan.FromDays(1);
    opt.SlidingExpiration = true;
});

builder.Services.AddAuthorization();


builder.Services.AddSignalR();
builder.Services.AddSingleton<BaUpdateService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}


app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost,
    
    KnownProxies = { System.Net.IPAddress.Loopback, System.Net.IPAddress.IPv6Loopback }
});


app.Use(async (context, next) =>
{
    context.Request.Headers["ngrok-skip-browser-warning"] = "true";
    await next();
});

app.UseHttpsRedirection();
app.UseStaticFiles(); 
app.UseAntiforgery();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();


app.UseMiddleware<SistemBeritaAcara.Web.Security.FirstRunMiddleware>();

app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = [new HangfireAdminAuthFilter()]
});

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();


app.MapHub<BaHub>("/hubs/ba");


app.MapPost("/account/login", async (
    HttpContext ctx,
    SignInManager<ApplicationUser> signInManager,
    UserManager<ApplicationUser> userManager) =>
{
    var form = await ctx.Request.ReadFormAsync();
    var email = form["email"].ToString();
    var password = form["password"].ToString();
    var returnUrl = form["returnUrl"].ToString();
    
    if (string.IsNullOrEmpty(returnUrl) || !returnUrl.StartsWith('/') || returnUrl.StartsWith("/" + "/") || returnUrl.StartsWith("/\\"))
        returnUrl = "/dashboard";

    var user = await userManager.FindByEmailAsync(email);
    if (user is null || user.IsDeleted)
        return Results.Redirect($"/login?error=invalid");

    var result = await signInManager.PasswordSignInAsync(user, password, isPersistent: false, lockoutOnFailure: true);
    if (result.Succeeded)
    {
        return Results.Redirect(returnUrl);
    }
    if (result.IsLockedOut)
        return Results.Redirect("/login?error=locked");

    return Results.Redirect("/login?error=invalid");
}).DisableAntiforgery().RequireRateLimiting("login");

app.MapPost("/account/logout", async (
    HttpContext ctx,
    SignInManager<ApplicationUser> signInManager) =>
{
    await signInManager.SignOutAsync();
    return Results.Redirect("/login");
});

app.MapGet("/api/preview/pdf/{baId:int}", async (
    int baId,
    HttpContext ctx,
    SistemBeritaAcara.Infrastructure.Data.AppDbContext db) =>
{
    ctx.Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
    ctx.Response.Headers["Pragma"] = "no-cache";
    ctx.Response.Headers["Expires"] = "0";

    var ba = await db.BeritaAcara.FindAsync(baId);
    if (ba == null || (ba.DocxPath == null && ba.DocxFinalPath == null)) return Results.NotFound();

    
    if (ba.Status != "Approved" && ba.Status != "Archived")
    {
        var role = ctx.User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
        if (role != "AdminIT" && role != "Reviewer")
        {
            var userIdStr = ctx.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(userIdStr, out int userId))
            {
                if (ba.CreatedBy != userId && ba.MengetahuiId != userId && ba.MenyerahkanId != userId)
                    return Results.Forbid();
            }
            else
            {
                return Results.Forbid();
            }
        }
    }

    
    string targetDocx = !string.IsNullOrEmpty(ba.DocxFinalPath) ? ba.DocxFinalPath : ba.DocxPath!;
    var pdfPath = Path.Combine(Directory.GetCurrentDirectory(), "AppFiles",
        targetDocx.TrimStart('/').Replace("files/", "").Replace("/", Path.DirectorySeparatorChar.ToString()).Replace(".docx", ".pdf"));

    if (!File.Exists(pdfPath)) return Results.NotFound();

    var bytes = await File.ReadAllBytesAsync(pdfPath);
    return Results.File(bytes, "application/pdf", enableRangeProcessing: true);
}).RequireAuthorization().DisableAntiforgery();


app.MapGet("/api/ba/{baId:int}/download", async (
    int baId,
    HttpContext ctx,
    SistemBeritaAcara.Infrastructure.Data.AppDbContext db) =>
{
    var ba = await db.BeritaAcara.FindAsync(baId);
    if (ba == null) return Results.NotFound();

    
    if (ba.Status != "Approved" && ba.Status != "Archived")
    {
        var role = ctx.User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
        if (role != "AdminIT" && role != "Reviewer")
        {
            var userIdStr = ctx.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(userIdStr, out int userId))
            {
                if (ba.CreatedBy != userId && ba.MengetahuiId != userId && ba.MenyerahkanId != userId)
                    return Results.Forbid();
            }
            else
            {
                return Results.Forbid();
            }
        }
    }

    string targetPath = ba.DocxFinalPath ?? ba.DocxPath;
    if (string.IsNullOrEmpty(targetPath)) return Results.NotFound();

    var pdfPath = Path.Combine(Directory.GetCurrentDirectory(), "AppFiles",
        targetPath.TrimStart('/').Replace("files/", "").Replace("/", Path.DirectorySeparatorChar.ToString()).Replace(".docx", ".pdf"));

    if (!File.Exists(pdfPath)) return Results.NotFound();

    string filename = !string.IsNullOrEmpty(ba.NomorSurat) 
        ? $"Berita_Acara_{ba.NomorSurat.Replace("/", "_")}.pdf" 
        : $"Berita_Acara_{ba.Id}.pdf";

    var bytes = await File.ReadAllBytesAsync(pdfPath);
    return Results.File(bytes, "application/pdf", filename);
}).RequireAuthorization().DisableAntiforgery();


app.MapGet("/files/{category}/{**filename}", async (
    string category, 
    string filename,
    HttpContext ctx) => 
{
    var physicalPath = Path.Combine(Directory.GetCurrentDirectory(), "AppFiles", category, filename.Replace("/", Path.DirectorySeparatorChar.ToString()));
    if (!File.Exists(physicalPath)) return Results.NotFound();
    
    
    string ext = Path.GetExtension(physicalPath).ToLower();
    string contentType = ext switch {
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        ".pdf" => "application/pdf",
        ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        _ => "application/octet-stream"
    };
    
    var bytes = await File.ReadAllBytesAsync(physicalPath);
    return Results.File(bytes, contentType, enableRangeProcessing: true);
}).RequireAuthorization().DisableAntiforgery();




RecurringJob.AddOrUpdate<DueDateCheckerJob>(
    "cek-jatuh-tempo",
    job => job.CheckDueDatesAsync(),
    Cron.Daily(7));

app.Run();
