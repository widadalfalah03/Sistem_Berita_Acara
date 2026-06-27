-- ============================================================
-- SCRIPT RESET DATABASE UNTUK DEMO / PRESENTASI
-- Sistem Berita Acara - Pertamina Sumbagsel
-- ============================================================
-- PERINGATAN: Jalankan script ini HANYA sebelum demo!
-- Script ini menghapus SEMUA data kecuali role Identity.
-- Setelah reset, jalankan ulang aplikasi agar seeder berjalan.
-- ============================================================

USE SistemBeritaAcara;
GO

SET QUOTED_IDENTIFIER ON;
GO

-- ============================================================
-- LANGKAH 1: Nonaktifkan sementara foreign key constraint
-- ============================================================
EXEC sp_MSforeachtable 'ALTER TABLE ? NOCHECK CONSTRAINT ALL';
GO

-- ============================================================
-- LANGKAH 2: Hapus data transaksi BA dan perangkat
-- ============================================================
DELETE FROM PegawaiImportLog;
DELETE FROM BarangImportLog;
DELETE FROM ApprovalToken;
DELETE FROM BuktiFoto;
DELETE FROM PerangkatBA;
DELETE FROM Notification;
DELETE FROM BeritaAcara;

-- ============================================================
-- LANGKAH 3: Hapus Master Data (Pegawai & Barang)
-- ============================================================
DELETE FROM MasterBarang;

-- ============================================================
-- LANGKAH 4: Hapus semua Users & Identity tables
-- ============================================================
DELETE FROM AspNetUserRoles;
DELETE FROM AspNetUserLogins;
DELETE FROM AspNetUserClaims;
DELETE FROM AspNetUserTokens;
DELETE FROM Users;

-- ============================================================
-- LANGKAH 5: Hapus Pegawai (setelah Users dihapus)
-- ============================================================
DELETE FROM Pegawai;

-- ============================================================
-- LANGKAH 6: Reset BACounter ke awal (nomor surat mulai dari 1)
-- ============================================================
DELETE FROM BACounter;

-- Reset identity seed jika ada
DBCC CHECKIDENT ('BACounter', RESEED, 0);

-- Insert ulang counter dengan value 1
SET IDENTITY_INSERT BACounter ON;
INSERT INTO BACounter (Id, NextValue) VALUES (1, 1);
SET IDENTITY_INSERT BACounter OFF;

-- ============================================================
-- LANGKAH 7: Reset Identity/Auto-increment semua tabel utama
--            agar Id mulai dari 1 kembali
-- ============================================================
DBCC CHECKIDENT ('Pegawai',          RESEED, 0);
DBCC CHECKIDENT ('Users',            RESEED, 0);
DBCC CHECKIDENT ('BeritaAcara',      RESEED, 0);
DBCC CHECKIDENT ('PerangkatBA',      RESEED, 0);
DBCC CHECKIDENT ('MasterBarang',     RESEED, 0);
DBCC CHECKIDENT ('BuktiFoto',        RESEED, 0);
DBCC CHECKIDENT ('ApprovalToken',    RESEED, 0);
DBCC CHECKIDENT ('Notification',     RESEED, 0);
DBCC CHECKIDENT ('PegawaiImportLog', RESEED, 0);
DBCC CHECKIDENT ('BarangImportLog',  RESEED, 0);

-- ============================================================
-- LANGKAH 8: Aktifkan kembali foreign key constraint
-- ============================================================
EXEC sp_MSforeachtable 'ALTER TABLE ? WITH CHECK CHECK CONSTRAINT ALL';
GO

-- ============================================================
-- VERIFIKASI: Cek semua tabel sudah kosong
-- ============================================================
SELECT 'Pegawai'          AS Tabel, COUNT(*) AS JumlahData FROM Pegawai          UNION ALL
SELECT 'Users'            AS Tabel, COUNT(*) AS JumlahData FROM Users            UNION ALL
SELECT 'MasterBarang'     AS Tabel, COUNT(*) AS JumlahData FROM MasterBarang     UNION ALL
SELECT 'BeritaAcara'      AS Tabel, COUNT(*) AS JumlahData FROM BeritaAcara      UNION ALL
SELECT 'PerangkatBA'      AS Tabel, COUNT(*) AS JumlahData FROM PerangkatBA      UNION ALL
SELECT 'BuktiFoto'        AS Tabel, COUNT(*) AS JumlahData FROM BuktiFoto        UNION ALL
SELECT 'ApprovalToken'    AS Tabel, COUNT(*) AS JumlahData FROM ApprovalToken    UNION ALL
SELECT 'Notification'     AS Tabel, COUNT(*) AS JumlahData FROM Notification     UNION ALL
SELECT 'BACounter'        AS Tabel, COUNT(*) AS JumlahData FROM BACounter        UNION ALL
SELECT 'PegawaiImportLog' AS Tabel, COUNT(*) AS JumlahData FROM PegawaiImportLog UNION ALL
SELECT 'BarangImportLog'  AS Tabel, COUNT(*) AS JumlahData FROM BarangImportLog;

SELECT 'BACounter NextValue' AS Info, NextValue FROM BACounter WHERE Id = 1;
GO

PRINT '============================================================';
PRINT 'RESET SELESAI!';
PRINT 'Langkah selanjutnya:';
PRINT '1. Jalankan ulang aplikasi (dotnet run) agar seeder berjalan';
PRINT '2. Login menggunakan akun Admin IT (dari seeder)';
PRINT '3. Import Excel pegawai melalui menu Manajemen Pegawai';
PRINT '4. Import Excel barang melalui menu Master Barang';
PRINT '5. Buat akun user baru (Admin Gudang / Approver)';
PRINT '6. Buat Berita Acara pertama!';
PRINT '============================================================';
GO
