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

        using var ms = new MemoryStream();
        await excelStream.CopyToAsync(ms);
        ms.Position = 0;

        using var workbook = new XLWorkbook(ms);
        var sheet = workbook.Worksheet(1);
        var rows = sheet.RowsUsed().Skip(1).ToList();

        var noPekerjaInExcel = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows)
        {
            string nama = row.Cell(1).Value.ToString().Trim();
            string noPekerja = row.Cell(2).Value.ToString().Trim();
            string jabatan = row.Cell(3).Value.ToString().Trim();
            string fungsi = row.Cell(4).Value.ToString().Trim();
            string email = row.Cell(5).Value.ToString().Trim();
            string costCenter = row.Cell(6).Value.ToString().Trim();

            if (string.IsNullOrEmpty(noPekerja)) continue;
            if (!noPekerjaInExcel.Add(noPekerja)) continue; // Skip duplicates within the file

            if (nama.Length > 100) nama = nama.Substring(0, 100);
            if (noPekerja.Length > 20) noPekerja = noPekerja.Substring(0, 20);
            if (jabatan.Length > 100) jabatan = jabatan.Substring(0, 100);
            if (fungsi.Length > 100) fungsi = fungsi.Substring(0, 100);
            if (email.Length > 150) email = email.Substring(0, 150);
            if (costCenter.Length > 50) costCenter = costCenter.Substring(0, 50);

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

                var linkedUsers = await db.Users.Where(u => u.PegawaiId == existing.Id && u.IsDeleted).ToListAsync();
                foreach (var user in linkedUsers)
                {
                    user.IsDeleted = false;
                    user.DeletedAt = null;
                }
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

        using var ms = new MemoryStream();
        await excelStream.CopyToAsync(ms);
        ms.Position = 0;

        using var workbook = new XLWorkbook(ms);
        var sheet = workbook.Worksheet(1);
        var rows = sheet.RowsUsed().Skip(1).ToList();

        var namaInExcel = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows)
        {
            // Hanya menggunakan Kolom 1 sebagai Nama Barang
            string nama = row.Cell(1).Value.ToString().Trim();

            if (string.IsNullOrEmpty(nama)) continue;

            if (nama.Length > 100) nama = nama.Substring(0, 100);

            if (!namaInExcel.Add(nama)) continue; // Skip duplikat di dalam file yang sama

            MasterBarang? existing = await db.MasterBarang.FirstOrDefaultAsync(b => b.NamaBarang == nama);

            if (existing is null)
            {
                db.MasterBarang.Add(new MasterBarang
                {
                    NamaBarang = nama,
                    IsAktif = true
                });
                added++;
            }
            else
            {
                if (!existing.IsAktif)
                {
                    existing.IsAktif = true;
                    updated++;
                }
            }
        }

        await db.SaveChangesAsync();

        var toDeactivate = await db.MasterBarang
            .Where(b => b.IsAktif && !namaInExcel.Contains(b.NamaBarang))
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

    public Task<byte[]> ExportArsipAsync(IEnumerable<BeritaAcara> data, string baseUrl)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Arsip BA");

        string[] headers = ["Tanggal", "Nomor Surat", "Jenis Berita Acara", "Dokumen"];

        for (int i = 0; i < headers.Length; i++)
        {
            var cell = sheet.Cell(1, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1E3A5F");
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        int row = 2;
        foreach (var ba in data)
        {
            sheet.Cell(row, 1).Value = ba.Tanggal.ToString("dd/MM/yyyy");
            sheet.Cell(row, 2).Value = ba.NomorSurat ?? string.Empty;
            sheet.Cell(row, 3).Value = ba.Jenis;
            
            string docPath = !string.IsNullOrEmpty(ba.DocxFinalPath) ? ba.DocxFinalPath : (!string.IsNullOrEmpty(ba.DocxPath) ? ba.DocxPath : "");
            
            var docCell = sheet.Cell(row, 4);
            if (!string.IsNullOrEmpty(docPath))
            {
                // PDF path is usually the docx path with .pdf extension, assuming they are converted
                string pdfPath = docPath.Replace(".docx", ".pdf");
                string pdfName = Path.GetFileName(pdfPath);
                string fileUrl = $"{baseUrl.TrimEnd('/')}/{pdfPath.TrimStart('/')}";
                
                docCell.Value = pdfName;
                docCell.SetHyperlink(new XLHyperlink(fileUrl));
                docCell.Style.Font.FontColor = XLColor.Blue;
                docCell.Style.Font.Underline = XLFontUnderlineValues.Single;
            }
            else
            {
                docCell.Value = "-";
            }

            if (row % 2 == 0)
                sheet.Row(row).Style.Fill.BackgroundColor = XLColor.FromHtml("#F8FAFC");

            row++;
        }

        sheet.Columns().AdjustToContents();

        var headerRange = sheet.Range(1, 1, 1, headers.Length);
        headerRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        headerRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return Task.FromResult(ms.ToArray());
    }
}

