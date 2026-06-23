using Hangfire;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SistemBeritaAcara.Core.Entities;
using SistemBeritaAcara.Infrastructure;
using SistemBeritaAcara.Infrastructure.Data;
using SistemBeritaAcara.Infrastructure.Jobs;
using SistemBeritaAcara.Web.Components;
using SistemBeritaAcara.Web.Filters;
using System.Globalization;

// ── Atur kultur global ke Bahasa Indonesia ──────────────────────────────────
// Semua format tanggal (ToString("MMMM"), dll.) otomatis menggunakan nama bulan
// dalam Bahasa Indonesia (misal: "Juni" bukan "June") tanpa perlu CultureInfo
// per-panggilan di seluruh aplikasi.
var idCulture = new CultureInfo("id-ID");
CultureInfo.DefaultThreadCurrentCulture   = idCulture;
CultureInfo.DefaultThreadCurrentUICulture = idCulture;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddInfrastructure(builder.Configuration);

// Ensure database and initial roles are created before the app (and Hangfire) starts
using (var preScope = builder.Services.BuildServiceProvider().CreateScope())
{
    var db = preScope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();

    // Add columns that may be missing when DB was created before the entity was updated
    await db.Database.ExecuteSqlRawAsync(@"
        IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'ProfilePicPath')
            ALTER TABLE [Users] ADD [ProfilePicPath] nvarchar(500) NULL;
        IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('BeritaAcara') AND name = 'PjNoTelp')
            ALTER TABLE [BeritaAcara] ADD [PjNoTelp] nvarchar(50) NULL;
        IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('BeritaAcara') AND name = 'IsReturned')
            ALTER TABLE [BeritaAcara] ADD [IsReturned] bit NOT NULL DEFAULT 0;
        IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('BeritaAcara') AND name = 'ReturnedAt')
            ALTER TABLE [BeritaAcara] ADD [ReturnedAt] datetime2 NULL;
    ");

    var roleManager = preScope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<int>>>();
    var userManager = preScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

    // ── 1. Roles ──────────────────────────────────────────────────────────
    foreach (var role in new[] { "AdminIT", "AdminGudangBarang", "Approver" })
    {
        if (!await roleManager.RoleExistsAsync(role))
            await roleManager.CreateAsync(new IdentityRole<int>(role));
    }

    // ── 2. Seed Pegawai (9 orang dari data Excel dummy) ───────────────────
    if (!db.Pegawai.Any())
    {
        db.Pegawai.AddRange(
            new Pegawai { Nama = "Romi Aprilian Mustafa",   NoPekerja = "1", Jabatan = "Jr Officer I Operation Delivery",          FungsiDirektorat = "IT Sumbagsel",                          Email = "romiaprilian7406@gmail.com",        CostCenter = "123456", IsAktif = true },
            new Pegawai { Nama = "Bruce Wayne",              NoPekerja = "2", Jabatan = "SBM Industry I",                           FungsiDirektorat = "Corporate Sales Sumbagsel",             Email = "ggyur21887@gmail.com",              CostCenter = "223456", IsAktif = true },
            new Pegawai { Nama = "Tony Stark",               NoPekerja = "3", Jabatan = "Jr Officer I Fuel Channel Adm",             FungsiDirektorat = "Retail Sales Sumbagsel",                Email = "guidotorvalds1985@gmail.com",       CostCenter = "323456", IsAktif = true },
            new Pegawai { Nama = "Muhammad Widad Alfalah",   NoPekerja = "4", Jabatan = "Aviation FT Manager Sultan Thaha",          FungsiDirektorat = "Corp. Operation & Serv. Sumbagsel",     Email = "widadalfalah03@gmail.com",          CostCenter = "423456", IsAktif = true },
            new Pegawai { Nama = "Steve Rogers",             NoPekerja = "5", Jabatan = "Junior Auditor I IA Sumbagsel",             FungsiDirektorat = "IA Region I I",                         Email = "wddalfalah01@gmail.com",            CostCenter = "523456", IsAktif = true },
            new Pegawai { Nama = "Matt Murdock",             NoPekerja = "6", Jabatan = "Jr Analyst II Environment",                 FungsiDirektorat = "HSSE Sumbagsel",                        Email = "kuruel03@gmail.com",                CostCenter = "623456", IsAktif = true },
            new Pegawai { Nama = "Luthfi Alif Pramudya",    NoPekerja = "7", Jabatan = "Junior Buyer II",                           FungsiDirektorat = "Procurement Sumbagsel",                 Email = "luthfialifp@gmail.com",             CostCenter = "723456", IsAktif = true },
            new Pegawai { Nama = "Peter Parker",             NoPekerja = "8", Jabatan = "Inspector II Reliability",                  FungsiDirektorat = "Rel. & Project Dev. Sumbagsel",         Email = "modernwar.ghost2@gmail.com",        CostCenter = "823456", IsAktif = true },
            new Pegawai { Nama = "Frank Castle",             NoPekerja = "9", Jabatan = "Officer I Maintenance Planning Area III",   FungsiDirektorat = "Rel. & Project Dev. Sumbagsel",         Email = "luthfialifpramudya170@gmail.com",   CostCenter = "923456", IsAktif = true }
        );
        await db.SaveChangesAsync();
    }

    // ── 3. Seed Users (Admin IT, Admin Gudang, 3 Approver) ───────────────
    async Task CreateUserIfMissing(string email, string name, string role, int? pegawaiId = null)
    {
        var existing = await userManager.FindByEmailAsync(email);
        if (existing is not null) return;

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            Nama = name,
            Role = role,
            MustChangePw = false,
            PegawaiId = pegawaiId
        };

        var result = await userManager.CreateAsync(user, "Admin1234!");
        if (result.Succeeded)
            await userManager.AddToRoleAsync(user, role);
    }

    // Cari pegawai berdasarkan NoPekerja untuk dikaitkan ke Users
    var pegawaiList = db.Pegawai.ToList();
    int? getPegawaiId(string noPekerja) =>
        pegawaiList.FirstOrDefault(p => p.NoPekerja == noPekerja)?.Id;

    // Admin IT — Romi Aprilian (no pekerja 1)
    await CreateUserIfMissing("romiaprilian7406@gmail.com", "Romi Aprilian Mustafa", "AdminIT", getPegawaiId("1"));

    // Admin Gudang & Barang — Muhammad Widad Alfalah (no pekerja 4)
    await CreateUserIfMissing("widadalfalah03@gmail.com", "Muhammad Widad Alfalah", "AdminGudangBarang", getPegawaiId("4"));

    // Approver 1 — Bruce Wayne (no pekerja 2)
    await CreateUserIfMissing("ggyur21887@gmail.com", "Bruce Wayne", "Approver", getPegawaiId("2"));

    // Approver 2 — Tony Stark (no pekerja 3)
    await CreateUserIfMissing("guidotorvalds1985@gmail.com", "Tony Stark", "Approver", getPegawaiId("3"));

    // Approver 3 — Steve Rogers (no pekerja 5)
    await CreateUserIfMissing("wddalfalah01@gmail.com", "Steve Rogers", "Approver", getPegawaiId("5"));

    // Sync Jabatan dari Pegawai ke ApplicationUser (untuk user yang Jabatan-nya masih null)
    var usersNeedJabatan = db.Users.Include(u => u.Pegawai).Where(u => u.Jabatan == null && u.PegawaiId != null).ToList();
    foreach (var u in usersNeedJabatan)
        u.Jabatan = u.Pegawai?.Jabatan;
    if (usersNeedJabatan.Any())
        await db.SaveChangesAsync();

    // ── 4. Seed MasterBarang (54 item dari data Excel dummy, kode brg-1 s/d brg-54) ──
    if (!db.MasterBarang.Any())
    {
        var barangList = new[]
        {
            "Anti Static Wrist Strap", "Bridge",         "Cable Tester",   "CCTV",            "Coaxial",
            "Crimping Tool",           "DisplayPort",     "DVI",            "External HDD",    "External SSD",
            "Fiber Cleaver",           "Fiber Optic",     "Flashdisk",      "Fusion Splicer",  "Handphone",
            "HDMI",                    "HDMI Adapter",    "Headset",        "Hot Air Station", "Hub",
            "Keyboard",                "Label Printer",   "LAN Tester",     "Laptop",          "Lightning",
            "Modem",                   "Monitor",         "Mouse",          "Multimeter",      "Obeng Presisi",
            "OTDR",                    "Perangkat Keras", "Power Cable",    "Printer",         "Projector",
            "Punch Down Tool",         "Repeater",        "RFID Reader",    "Router",          "SATA Cable",
            "Scanner",                 "Smart Card Reader","Solder",        "Speaker",         "Switch",
            "Tablet",                  "Thermal Camera",  "Thunderbolt",    "Tone Generator",  "USB Hub",
            "USB-A",                   "USB-C",           "VGA",            "Webcam"
        };

        for (int i = 0; i < barangList.Length; i++)
        {
            db.MasterBarang.Add(new MasterBarang
            {
                KodeBarang = $"brg-{i + 1}",
                NamaBarang = barangList[i],
                IsAktif = true
            });
        }
        await db.SaveChangesAsync();
    }
}

builder.Services.ConfigureApplicationCookie(opt =>
{
    opt.LoginPath = "/login";
    opt.AccessDeniedPath = "/akses-ditolak";
    opt.ExpireTimeSpan = TimeSpan.FromDays(14); // Diperpanjang agar Remember Me bertahan lama
    opt.SlidingExpiration = true;
});

builder.Services.AddAuthorization();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAntiforgery();
app.UseAuthentication();
app.UseAuthorization();

app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = [new HangfireAdminAuthFilter()]
});

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// ── Auth Endpoints (POST must be used for cookie auth from Blazor Server) ──
app.MapPost("/account/login", async (
    HttpContext ctx,
    SignInManager<ApplicationUser> signInManager,
    UserManager<ApplicationUser> userManager) =>
{
    var form = await ctx.Request.ReadFormAsync();
    var email = form["email"].ToString();
    var password = form["password"].ToString();
    var rememberMe = form["rememberMe"].ToString() == "on";
    var returnUrl = form["returnUrl"].ToString();
    if (string.IsNullOrEmpty(returnUrl)) returnUrl = "/dashboard";

    var user = await userManager.FindByEmailAsync(email);
    if (user is null || user.IsDeleted)
        return Results.Redirect($"/login?error=invalid");

    var result = await signInManager.PasswordSignInAsync(user, password, rememberMe, lockoutOnFailure: false);
    if (result.Succeeded)
    {
        if (user.MustChangePw && user.Role != "AdminIT")
        {
            return Results.Redirect("/setup-akun");
        }
        return Results.Redirect(returnUrl);
    }

    return Results.Redirect("/login?error=invalid");
});

app.MapPost("/account/logout", async (
    HttpContext ctx,
    SignInManager<ApplicationUser> signInManager) =>
{
    await signInManager.SignOutAsync();
    return Results.Redirect("/login");
});

// ── ONLYOFFICE Callback Endpoint ──
app.MapPost("/api/onlyoffice/callback/{baId:int}", async (
    int baId,
    HttpContext ctx,
    IConfiguration cfg,
    SistemBeritaAcara.Infrastructure.Data.AppDbContext db,
    SistemBeritaAcara.Core.Interfaces.IDocumentService documentService) =>
{
    try
    {
        var payload = await ctx.Request.ReadFromJsonAsync<System.Text.Json.JsonElement>();

        // Status 2 = closed/saved (regular)
        // Status 6 = auto force-save (dari config forcesave:true / Ctrl+S)
        // Status 7 = force-save dipicu oleh docEditor.forceSave() API call — HARUS ditangani!
        if (payload.TryGetProperty("status", out var statusProp) && (statusProp.GetInt32() == 2 || statusProp.GetInt32() == 6 || statusProp.GetInt32() == 7))
        {
            if (payload.TryGetProperty("url", out var downloadUrlProp))
            {
                var downloadUrl = downloadUrlProp.GetString();
                if (!string.IsNullOrEmpty(downloadUrl))
                {
                    // ONLYOFFICE sends a URL from its own server (Docker-internal, port 80).
                    // Rewrite the host to the mapped port on the Windows host so we can download it.
                    var onlyOfficeServerUrl = (cfg["OnlyOffice:ServerUrl"] ?? "http://localhost:8081").TrimEnd('/');
                    try
                    {
                        var dlUri = new Uri(downloadUrl);
                        var targetBase = new Uri(onlyOfficeServerUrl);
                        // Rewrite scheme+host+port to the configured ONLYOFFICE server (keeps path & query)
                        var builder = new UriBuilder(targetBase);
                        builder.Path = dlUri.AbsolutePath;
                        builder.Query = dlUri.Query.TrimStart('?');
                        downloadUrl = builder.Uri.ToString();
                    }
                    catch { /* keep original URL if rewrite fails */ }

                    var ba = await db.BeritaAcara.FindAsync(baId);
                    if (ba != null && !string.IsNullOrEmpty(ba.DocxPath))
                    {
                        var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", ba.DocxPath.TrimStart('/').Replace("/", Path.DirectorySeparatorChar.ToString()));
                        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);

                        using var httpClient = new System.Net.Http.HttpClient();
                        httpClient.Timeout = TimeSpan.FromSeconds(30);
                        var response = await httpClient.GetAsync(downloadUrl);
                        if (response.IsSuccessStatusCode)
                        {
                            // Tulis ke temp dulu, lalu atomic move — menghindari partial-write
                            // dan konflik lock dengan proses lain (Spire.Doc, Defender, dsb.)
                            var tempFilePath = filePath + ".cb_tmp";
                            await using (var fs = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write, FileShare.None))
                            {
                                await response.Content.CopyToAsync(fs);
                            }

                            // Move dengan retry untuk menangani sisa lock sementara
                            for (int attempt = 0; attempt < 10; attempt++)
                            {
                                try
                                {
                                    File.Move(tempFilePath, filePath, overwrite: true);
                                    break;
                                }
                                catch (IOException) when (attempt < 9)
                                {
                                    await Task.Delay(300);
                                }
                            }

                            // Regenerate PDF dari DOCX yang sudah diedit
                            await documentService.ConvertDocxToPdfAsync(filePath);
                        }
                        else
                        {
                            return Results.Ok(new { error = 1, message = $"Download failed: {response.StatusCode} from {downloadUrl}" });
                        }
                    }
                }
            }
        }
        return Results.Ok(new { error = 0 });
    }
    catch (Exception ex)
    {
        return Results.Ok(new { error = 1, message = ex.Message });
    }
}).DisableAntiforgery();

// PDF Preview endpoint — no-cache agar browser selalu fetch dari disk, bukan cache
app.MapGet("/api/preview/pdf/{baId:int}", async (
    int baId,
    HttpContext ctx,
    SistemBeritaAcara.Infrastructure.Data.AppDbContext db) =>
{
    ctx.Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
    ctx.Response.Headers["Pragma"] = "no-cache";
    ctx.Response.Headers["Expires"] = "0";

    var ba = await db.BeritaAcara.FindAsync(baId);
    if (ba?.DocxPath == null) return Results.NotFound();

    var pdfPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot",
        ba.DocxPath.TrimStart('/').Replace("/", Path.DirectorySeparatorChar.ToString()).Replace(".docx", ".pdf"));

    if (!File.Exists(pdfPath)) return Results.NotFound();

    var bytes = await File.ReadAllBytesAsync(pdfPath);
    return Results.File(bytes, "application/pdf", enableRangeProcessing: true);
}).RequireAuthorization().DisableAntiforgery();

// Database and roles are ensured earlier before app start

RecurringJob.AddOrUpdate<DueDateCheckerJob>(
    "cek-jatuh-tempo",
    job => job.CheckDueDatesAsync(),
    Cron.Daily(7));

app.Run();
