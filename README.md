# Sistem Berita Acara Digital

Sistem pengelolaan **Berita Acara (BA)** berbasis web untuk PT Pertamina Patra Niaga SSC ICT. Menggantikan proses manual dengan alur digital: pembuatan → tanda tangan → review → arsip.

---

## Daftar Isi

- [Teknologi](#teknologi)
- [Arsitektur](#arsitektur)
- [Prasyarat](#prasyarat)
- [Instalasi & Konfigurasi](#instalasi--konfigurasi)
- [Menjalankan Aplikasi](#menjalankan-aplikasi)
- [Setup Pertama Kali](#setup-pertama-kali)
- [Peran (Roles)](#peran-roles)
- [Fitur Utama](#fitur-utama)
- [Struktur Folder](#struktur-folder)
- [Deploy ke Produksi](#deploy-ke-produksi)
- [Reset Database](#reset-database)

---

## Teknologi

| Komponen | Teknologi |
|---|---|
| Framework | ASP.NET Core 10 — Blazor Server |
| Database | Microsoft SQL Server (Express/Standard) |
| ORM | Entity Framework Core 10 |
| Auth | ASP.NET Core Identity |
| Background Jobs | Hangfire |
| Editor Dokumen | OnlyOffice Document Server |
| Konversi PDF | Spire.Doc (embedded) |
| Email | MailKit (SMTP) |
| Excel | ClosedXML |
| Real-time | SignalR (notifikasi) |

---

## Arsitektur

```
SistemBeritaAcara/
├── SistemBeritaAcara.Core/          # Entitas domain & interface
├── SistemBeritaAcara.Infrastructure/# EF Core, Services, Jobs
└── SistemBeritaAcara.Web/           # Blazor Server, Pages, API
```

**Alur Berita Acara:**

```
Admin GB → Buat BA → Editor (Preview) → Kirim
         ↓
       PJ Sign (tanda tangan penanggung jawab)
         ↓
     Reviewer → Setujui → Nomor Surat → Arsip
              → Tolak   → Admin GB (Revisi)
```

---

## Prasyarat

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- SQL Server 2019+ atau SQL Server Express
- [OnlyOffice Document Server](https://www.onlyoffice.com/download-docs.aspx) (opsional, untuk edit manual)
- SMTP server (Gmail, dll.) untuk notifikasi email

---

## Instalasi & Konfigurasi

### 1. Clone Repository

```bash
git clone <repo-url>
cd Sistem_Berita_Acara
```

### 2. Konfigurasi `appsettings.json`

Edit `SistemBeritaAcara.Web/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost\\SQLEXPRESS;Database=SistemBeritaAcara;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true"
  },
  "Email": {
    "SmtpHost": "smtp.gmail.com",
    "SmtpPort": "587",
    "Username": "email@gmail.com",
    "Password": "app-password-gmail",
    "From": "email@gmail.com"
  },
  "App": {
    "BaseUrl": "http://localhost:5000",
    "PublicUrl": "https://domain-publik.com"
  },
  "OnlyOffice": {
    "ServerUrl": "http://localhost:8081",
    "CallbackSecret": ""
  },
  "NomorSurat": {
    "FormatUmum":  "BA {counter}/PPNEG1000/{year}-S0",
    "FormatLainnya": "BA {counter}/PPNEG1000/{year}-S8"
  }
}
```

> **Gmail App Password:** aktifkan 2FA di akun Google, lalu buat App Password di `myaccount.google.com/apppasswords`.

### 3. Konfigurasi OnlyOffice (opsional)

Salin file `local.json` ke direktori konfigurasi OnlyOffice Document Server agar server mengizinkan callback dari IP lokal:

```json
{
  "services": {
    "CoAuthoring": {
      "request-filtering-agent": {
        "allowPrivateIPAddress": true,
        "allowMetaIPAddress": true
      }
    }
  }
}
```

### 4. Template Dokumen

Pastikan file template `.docx` ada di `SistemBeritaAcara.Web/wwwroot/files/templates/`:

```
Template_BA_ALOKASI.docx
Template_BA_PEMINJAMAN.docx
Template_BA_PENARIKAN.docx
Template_BA_LAINNYA.docx
```

Template menggunakan placeholder `{{NamaPJ}}`, `{{TujuanBA}}`, `{{Tanggal}}`, dll.

---

## Menjalankan Aplikasi

```bash
cd SistemBeritaAcara.Web
dotnet run
```

Akses di browser: `http://localhost:5000`

Database dibuat otomatis oleh `EnsureCreated()` saat pertama kali dijalankan.

---

## Setup Pertama Kali

1. Buka `http://localhost:5000` — akan diarahkan ke `/setup`
2. Isi form untuk membuat akun **Admin IT** pertama
3. Login dengan akun tersebut
4. Tambah pengguna lain via menu **Daftar Pengguna**
5. Import data Pegawai dan MasterBarang via menu masing-masing

---

## Peran (Roles)

| Role | Deskripsi |
|---|---|
| `AdminIT` | Kelola pengguna, import data master |
| `AdminGudangBarang` | Buat dan kelola Berita Acara |
| `Reviewer` | Review dan setujui/tolak Berita Acara |

---

## Fitur Utama

### Berita Acara
- Jenis: **Alokasi**, **Peminjaman**, **Penarikan**, **Lainnya**
- Generate dokumen `.docx` dari template otomatis
- Preview PDF langsung di browser
- Edit manual via OnlyOffice (opsional)
- Sistem tanda tangan digital (gambar PNG)
- Penomoran surat otomatis dengan format konfigurabel

### Alur Persetujuan
- Notifikasi email & real-time (SignalR) di setiap tahap
- Penanggung Jawab (PJ) tanda tangan via link email
- Reviewer setujui/tolak dengan alasan
- Riwayat penolakan tersimpan permanen (`BeritaAcaraHistory`)
- Auto-approve via Hangfire job (jika dikonfigurasi)

### Master Data
- Import Pegawai dari Excel (`.xlsx`)
- Import Barang dari Excel (`.xlsx`)
- Sinkronisasi data dari sistem ERP

### Arsip
- Arsip semua BA yang sudah disetujui
- Export rekap ke Excel
- Filter dan pencarian

---

## Struktur Folder

```
SistemBeritaAcara.Web/
├── Components/
│   ├── Layout/         # MainLayout, EmptyLayout
│   ├── Pages/          # Semua halaman Blazor
│   └── Shared/         # Komponen reusable (Pagination, dll.)
├── wwwroot/
│   ├── app.css         # Stylesheet utama
│   ├── js/             # JavaScript (loading overlay, pdf.js, dll.)
│   ├── files/
│   │   ├── templates/  # Template .docx (di-commit ke Git)
│   │   ├── documents/  # Dokumen BA generated (tidak di-commit)
│   │   └── signatures/ # File TTD pengguna (tidak di-commit)
│   └── images/         # Logo, aset statis
└── appsettings.json
```

---

## Deploy ke Produksi

### Publish

```bash
dotnet publish SistemBeritaAcara.Web -c Release -o ./publish
```

### IIS / Windows Server

1. Install .NET 10 Hosting Bundle
2. Buat site baru di IIS, arahkan ke folder `publish/`
3. Set Application Pool: `.NET CLR version = No Managed Code`
4. Pastikan identity pool punya akses tulis ke `wwwroot/files/`
5. Set environment variable `ASPNETCORE_ENVIRONMENT=Production`
6. Buat `appsettings.Production.json` dengan konfigurasi production

### Reverse Proxy (Nginx)

```nginx
location / {
    proxy_pass         http://localhost:5000;
    proxy_http_version 1.1;
    proxy_set_header   Upgrade $http_upgrade;
    proxy_set_header   Connection keep-alive;
    proxy_set_header   Host $host;
    proxy_cache_bypass $http_upgrade;
}
```

> SignalR memerlukan WebSocket — pastikan `proxy_set_header Upgrade` dikonfigurasi.

### Checklist Deploy

- [ ] Connection string production di `appsettings.Production.json`
- [ ] SMTP email dikonfigurasi
- [ ] `App:PublicUrl` diisi dengan domain publik (digunakan untuk link email PJ Sign)
- [ ] OnlyOffice Document Server jalan (jika dipakai)
- [ ] Folder `wwwroot/files/documents/` dan `wwwroot/files/signatures/` bisa ditulis
- [ ] Template `.docx` ada di `wwwroot/files/templates/`
- [ ] Jalankan `reset_database.sql` jika butuh clean start

---

## Reset Database

Untuk reset data sebelum go-live atau saat testing:

```bash
# Di SQL Server Management Studio atau sqlcmd:
sqlcmd -S localhost\SQLEXPRESS -d SistemBeritaAcara -i reset_database.sql
```

Script `reset_database.sql` menghapus:
- Semua Berita Acara dan data turunannya
- Semua pengguna (Users, Roles)
- Notifikasi, token, riwayat
- Counter nomor surat (reset ke 1)
- Data Pegawai & MasterBarang **tidak** dihapus secara default (lihat MODE A di dalam script)

Setelah reset, jalankan aplikasi dan buka `/setup` untuk membuat Admin IT baru.

---

## Lisensi

Internal — PT Pertamina Patra Niaga SSC ICT
