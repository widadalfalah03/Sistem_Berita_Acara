using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SistemBeritaAcara.Core.Entities;

namespace SistemBeritaAcara.Infrastructure.Data;

public class AppDbContext : IdentityDbContext<ApplicationUser, Microsoft.AspNetCore.Identity.IdentityRole<int>, int>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Pegawai> Pegawai => Set<Pegawai>();
    public DbSet<MasterBarang> MasterBarang => Set<MasterBarang>();
    public DbSet<BeritaAcara> BeritaAcara => Set<BeritaAcara>();
    public DbSet<PerangkatBA> PerangkatBA => Set<PerangkatBA>();
    public DbSet<BuktiFoto> BuktiFoto => Set<BuktiFoto>();
    public DbSet<ApprovalToken> ApprovalToken => Set<ApprovalToken>();
    public DbSet<Notification> Notification => Set<Notification>();
    public DbSet<BeritaAcaraHistory> BeritaAcaraHistory => Set<BeritaAcaraHistory>();
    public DbSet<BACounter> BACounter => Set<BACounter>();
    public DbSet<PegawaiImportLog> PegawaiImportLog => Set<PegawaiImportLog>();
    public DbSet<BarangImportLog> BarangImportLog => Set<BarangImportLog>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>(e =>
        {
            e.ToTable("Users");
            e.Property(u => u.Nama).HasMaxLength(100).IsRequired();
            e.Property(u => u.Jabatan).HasMaxLength(100);
            e.Property(u => u.Role).HasMaxLength(20).IsRequired();
            e.Property(u => u.TtdPath).HasMaxLength(500);
            e.Property(u => u.ProfilePicPath).HasMaxLength(500);
            e.HasOne(u => u.Pegawai)
                .WithMany(p => p.Users)
                .HasForeignKey(u => u.PegawaiId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<Pegawai>(e =>
        {
            e.HasKey(p => p.Id);
            e.Property(p => p.Nama).HasMaxLength(100).IsRequired();
            e.Property(p => p.NoPekerja).HasMaxLength(20).IsRequired();
            e.HasIndex(p => p.NoPekerja).IsUnique();
            e.Property(p => p.Jabatan).HasMaxLength(100);
            e.Property(p => p.CostCenter).HasMaxLength(50);
            e.Property(p => p.FungsiDirektorat).HasMaxLength(100);
            e.Property(p => p.Alamat).HasMaxLength(200);
            e.Property(p => p.Email).HasMaxLength(150);
            e.Property(p => p.NoTelp).HasMaxLength(20);
            e.Property(p => p.TtdPath).HasMaxLength(500);
        });

        builder.Entity<MasterBarang>(e =>
        {
            e.HasKey(b => b.Id);
            e.Property(b => b.NamaBarang).HasMaxLength(100).IsRequired();
        });

        builder.Entity<BeritaAcara>(e =>
        {
            e.HasKey(ba => ba.Id);
            e.Property(ba => ba.NomorSurat).HasMaxLength(50);
            e.HasIndex(ba => ba.NomorSurat).IsUnique().HasFilter("[NomorSurat] IS NOT NULL");
            e.Property(ba => ba.Jenis).HasMaxLength(20).IsRequired();
            e.Property(ba => ba.JenisCustom).HasMaxLength(50);
            e.Property(ba => ba.TiketSscNo).HasMaxLength(50);
            e.Property(ba => ba.DasarAlokasi).HasMaxLength(30).HasDefaultValue("Tiket SSC");
            e.Property(ba => ba.Status).HasMaxLength(30).HasDefaultValue("Draft");
            e.Property(ba => ba.AlasanReject).HasMaxLength(500);
            e.Property(ba => ba.TtdPjPath).HasMaxLength(500);
            e.Property(ba => ba.DocxPath).HasMaxLength(500);
            e.Property(ba => ba.DocxFinalPath).HasMaxLength(500);
            e.Property(ba => ba.CreatedAt).HasDefaultValueSql("GETDATE()");

            e.HasOne(ba => ba.Pj)
                .WithMany(p => p.BeritaAcaraPj)
                .HasForeignKey(ba => ba.PjId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(ba => ba.Menyerahkan)
                .WithMany(p => p.BeritaAcaraMenyerahkan)
                .HasForeignKey(ba => ba.MenyerahkanId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(ba => ba.Mengetahui)
                .WithMany(u => u.BeritaAcaraMengetahui)
                .HasForeignKey(ba => ba.MengetahuiId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(ba => ba.Creator)
                .WithMany(u => u.BeritaAcaraCreated)
                .HasForeignKey(ba => ba.CreatedBy)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<PerangkatBA>(e =>
        {
            e.HasKey(p => p.Id);
            e.Property(p => p.Satuan).HasMaxLength(20).HasDefaultValue("Pcs");
            e.Property(p => p.NoSerial).HasMaxLength(100);
            e.Property(p => p.Keterangan).HasMaxLength(200);
        });

        builder.Entity<BuktiFoto>(e =>
        {
            e.HasKey(f => f.Id);
            e.Property(f => f.FilePath).HasMaxLength(500).IsRequired();
        });

        builder.Entity<ApprovalToken>(e =>
        {
            e.HasKey(t => t.Id);
            e.Property(t => t.Token).HasMaxLength(128).IsRequired();
            e.HasIndex(t => t.Token).IsUnique();
            e.Property(t => t.TokenType).HasMaxLength(20).HasDefaultValue("PJ_SIGNATURE");
        });

        builder.Entity<Notification>(e =>
        {
            e.HasKey(n => n.Id);
            e.Property(n => n.Tipe).HasMaxLength(30).IsRequired();
            e.Property(n => n.Message).HasMaxLength(300).IsRequired();
            e.HasOne(n => n.BeritaAcara)
                .WithMany(ba => ba.Notifications)
                .HasForeignKey(n => n.BaId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<BeritaAcaraHistory>(e =>
        {
            e.HasKey(h => h.Id);
            e.Property(h => h.AlasanReject).HasMaxLength(500).IsRequired();
            e.Property(h => h.BaJenis).HasMaxLength(20).IsRequired();
            e.Property(h => h.BaNomorSurat).HasMaxLength(50);
            e.Property(h => h.RejectedAt).HasDefaultValueSql("GETDATE()");
            e.HasOne(h => h.BeritaAcara)
                .WithMany()
                .HasForeignKey(h => h.BaId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(h => h.RejectedByUser)
                .WithMany()
                .HasForeignKey(h => h.RejectedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<BACounter>(e =>
        {
            e.HasKey(c => c.Id);
            e.HasData(new BACounter { Id = 1, NextValue = 1 });
        });

        builder.Entity<PegawaiImportLog>(e =>
        {
            e.HasKey(l => l.Id);
            e.Property(l => l.FileName).HasMaxLength(200);
            e.HasOne(l => l.ImportedByUser)
                .WithMany(u => u.PegawaiImportLogs)
                .HasForeignKey(l => l.ImportedBy)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<BarangImportLog>(e =>
        {
            e.HasKey(l => l.Id);
            e.Property(l => l.FileName).HasMaxLength(200);
            e.HasOne(l => l.ImportedByUser)
                .WithMany(u => u.BarangImportLogs)
                .HasForeignKey(l => l.ImportedBy)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
