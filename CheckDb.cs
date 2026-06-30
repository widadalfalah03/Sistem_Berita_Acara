using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using SistemBeritaAcara.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;
class Program {
    static void Main() {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(""Server=localhost\\SQLEXPRESS;Database=SistemBeritaAcara;Trusted_Connection=True;TrustServerCertificate=True;"")
            .Options;
        using var db = new AppDbContext(options);
        var p = db.Pegawai.FirstOrDefault(x => x.Id == 0);
        Console.WriteLine(""Pegawai 0:"" + (p != null));
    }
}
