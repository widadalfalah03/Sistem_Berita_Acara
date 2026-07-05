using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SistemBeritaAcara.Core.Entities;
using SistemBeritaAcara.Core.Interfaces;
using SistemBeritaAcara.Infrastructure.Data;

namespace SistemBeritaAcara.Infrastructure.Services;

public class ExcelService(AppDbContext db, IDeaktivasiService deaktivasiService, IConfiguration configuration) : IExcelService
{
    private static readonly SemaphoreSlim _archiveLock = new(1, 1);

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

        // Normalisasi header: lowercase, hanya alfanumerik + spasi
        static string Norm(string s) => new string(s.ToLowerInvariant().Where(c => char.IsLetterOrDigit(c) || c == ' ').ToArray()).Trim();

        // Baca semua header dari baris 1, petakan nama → indeks kolom
        var headerRow = sheet.Row(1);
        var colMap = new Dictionary<string, int>();
        int lastCol = headerRow.LastCellUsed()?.Address.ColumnNumber ?? 0;
        for (int c = 1; c <= lastCol; c++)
        {
            var h = Norm(headerRow.Cell(c).Value.ToString());
            if (!string.IsNullOrEmpty(h) && !colMap.ContainsKey(h))
                colMap[h] = c;
        }
        // "fungsi" dan "fungsi direktorat" keduanya diarahkan ke key yang sama
        if (!colMap.ContainsKey("fungsi direktorat") && colMap.ContainsKey("fungsi"))
            colMap["fungsi direktorat"] = colMap["fungsi"];

        // Validasi kolom wajib: Nama, No. Pekerja, Email
        foreach (var required in new[] { "nama", "no pekerja", "email" })
        {
            if (!colMap.ContainsKey(required))
                return (0, 0, 0, [$"Format file tidak valid. Kolom wajib '{required}' tidak ditemukan. Pastikan file memiliki kolom Nama, No. Pekerja, dan Email."]);
        }

        int colNama        = colMap["nama"];
        int colNoPekerja   = colMap["no pekerja"];
        int colEmail       = colMap["email"];
        int colJabatan     = colMap.GetValueOrDefault("jabatan", 0);
        int colFungsi      = colMap.GetValueOrDefault("fungsi direktorat", 0);
        int colCostCenter  = colMap.GetValueOrDefault("cost center", 0);

        var rows = sheet.RowsUsed().Skip(1).ToList();

        var noPekerjaInExcel = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows)
        {
            string nama      = row.Cell(colNama).Value.ToString().Trim();
            string noPekerja = row.Cell(colNoPekerja).Value.ToString().Trim();
            string email     = row.Cell(colEmail).Value.ToString().Trim();
            string jabatan   = colJabatan > 0 ? row.Cell(colJabatan).Value.ToString().Trim() : string.Empty;
            string fungsi    = colFungsi > 0 ? row.Cell(colFungsi).Value.ToString().Trim() : string.Empty;
            string costCenter = colCostCenter > 0 ? row.Cell(colCostCenter).Value.ToString().Trim() : string.Empty;

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

                var linkedUsers = await db.Users.Where(u => u.PegawaiId == existing.Id).ToListAsync();
                foreach (var user in linkedUsers)
                {
                    user.Nama = nama;
                    user.Jabatan = jabatan;
                    if (user.IsDeleted)
                    {
                        user.IsDeleted = false;
                        user.DeletedAt = null;
                    }
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

        // Validasi kolom wajib Data Barang (normalisasi: lowercase, hanya alfanumerik+spasi)
        static string NormB(string s) => new string(s.ToLowerInvariant().Where(c => char.IsLetterOrDigit(c) || c == ' ').ToArray()).Trim();
        var col1Norm = NormB(sheet.Cell(1, 1).Value.ToString());
        if (col1Norm != "nama barang")
            return (0, 0, 0, [$"Format file tidak valid. Kolom pertama harus 'Nama Barang', ditemukan '{col1Norm}'. Pastikan menggunakan template Data Barang yang benar."]);

        var rows = sheet.RowsUsed().Skip(1).ToList();

        var namaInExcel = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows)
        {
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
        await _archiveLock.WaitAsync();
        try
        {
            var configuredPath = configuration["Storage:ArchivePath"];
            var dataRoot = !string.IsNullOrWhiteSpace(configuredPath)
                ? configuredPath
                : Path.Combine(Directory.GetCurrentDirectory(), "data");
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
                worksheet.Cell(1, 6).Value = "Reviewer";
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
        finally
        {
            _archiveLock.Release();
        }
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

    public Task<byte[]> ExportPegawaiAsync(IEnumerable<Pegawai> data)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Data Pegawai");

        string[] headers = ["Nama", "No. Pekerja", "Jabatan", "Fungsi/Direktorat", "Email", "Cost Center"];
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
        foreach (var p in data)
        {
            sheet.Cell(row, 1).Value = p.Nama;
            sheet.Cell(row, 2).Value = p.NoPekerja;
            sheet.Cell(row, 3).Value = p.Jabatan ?? string.Empty;
            sheet.Cell(row, 4).Value = p.FungsiDirektorat ?? string.Empty;
            sheet.Cell(row, 5).Value = p.Email ?? string.Empty;
            sheet.Cell(row, 6).Value = p.CostCenter ?? string.Empty;
            if (row % 2 == 0)
                sheet.Row(row).Style.Fill.BackgroundColor = XLColor.FromHtml("#F8FAFC");
            row++;
        }

        sheet.Columns().AdjustToContents();
        sheet.Range(1, 1, 1, headers.Length).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        sheet.Range(1, 1, 1, headers.Length).Style.Border.InsideBorder = XLBorderStyleValues.Thin;

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return Task.FromResult(ms.ToArray());
    }

    public Task<byte[]> ExportBarangAsync(IEnumerable<MasterBarang> data)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Data Barang");

        sheet.Cell(1, 1).Value = "Nama Barang";
        sheet.Cell(1, 1).Style.Font.Bold = true;
        sheet.Cell(1, 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#1E3A5F");
        sheet.Cell(1, 1).Style.Font.FontColor = XLColor.White;
        sheet.Cell(1, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        int row = 2;
        foreach (var b in data)
        {
            sheet.Cell(row, 1).Value = b.NamaBarang;
            if (row % 2 == 0)
                sheet.Row(row).Style.Fill.BackgroundColor = XLColor.FromHtml("#F8FAFC");
            row++;
        }

        sheet.Column(1).AdjustToContents();
        sheet.Cell(1, 1).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return Task.FromResult(ms.ToArray());
    }
}

