USE SistemBeritaAcara;
GO
SET QUOTED_IDENTIFIER ON;

-- 1. Disable foreign keys temporarily
EXEC sp_MSforeachtable 'ALTER TABLE ? NOCHECK CONSTRAINT ALL';

-- 2. Clear application tables
DELETE FROM PegawaiImportLog;
DELETE FROM BarangImportLog;
DELETE FROM ApprovalToken;
DELETE FROM BuktiFoto;
DELETE FROM PerangkatBA;
DELETE FROM Notification;
DELETE FROM BeritaAcara;
DELETE FROM MasterBarang;
DELETE FROM BACounter;

-- Insert back initial counter for BACounter
SET IDENTITY_INSERT BACounter ON;
INSERT INTO BACounter (Id, NextValue) VALUES (1, 1);
SET IDENTITY_INSERT BACounter OFF;

-- 3. Clear users and related tables EXCEPT Admin IT
DELETE FROM AspNetUserRoles WHERE UserId NOT IN (SELECT Id FROM Users WHERE Role = 'AdminIT');
DELETE FROM AspNetUserLogins WHERE UserId NOT IN (SELECT Id FROM Users WHERE Role = 'AdminIT');
DELETE FROM AspNetUserClaims WHERE UserId NOT IN (SELECT Id FROM Users WHERE Role = 'AdminIT');
DELETE FROM AspNetUserTokens WHERE UserId NOT IN (SELECT Id FROM Users WHERE Role = 'AdminIT');
DELETE FROM Users WHERE Role != 'AdminIT' OR Role IS NULL;

-- 4. Clear Pegawai EXCEPT the one linked to Admin IT
DELETE FROM Pegawai WHERE Id NOT IN (SELECT PegawaiId FROM Users WHERE Role = 'AdminIT' AND PegawaiId IS NOT NULL);

-- 5. Enable foreign keys
EXEC sp_MSforeachtable 'ALTER TABLE ? WITH CHECK CHECK CONSTRAINT ALL';
GO
