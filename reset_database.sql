USE [SistemBeritaAcara];
GO

--  BAGIAN 1: MATIKAN FOREIGN KEY SEMENTARA
EXEC sp_MSforeachtable 'ALTER TABLE ? NOCHECK CONSTRAINT ALL';
GO

--  BAGIAN 2: HAPUS DATA TRANSAKSI (SELALU DIJALANKAN)

-- File-file generated dokumen dan tanda tangan di-handle manual
-- (hapus isi folder wwwroot/files/documents/ dan wwwroot/files/signatures/)

-- Tabel Hangfire (background jobs)
IF OBJECT_ID('HangFire.AggregatedCounter', 'U') IS NOT NULL DELETE FROM [HangFire].[AggregatedCounter];
IF OBJECT_ID('HangFire.Counter', 'U')           IS NOT NULL DELETE FROM [HangFire].[Counter];
IF OBJECT_ID('HangFire.Hash', 'U')              IS NOT NULL DELETE FROM [HangFire].[Hash];
IF OBJECT_ID('HangFire.JobParameter', 'U')      IS NOT NULL DELETE FROM [HangFire].[JobParameter];
IF OBJECT_ID('HangFire.JobQueue', 'U')          IS NOT NULL DELETE FROM [HangFire].[JobQueue];
IF OBJECT_ID('HangFire.List', 'U')              IS NOT NULL DELETE FROM [HangFire].[List];
IF OBJECT_ID('HangFire.Set', 'U')               IS NOT NULL DELETE FROM [HangFire].[Set];
IF OBJECT_ID('HangFire.State', 'U')             IS NOT NULL DELETE FROM [HangFire].[State];
IF OBJECT_ID('HangFire.Job', 'U')               IS NOT NULL DELETE FROM [HangFire].[Job];
IF OBJECT_ID('HangFire.Server', 'U')            IS NOT NULL DELETE FROM [HangFire].[Server];
GO

-- Notifikasi
DELETE FROM [Notification];
GO

-- Token persetujuan
DELETE FROM [ApprovalToken];
GO

-- Riwayat reject
DELETE FROM [BeritaAcaraHistory];
GO

-- Foto bukti
DELETE FROM [BuktiFoto];
GO

-- Perangkat dalam BA
DELETE FROM [PerangkatBA];
GO

-- Perpanjangan BA
DELETE FROM [PerpanjanganBA];
GO

-- Berita Acara utama
DELETE FROM [BeritaAcara];
DBCC CHECKIDENT('[BeritaAcara]', RESEED, 0);
GO

-- Reset counter nomor surat
UPDATE [BACounter] SET [NextValue] = 1 WHERE [Id] = 1;
IF @@ROWCOUNT = 0
    INSERT INTO [BACounter] ([Id], [NextValue]) VALUES (1, 1);
GO

-- Log impor
DELETE FROM [PegawaiImportLog];
DELETE FROM [BarangImportLog];
GO

--  BAGIAN 3: HAPUS DATA PENGGUNA (USER & ROLES)
--  Setelah ini: aplikasi restart → halaman /setup muncul →
--  akun pertama yang dibuat menjadi Admin IT (hanya 1 akun).

-- Identity tables (termasuk Admin IT)
DELETE FROM [AspNetUserTokens];
DELETE FROM [AspNetUserLogins];
DELETE FROM [AspNetUserClaims];
DELETE FROM [AspNetUserRoles];
DELETE FROM [Users];
DBCC CHECKIDENT('[Users]', RESEED, 0);
GO

DELETE FROM [AspNetRoleClaims];
DELETE FROM [AspNetRoles];
GO

--  BAGIAN 4 (OPSIONAL) — MODE A: HAPUS MASTER DATA

-- Hapus data Pegawai
DELETE FROM [Pegawai];
DBCC CHECKIDENT('[Pegawai]', RESEED, 0);
GO

-- Hapus data MasterBarang
DELETE FROM [MasterBarang];
DBCC CHECKIDENT('[MasterBarang]', RESEED, 0);
GO

--  BAGIAN 5: AKTIFKAN KEMBALI FOREIGN KEY
EXEC sp_MSforeachtable 'ALTER TABLE ? WITH CHECK CHECK CONSTRAINT ALL';
GO

--  VERIFIKASI
SELECT 'BeritaAcara'      AS [Tabel], COUNT(*) AS [Jumlah] FROM [BeritaAcara]
UNION ALL
SELECT 'PerpanjanganBA',   COUNT(*)                          FROM [PerpanjanganBA]
UNION ALL
SELECT 'BACounter',        [NextValue]                       FROM [BACounter]
UNION ALL
SELECT 'Users',            COUNT(*)                          FROM [Users]
UNION ALL
SELECT 'AspNetRoles',      COUNT(*)                          FROM [AspNetRoles]
UNION ALL
SELECT 'Pegawai',          COUNT(*)                          FROM [Pegawai]
UNION ALL
SELECT 'MasterBarang',     COUNT(*)                          FROM [MasterBarang]
UNION ALL
SELECT 'Notification',     COUNT(*)                          FROM [Notification];
GO
