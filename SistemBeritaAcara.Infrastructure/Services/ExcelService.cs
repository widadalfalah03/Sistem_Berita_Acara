using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using SistemBeritaAcara.Core.Entities;
using SistemBeritaAcara.Core.Interfaces;
using SistemBeritaAcara.Infrastructure.Data;

namespace SistemBeritaAcara.Infrastructure.Services;

public class ExcelService(AppDbContext db, IDeaktivasiService deaktivasiService) : IExcelService
{
    public async Task<(int added, int updated, int deactivated, List<string> errors)> ImportPegawaiAsync(
        Stream excelStream, int importedBy, string fileName)
    {
        int added = 0, updated = 0, deactivated = 0;
        var errors = new List<string>();

        using var workbook = new XLWorkbook(excelStream);
        var sheet = workbook.Worksheet(1);
        var rows = sheet.RowsUsed().Skip(1).ToList();

        var noPekerjaInExcel = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows)
        {
            string nama = row.Cell(1).GetString().Trim();
            string noPekerja = row.Cell(2).GetString().Trim();
            string jabatan = row.Cell(3).GetString().Trim();
            string fungsi = row.Cell(4).GetString().Trim();
            string email = row.Cell(5).GetString().Trim();
            string costCenter = row.Cell(6).GetString().Trim();

            if (string.IsNullOrEmpty(noPekerja)) continue;
            noPekerjaInExcel.Add(noPekerja);

            var existing = await db.Pegawai.FirstOrDefaultAsync(p => p.NoPekerja == noPekerja);
            if (existing is null)
            {
                db.Pegawai.Add(new Pegawai
                {
                    Nama = nama,
                    NoPekerja = noPekerja,
                    Jabatan = jabatan,
                    FungsiDirektorat = fungsi,
                    Email = email,
                    CostCenter = costCenter,
                    IsAktif = true,
                    LastSync = DateTime.Now
                });
                added++;
            }
            else
            {
                existing.Nama = nama;
                existing.Jabatan = jabatan;
                existing.FungsiDirektorat = fungsi;
                existing.Email = email;
                existing.CostCenter = costCenter;
                existing.IsAktif = true;
                existing.LastSync = DateTime.Now;
                updated++;
            }
        }

        await db.SaveChangesAsync();

        var toDeactivate = await db.Pegawai
            .Where(p => p.IsAktif && !noPekerjaInExcel.Contains(p.NoPekerja))
            .ToListAsync();

        foreach (var p in toDeactivate)
        {
            await deaktivasiService.DeaktivasiPegawaiAsync(p.Id);
            deactivated++;
        }

        db.PegawaiImportLog.Add(new PegawaiImportLog
        {
            ImportedBy = importedBy,
            FileName = fileName,
            AddedCount = added,
            UpdatedCount = updated,
            DeactivatedCount = deactivated
        });
        await db.SaveChangesAsync();

        return (added, updated, deactivated, errors);
    }

    public async Task<(int added, int updated, int deactivated, List<string> errors)> ImportBarangAsync(
        Stream excelStream, int importedBy, string fileName)
    {
        int added = 0, updated = 0, deactivated = 0;
        var errors = new List<string>();

        using var workbook = new XLWorkbook(excelStream);
        var sheet = workbook.Worksheet(1);
        var rows = sheet.RowsUsed().Skip(1).ToList();

        var kodeInExcel = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows)
        {
            string kode = row.Cell(1).GetString().Trim();
            string nama = row.Cell(2).GetString().Trim();

            if (string.IsNullOrEmpty(kode)) continue;
            kodeInExcel.Add(kode);

            var existing = await db.MasterBarang.FirstOrDefaultAsync(b => b.KodeBarang == kode);
            if (existing is null)
            {
                db.MasterBarang.Add(new MasterBarang
                {
                    KodeBarang = kode,
                    NamaBarang = nama,
                    IsAktif = true
                });
                added++;
            }
            else
            {
                existing.NamaBarang = nama;
                existing.IsAktif = true;
                updated++;
            }
        }

        await db.SaveChangesAsync();

        var toDeactivate = await db.MasterBarang
            .Where(b => b.IsAktif && !kodeInExcel.Contains(b.KodeBarang))
            .ToListAsync();

        foreach (var b in toDeactivate)
        {
            b.IsAktif = false;
            deactivated++;
        }

        db.BarangImportLog.Add(new BarangImportLog
        {
            ImportedBy = importedBy,
            FileName = fileName,
            AddedCount = added,
            UpdatedCount = updated,
            DeactivatedCount = deactivated
        });
        await db.SaveChangesAsync();

        return (added, updated, deactivated, errors);
    }

    public async Task AppendBeritaAcaraToArsipAsync(BeritaAcara ba)
    {
        var dataRoot = Path.Combine(Directory.GetCurrentDirectory(), "data");
        Directory.CreateDirectory(dataRoot);

        var archivePath = Path.Combine(dataRoot, "ArsipBeritaAcara.xlsx");
        using var workbook = File.Exists(archivePath)
            ? new XLWorkbook(archivePath)
            : new XLWorkbook();

        var worksheet = workbook.Worksheets.FirstOrDefault(ws => ws.Name == "Arsip")
            ?? workbook.AddWorksheet("Arsip");

        if (worksheet.RowsUsed() is null || worksheet.Row(1).Cell(1).GetString() != "Nomor Surat")
        {
            worksheet.Cell(1, 1).Value = "Nomor Surat";
            worksheet.Cell(1, 2).Value = "Tanggal";
            worksheet.Cell(1, 3).Value = "Jenis";
            worksheet.Cell(1, 4).Value = "PJ";
            worksheet.Cell(1, 5).Value = "Yang Menyerahkan";
            worksheet.Cell(1, 6).Value = "Approver";
            worksheet.Cell(1, 7).Value = "Status";
            worksheet.Cell(1, 8).Value = "Tanggal Kembali";
        }

        var nextRow = worksheet.LastRowUsed()?.RowNumber() + 1 ?? 2;
        worksheet.Cell(nextRow, 1).Value = ba.NomorSurat;
        worksheet.Cell(nextRow, 2).Value = ba.Tanggal.ToString("yyyy-MM-dd");
        worksheet.Cell(nextRow, 3).Value = ba.Jenis;
        worksheet.Cell(nextRow, 4).Value = ba.Pj?.Nama ?? string.Empty;
        worksheet.Cell(nextRow, 5).Value = ba.Menyerahkan?.Nama ?? string.Empty;
        worksheet.Cell(nextRow, 6).Value = ba.Mengetahui?.Nama ?? string.Empty;
        worksheet.Cell(nextRow, 7).Value = ba.Status;
        worksheet.Cell(nextRow, 8).Value = ba.TanggalKembali?.ToString("yyyy-MM-dd") ?? string.Empty;

        workbook.SaveAs(archivePath);
    }
}

