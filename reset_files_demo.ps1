# ============================================================
# SCRIPT RESET FILE UPLOAD UNTUK DEMO
# Sistem Berita Acara - Jalankan di PowerShell
# ============================================================
# Pastikan Anda sudah menjalankan reset_demo.sql di SSMS dulu!
# ============================================================

$WebRoot = "c:\Users\USER\Sistem_Berita_Acara\SistemBeritaAcara.Web\wwwroot\files"

Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "  RESET FILE UPLOAD UNTUK DEMO" -ForegroundColor Cyan
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host ""

# Konfirmasi sebelum hapus
$confirm = Read-Host "Apakah Anda yakin ingin menghapus semua file upload? (y/n)"
if ($confirm -ne "y") {
    Write-Host "Dibatalkan." -ForegroundColor Yellow
    exit
}

# Folder yang akan dibersihkan
$folders = @(
    "$WebRoot\documents",
    "$WebRoot\photos",
    "$WebRoot\signatures",
    "$WebRoot\profiles"
)

foreach ($folder in $folders) {
    if (Test-Path $folder) {
        $files = Get-ChildItem -Path $folder -File
        $count = $files.Count
        
        if ($count -gt 0) {
            Remove-Item -Path "$folder\*" -Force -Recurse
            Write-Host "  [OK] Dihapus $count file dari: $folder" -ForegroundColor Green
        } else {
            Write-Host "  [--] Sudah kosong: $folder" -ForegroundColor Gray
        }
    } else {
        # Buat folder jika belum ada
        New-Item -ItemType Directory -Path $folder -Force | Out-Null
        Write-Host "  [+]  Dibuat folder: $folder" -ForegroundColor Yellow
    }
}

# Pastikan folder templates TIDAK dihapus
$templatesFolder = "$WebRoot\templates"
if (Test-Path $templatesFolder) {
    $templateCount = (Get-ChildItem -Path $templatesFolder -File).Count
    Write-Host ""
    Write-Host "  [i]  Templates TIDAK dihapus ($templateCount file aman): $templatesFolder" -ForegroundColor DarkCyan
}

Write-Host ""
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "  SELESAI! File upload berhasil direset." -ForegroundColor Green
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Langkah selanjutnya:" -ForegroundColor Yellow
Write-Host "  1. Jalankan: dotnet run --project SistemBeritaAcara.Web" -ForegroundColor White
Write-Host "  2. Login ke aplikasi sebagai Admin IT" -ForegroundColor White
Write-Host "  3. Import Excel Pegawai dan Barang" -ForegroundColor White
Write-Host "  4. Buat akun user baru" -ForegroundColor White
Write-Host "  5. Buat Berita Acara pertama!" -ForegroundColor White
Write-Host ""
