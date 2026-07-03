# Panduan Deployment

**Sistem Informasi Manajemen Berita Acara (SIMBA)**  
PT Pertamina Patra Niaga Regional Sumbagsel

</div>

---

## Daftar Isi

1. [Gambaran Umum](#1-gambaran-umum)
2. [Prasyarat Server](#2-prasyarat-server)
3. [Instalasi Prasyarat](#3-instalasi-prasyarat)
4. [Persiapan Aplikasi (Publish)](#4-persiapan-aplikasi-publish)
5. [Setup SQL Server](#5-setup-sql-server)
6. [Setup IIS](#6-setup-iis)
7. [Konfigurasi appsettings.Production.json](#7-konfigurasi-appsettingsproductionjson)
8. [Konfigurasi Email @pertamina.com](#8-konfigurasi-email-pertaminacom)
9. [Konfigurasi HTTPS / SSL](#9-konfigurasi-https--ssl)
10. [Setup OnlyOffice (Edit Manual)](#10-setup-onlyoffice-edit-manual)
11. [Setup Gotenberg (Konversi PDF)](#11-setup-gotenberg-konversi-pdf)
12. [Konfigurasi Reverse Proxy (Nginx / IIS ARR)](#12-konfigurasi-reverse-proxy-nginx--iis-arr)
13. [Konfigurasi Firewall & Port](#13-konfigurasi-firewall--port)
14. [Jalankan & Verifikasi Aplikasi](#14-jalankan--verifikasi-aplikasi)
15. [Setup Pertama Kali (First-Run)](#15-setup-pertama-kali-first-run)
16. [Import Data Master](#16-import-data-master)
17. [Perawatan & Monitoring](#17-perawatan--monitoring)
18. [Troubleshooting](#18-troubleshooting)
19. [Checklist Deploy](#19-checklist-deploy)

---

## 1. Gambaran Umum

```
[Client Browser]
       │  HTTPS 443
       ▼
  [IIS / Nginx]  ← reverse proxy
       │  HTTP 5000 (internal)
       ▼
 [Kestrel / .NET 10]
       │
       ├── SQL Server (port 1433)
       ├── SMTP Exchange / Gmail (port 587)
       ├── OnlyOffice (port 8081, opsional)
       └── Gotenberg (port 3000, opsional)
```

Aplikasi berjalan sebagai **ASP.NET Core Blazor Server** di atas Kestrel, dibalik IIS sebagai reverse proxy. Database menggunakan SQL Server yang ada di server yang sama atau server DB terpisah.

Dua layanan eksternal yang opsional namun sangat disarankan:
- **OnlyOffice** — editor DOCX berbasis browser untuk fitur "Edit Manual"
- **Gotenberg** — server konversi dokumen alternatif (PDF fallback jika Spire.Doc gagal)

---

## 2. Prasyarat Server

| Komponen | Versi Minimum | Keterangan |
|---|---|---|
| OS | Windows Server 2019 | 2022 lebih disarankan |
| IIS | 10.0 | Role harus diaktifkan |
| ASP.NET Core Module v2 | — | Bundled dengan Hosting Bundle |
| .NET 10 Hosting Bundle | 10.0.x | Wajib di server |
| SQL Server | 2019 Express/Standard | Express cukup untuk skala SSC ICT |
| RAM | minimal 4 GB | 8 GB disarankan jika OnlyOffice di server yang sama |
| Disk | minimal 20 GB bebas | untuk DB, generated DOCX/PDF, dan signatures |

> **Catatan:** Jika server sudah memiliki SQL Server dari sistem lain (SAP, dll.), cukup buat database baru di instance yang sama.

---

## 3. Instalasi Prasyarat

### 3.1 Aktifkan IIS di Windows Server

Buka **Server Manager** → **Add Roles and Features** → pilih:

```
Web Server (IIS)
  ├── Common HTTP Features
  │     ├── Static Content ✓
  │     ├── Default Document ✓
  │     └── HTTP Errors ✓
  ├── Application Development
  │     ├── ASP.NET 4.x ✓
  │     └── WebSocket Protocol ✓   ← WAJIB untuk SignalR
  └── Security
        └── Basic Authentication ✓
```

Atau via PowerShell (run as Administrator):

```powershell
Install-WindowsFeature -Name Web-Server, Web-Asp-Net45, Web-WebSockets, Web-Default-Doc, Web-Http-Errors, Web-Static-Content -IncludeManagementTools
```

### 3.2 Install .NET 10 Hosting Bundle

Download dari: https://dotnet.microsoft.com/download/dotnet/10.0  
Pilih: **ASP.NET Core Runtime 10.x.x — Windows Hosting Bundle**

Jalankan installer, restart IIS setelah selesai:

```cmd
iisreset /restart
```

Verifikasi:
```cmd
dotnet --version
# Output: 10.x.x
```

### 3.3 Verifikasi ASP.NET Core Module

Buka IIS Manager → klik server → **Modules** → pastikan `AspNetCoreModuleV2` ada di daftar.

---

## 4. Persiapan Aplikasi (Publish)

Lakukan di **komputer developer**, bukan di server.

### 4.1 Publish ke folder

```bash
# Di root project
dotnet publish SistemBeritaAcara.Web -c Release -r win-x64 --self-contained false -o ./publish
```

### 4.2 Isi folder publish yang dihasilkan

```
publish/
├── SistemBeritaAcara.Web.exe
├── SistemBeritaAcara.Web.dll
├── appsettings.json
├── web.config              ← dibuat otomatis oleh dotnet publish
├── wwwroot/
│   ├── app.css
│   ├── js/
│   ├── images/
│   └── files/
│       ├── templates/      ← template .docx (PENTING: harus ada)
│       ├── documents/      ← DOCX & PDF yang di-generate (dibuat otomatis)
│       └── signatures/     ← gambar TTD pengguna (dibuat otomatis)
└── [dll lainnya]
```

> **PENTING:** Folder `wwwroot/files/templates/` berisi template `.docx` untuk setiap jenis BA. Pastikan folder ini ikut tersalin ke server.

### 4.3 Transfer ke server

Salin seluruh folder `publish/` ke server, misalnya:

```
\\dev-server\C$\inetpub\SistemBeritaAcara\
```

Gunakan **WinSCP**, **Robocopy**, atau **xcopy**:

```cmd
robocopy ".\publish" "\\dev-server\C$\inetpub\SistemBeritaAcara" /E /PURGE /XF appsettings.Production.json
```

> `/XF appsettings.Production.json` — jangan timpa file konfigurasi production yang sudah ada di server.

---

## 5. Setup SQL Server

### 5.1 Buat Database

Buka **SQL Server Management Studio (SSMS)**, sambungkan ke instance SQL Server di server, lalu jalankan:

```sql
CREATE DATABASE [SistemBeritaAcara]
    COLLATE Latin1_General_CI_AS;
GO
```

> Collation `Latin1_General_CI_AS` penting agar karakter Indonesia tersimpan dengan benar.

### 5.2 Buat SQL Login untuk Aplikasi (Opsional)

Jika tidak menggunakan Windows Authentication:

```sql
CREATE LOGIN [sba_app] WITH PASSWORD = 'GantiDenganPasswordKuat!123';
GO

USE [SistemBeritaAcara];
CREATE USER [sba_app] FOR LOGIN [sba_app];
ALTER ROLE [db_owner] ADD MEMBER [sba_app];
GO
```

### 5.3 Connection String

**Opsi A — Windows Authentication** (disarankan di domain Pertamina):
```
Server=NAMA_SERVER\SQLEXPRESS;Database=SistemBeritaAcara;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true
```

**Opsi B — SQL Authentication**:
```
Server=NAMA_SERVER\SQLEXPRESS;Database=SistemBeritaAcara;User Id=sba_app;Password=GantiDenganPasswordKuat!123;TrustServerCertificate=True;MultipleActiveResultSets=true
```

> Ganti `NAMA_SERVER` dengan hostname atau IP server SQL, contoh: `DEVSRV01\SQLEXPRESS`

### 5.4 Schema Database

Schema dibuat **otomatis** oleh aplikasi saat pertama dijalankan (`EnsureCreated()` + startup migration). Tidak perlu menjalankan migration manual.

---

## 6. Setup IIS

### 6.1 Buat Application Pool

Buka IIS Manager → **Application Pools** → **Add Application Pool**:

| Setting | Nilai |
|---|---|
| Name | `SistemBeritaAcara` |
| .NET CLR Version | **No Managed Code** |
| Managed Pipeline | Integrated |

Setelah dibuat, klik kanan pool → **Advanced Settings**:

| Setting | Nilai |
|---|---|
| Identity | `ApplicationPoolIdentity` atau akun domain khusus |
| Start Mode | `AlwaysRunning` |
| Idle Time-out | `0` (disable) |
| Regular Time Interval | `0` (disable recycle) |

> Untuk akses resource domain (SMTP internal, SQL via Windows Auth), gunakan **akun domain** sebagai Identity.

### 6.2 Buat Website / Application

Buka IIS Manager → **Sites** → **Add Website**:

| Setting | Nilai |
|---|---|
| Site name | `SistemBeritaAcara` |
| Application pool | `SistemBeritaAcara` (yang baru dibuat) |
| Physical path | `C:\inetpub\SistemBeritaAcara\` |
| Binding — Type | `http` |
| Binding — Port | `80` atau port yang tersedia |
| Binding — Host name | `sba.pertamina.com` (jika sudah ada domain internal) |

### 6.3 Konfigurasi web.config

File `web.config` dibuat otomatis oleh `dotnet publish`. Verifikasi isinya di server:

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <location path="." inheritInChildApplications="false">
    <system.webServer>
      <handlers>
        <add name="aspNetCore" path="*" verb="*"
             modules="AspNetCoreModuleV2"
             resourceType="Unspecified" />
      </handlers>
      <aspNetCore processPath="dotnet"
                  arguments=".\SistemBeritaAcara.Web.dll"
                  stdoutLogEnabled="true"
                  stdoutLogFile=".\logs\stdout"
                  hostingModel="inprocess">
        <environmentVariables>
          <environmentVariable name="ASPNETCORE_ENVIRONMENT" value="Production" />
        </environmentVariables>
      </aspNetCore>
    </system.webServer>
  </location>
</configuration>
```

Buat folder `logs` agar stdout log bisa ditulis:
```cmd
mkdir C:\inetpub\SistemBeritaAcara\logs
```

### 6.4 Permission Folder

Identity Application Pool harus bisa **membaca, menulis, dan menghapus** file:

```cmd
icacls "C:\inetpub\SistemBeritaAcara" /grant "IIS AppPool\SistemBeritaAcara:(OI)(CI)(M)" /T
```

Folder `files` perlu akses penuh (buat subfolder, tulis dokumen):
```cmd
icacls "C:\inetpub\SistemBeritaAcara\wwwroot\files" /grant "IIS AppPool\SistemBeritaAcara:(OI)(CI)(F)" /T
```

---

## 7. Konfigurasi appsettings.Production.json

Buat file ini **langsung di server**, di folder aplikasi. File ini **tidak di-commit ke Git** (sudah ada di `.gitignore`).

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=DEVSRV01\\SQLEXPRESS;Database=SistemBeritaAcara;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true"
  },
  "Email": {
    "SmtpHost": "smtp.pertamina.com",
    "SmtpPort": "587",
    "Username": "sba-noreply@pertamina.com",
    "Password": "ISI_PASSWORD_AKUN_EMAIL_SERVICE",
    "From": "sba-noreply@pertamina.com"
  },
  "App": {
    "BaseUrl": "http://sba.dev.pertamina.com",
    "PublicUrl": "http://sba.dev.pertamina.com"
  },
  "OnlyOffice": {
    "ServerUrl": "http://localhost:8081",
    "CallbackSecret": "ISI_STRING_ACAK_MINIMAL_32_KARAKTER"
  },
  "Gotenberg": {
    "ServerUrl": "http://localhost:3000"
  },
  "NomorSurat": {
    "FormatUmum": "BA-{counter}/PPNEG1000/{year}-S0",
    "FormatLainnya": "BA-{counter}/PPNEG1000/{year}-S8"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Warning",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*"
}
```

### Penjelasan Konfigurasi Penting

| Key | Keterangan |
|---|---|
| `App:PublicUrl` | URL yang bisa dibuka dari browser pengguna dan dari email client — digunakan di magic link PJ |
| `App:BaseUrl` | URL internal server (sama dengan PublicUrl jika tidak ada reverse proxy terpisah) |
| `OnlyOffice:CallbackSecret` | String acak minimal 32 karakter — mengamankan endpoint callback dari akses tidak sah |
| `NomorSurat:FormatUmum` | Format nomor surat BA reguler. `{counter}` = nomor urut, `{year}` = tahun |

> **PENTING:** `App:PublicUrl` harus bisa diakses dari klien email penerima (termasuk jika dibuka di HP). Jangan gunakan `localhost`.

---

## 8. Konfigurasi Email @pertamina.com

Pertamina Patra Niaga menggunakan Microsoft Exchange / Microsoft 365. Ada beberapa opsi:

### Opsi A — SMTP Relay Internal Exchange (Tanpa Autentikasi)

Cocok jika server berada di jaringan internal Pertamina dan Exchange dikonfigurasi sebagai relay:

```json
"Email": {
  "SmtpHost": "mail.pertamina.com",
  "SmtpPort": "25",
  "Username": "",
  "Password": "",
  "From": "sba-noreply@pertamina.com"
}
```

Hubungi tim IT Pertamina untuk: IP address SMTP relay, whitelist IP server, dan konfigurasi connector di Exchange.

### Opsi B — Akun Service Microsoft 365 (Dengan Autentikasi)

```json
"Email": {
  "SmtpHost": "smtp.office365.com",
  "SmtpPort": "587",
  "Username": "sba-noreply@pertamina.com",
  "Password": "APP_PASSWORD_ATAU_OAUTH2_TOKEN",
  "From": "sba-noreply@pertamina.com"
}
```

> Untuk akun @pertamina.com di Microsoft 365, kemungkinan besar perlu **App Password** atau konfigurasi **OAuth2**. Koordinasikan dengan tim IT/ICT Pertamina.

### Opsi C — Gmail (Development / Sementara)

Untuk pengembangan atau jika belum ada akun service Pertamina:

```json
"Email": {
  "SmtpHost": "smtp.gmail.com",
  "SmtpPort": "587",
  "Username": "alamat@gmail.com",
  "Password": "app-password-16-karakter",
  "From": "alamat@gmail.com"
}
```

> App Password Gmail: Google Account → Security → 2-Step Verification → App Passwords.

### Verifikasi Email

Setelah konfigurasi, test dengan:
1. Login sebagai Admin IT
2. **Tambah Pengguna** → isi form → klik Kirim Undangan
3. Periksa apakah email undangan masuk ke inbox penerima

---

## 9. Konfigurasi HTTPS / SSL

### 9.1 Menggunakan Sertifikat Internal Pertamina (Disarankan)

Pertamina kemungkinan memiliki **Internal Certificate Authority (CA)** sendiri. Koordinasikan dengan tim IT untuk mendapatkan file `.pfx`.

Install sertifikat di IIS:
1. IIS Manager → nama server → **Server Certificates** → **Import**
2. Pilih file `.pfx` → masukkan password
3. Site `SistemBeritaAcara` → **Bindings** → **Add**:
   - Type: `https` · Port: `443` · Host name: `sba.dev.pertamina.com`
   - SSL certificate: pilih sertifikat yang diimport

### 9.2 Redirect HTTP → HTTPS

Di IIS Manager → pilih site → **HTTP Redirect**:
- Centang **Redirect requests to this destination**
- Isi: `https://sba.dev.pertamina.com`
- Status: `301 Permanent`

### 9.3 Update KnownProxies (jika ada reverse proxy terpisah)

Jika HTTPS di-handle oleh load balancer atau Nginx di server berbeda, tambahkan IP proxy ke `Program.cs` lalu publish ulang:

```csharp
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor 
                     | ForwardedHeaders.XForwardedProto 
                     | ForwardedHeaders.XForwardedHost,
    KnownProxies = {
        System.Net.IPAddress.Loopback,
        System.Net.IPAddress.IPv6Loopback,
        System.Net.IPAddress.Parse("192.168.X.X") // ← IP reverse proxy
    }
});
```

---

## 10. Setup OnlyOffice (Edit Manual)

OnlyOffice digunakan untuk fitur **Edit Manual** dokumen DOCX. Jika tidak dibutuhkan, aplikasi tetap berjalan normal tanpa fitur ini.

### 10.1 Install via Docker (Disarankan)

```bash
docker pull onlyoffice/documentserver

docker run -d -p 8081:80 \
  --name onlyoffice \
  --restart always \
  -v /var/www/onlyoffice/Data:/var/www/onlyoffice/Data \
  onlyoffice/documentserver
```

### 10.2 Install via Windows (Tanpa Docker)

Download dari: https://www.onlyoffice.com/download-docs.aspx  
Pilih: **ONLYOFFICE Docs Community Edition for Windows**

### 10.3 Konfigurasi local.json

Salin file `local.json` (ada di root project) ke direktori konfigurasi OnlyOffice:

- **Docker:** `docker cp local.json onlyoffice:/etc/onlyoffice/documentserver/local.json`
- **Windows:** `C:\Program Files\ONLYOFFICE\DocumentServer\config\local.json`

Isi file `local.json`:
```json
{
  "services": {
    "CoAuthoring": {
      "request-filtering-agent": {
        "allowPrivateIPAddress": true,
        "allowMetaIPAddress": true
      },
      "token": {
        "enable": {
          "request": { "inbox": false, "outbox": false },
          "browser": false
        }
      }
    }
  }
}
```

Restart OnlyOffice setelah konfigurasi:
```bash
docker restart onlyoffice
```

### 10.4 Konfigurasi CallbackSecret

Isi `CallbackSecret` di `appsettings.Production.json` dengan string acak yang kuat (minimal 32 karakter):

```json
"OnlyOffice": {
  "ServerUrl": "http://localhost:8081",
  "CallbackSecret": "Hx9Kz2mP4qR7vN1wY6tL3dF8bJ0cA5sE"
}
```

Secret ini digunakan untuk memverifikasi bahwa request ke endpoint `/api/onlyoffice/callback/{id}` benar-benar berasal dari OnlyOffice.

---

## 11. Setup Gotenberg (Konversi PDF)

Gotenberg adalah server konversi dokumen berbasis LibreOffice. Digunakan sebagai **fallback** atau alternatif Spire.Doc untuk mengonversi DOCX ke PDF.

### 11.1 Install via Docker (Disarankan)

```bash
docker pull gotenberg/gotenberg:8

docker run -d -p 3000:3000 \
  --name gotenberg \
  --restart always \
  gotenberg/gotenberg:8
```

Verifikasi:
```bash
curl http://localhost:3000/health
# Output: {"status":"up"}
```

### 11.2 Install via Windows (Tanpa Docker)

Gotenberg tidak tersedia sebagai binary Windows native. Gunakan Docker Desktop untuk Windows atau WSL2.

### 11.3 Konfigurasi

Update `appsettings.Production.json`:

```json
"Gotenberg": {
  "ServerUrl": "http://localhost:3000"
}
```

Jika Gotenberg tidak tersedia, konversi PDF tetap berjalan menggunakan **Spire.Doc** yang sudah terpasang. Preview dan generate PDF tidak terganggu.

---

## 12. Konfigurasi Reverse Proxy (Nginx / IIS ARR)

### Opsi A — IIS in-process (Paling Sederhana)

`web.config` dengan `hostingModel="inprocess"` sudah mengkonfigurasi IIS agar meng-host aplikasi .NET secara langsung. Tidak perlu konfigurasi tambahan untuk ini.

### Opsi B — Nginx sebagai Reverse Proxy

Jika menggunakan Nginx di depan Kestrel:

```nginx
server {
    listen 443 ssl;
    server_name sba.dev.pertamina.com;

    ssl_certificate     /etc/ssl/certs/sba.crt;
    ssl_certificate_key /etc/ssl/private/sba.key;

    # WebSocket untuk SignalR (WAJIB)
    location /hubs/ {
        proxy_pass         http://localhost:5000;
        proxy_http_version 1.1;
        proxy_set_header   Upgrade $http_upgrade;
        proxy_set_header   Connection "upgrade";
        proxy_set_header   Host $host;
        proxy_set_header   X-Real-IP $remote_addr;
        proxy_set_header   X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header   X-Forwarded-Proto $scheme;
        proxy_read_timeout 86400s;
        proxy_send_timeout 86400s;
    }

    location / {
        proxy_pass         http://localhost:5000;
        proxy_http_version 1.1;
        proxy_set_header   Connection keep-alive;
        proxy_set_header   Host $host;
        proxy_set_header   X-Real-IP $remote_addr;
        proxy_set_header   X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header   X-Forwarded-Proto $scheme;
        proxy_set_header   X-Forwarded-Host $host;
        proxy_cache_bypass $http_upgrade;
        client_max_body_size 50M;
    }
}

server {
    listen 80;
    server_name sba.dev.pertamina.com;
    return 301 https://$host$request_uri;
}
```

> **SignalR memerlukan WebSocket** — pastikan blok `/hubs/` dengan `Upgrade` dan `Connection upgrade` dikonfigurasi.

### Jalankan Kestrel sebagai Windows Service (Alternatif IIS)

Jika tidak menggunakan IIS:

```cmd
sc create "SistemBeritaAcara" binPath= "C:\inetpub\SistemBeritaAcara\SistemBeritaAcara.Web.exe" start= auto
sc description "SistemBeritaAcara" "SIMBA - Sistem Informasi Manajemen Berita Acara"
sc start "SistemBeritaAcara"
```

---

## 13. Konfigurasi Firewall & Port

### Port yang Perlu Dibuka di Server

| Port | Protokol | Arah | Keterangan |
|---|---|---|---|
| 80 | TCP | Inbound | HTTP (redirect ke HTTPS) |
| 443 | TCP | Inbound | HTTPS (akses utama dari client) |
| 1433 | TCP | Lokal | SQL Server (internal saja) |
| 8081 | TCP | Lokal | OnlyOffice (internal saja) |
| 3000 | TCP | Lokal | Gotenberg (internal saja) |
| 587 / 25 | TCP | Outbound | SMTP ke mail server Pertamina |

### Windows Firewall (PowerShell — run as Administrator)

```powershell
# Buka port HTTP dan HTTPS
New-NetFirewallRule -DisplayName "SIMBA HTTP"  -Direction Inbound -Protocol TCP -LocalPort 80  -Action Allow
New-NetFirewallRule -DisplayName "SIMBA HTTPS" -Direction Inbound -Protocol TCP -LocalPort 443 -Action Allow

# Blokir akses eksternal ke port internal
New-NetFirewallRule -DisplayName "Block OnlyOffice External" -Direction Inbound -Protocol TCP -LocalPort 8081 -RemoteAddress Internet -Action Block
New-NetFirewallRule -DisplayName "Block Gotenberg External"  -Direction Inbound -Protocol TCP -LocalPort 3000 -RemoteAddress Internet -Action Block
```

### DNS Internal Pertamina

Koordinasikan dengan tim IT/ICT untuk mendaftarkan hostname di DNS internal:

```
sba.dev.pertamina.com  →  192.168.X.X  (IP server)
```

---

## 14. Jalankan & Verifikasi Aplikasi

### 14.1 Start via IIS

1. IIS Manager → Sites → `SistemBeritaAcara` → **Start**
2. Application Pool `SistemBeritaAcara` → **Start**

### 14.2 Verifikasi Log

Cek `C:\inetpub\SistemBeritaAcara\logs\stdout*.log`:

```
# Tanda aplikasi berjalan normal:
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://[::]:5000
info: Hangfire.BackgroundJobServer[0]
      Starting Hangfire Server
info: Microsoft.Hosting.Lifetime[0]
      Application started.
```

### 14.3 Test Akses Browser

Buka dari client di jaringan internal:
```
http://sba.dev.pertamina.com
```

Saat pertama kali → diarahkan ke `/setup`. Jika sudah pernah di-setup → diarahkan ke `/login`.

### 14.4 Test SignalR (WebSocket)

Buka browser DevTools (F12) → Network → filter `WS`:
- Harus ada koneksi WebSocket ke `/hubs/ba` dengan status `101 Switching Protocols`
- Jika tidak ada atau error → periksa konfigurasi WebSocket Protocol di IIS

---

## 15. Setup Pertama Kali (First-Run)

Saat pertama kali aplikasi diakses dan database masih kosong, sistem otomatis redirect ke `/setup`.

### Langkah-langkah:

1. Buka `http://sba.dev.pertamina.com` → redirect ke `/setup`
2. Isi form Setup Admin IT:
   - **Nama Lengkap** — nama admin IT
   - **Email** — alamat email @pertamina.com
   - **Password** — min. 8 karakter
   - **Tanda Tangan** — gambar TTD (upload file atau gambar langsung di kanvas)
3. Klik **Selesai & Masuk**
4. Sistem membuat:
   - Roles: `AdminIT`, `AdminGudangBarang`, `Reviewer`
   - Data Pegawai untuk Admin IT
   - Akun user Admin IT
5. Login otomatis ke Dashboard

---

## 16. Import Data Master

### 16.1 Import Data Pegawai

1. Login sebagai **Admin IT**
2. Sidebar → **Data Pekerja** → tombol **Import Excel**
3. Upload file `.xlsx` dengan kolom:

| Kolom | Keterangan |
|---|---|
| Nama | Nama lengkap pegawai |
| NoPekerja | Nomor pekerja |
| Jabatan | Jabatan/posisi |
| CostCenter | Kode cost center |
| FungsiDirektorat | Fungsi atau direktorat |
| Alamat | Alamat unit kerja |
| Email | Email @pertamina.com |
| NoTelp | Nomor telepon |

> Batas ukuran file Excel: **10 MB**

### 16.2 Import Data Barang

1. Sidebar → **Data Barang** → tombol **Import Excel**
2. Upload file `.xlsx` dengan kolom nama barang

### 16.3 Tambah Pengguna Sistem

1. Sidebar → **Daftar Pengguna** → **Tambah Pengguna**
2. Isi data:
   - Email harus terdaftar di Data Pekerja
   - Pilih role: `AdminGudangBarang` atau `Reviewer`
3. Sistem kirim email undangan ke alamat tersebut
4. Pengguna buka link undangan → setup password & TTD → akun aktif

---

## 17. Perawatan & Monitoring

### 17.1 Backup Database

Jadwalkan backup otomatis di SQL Server Agent:

```sql
BACKUP DATABASE [SistemBeritaAcara]
TO DISK = N'D:\Backup\SistemBeritaAcara_' + FORMAT(GETDATE(), 'yyyyMMdd') + N'.bak'
WITH COMPRESSION, STATS = 10;
```

### 17.2 Backup File Generated

Salin periodik folder-folder berikut ke storage terpisah:

```
C:\inetpub\SistemBeritaAcara\wwwroot\files\documents\   ← DOCX & PDF BA
C:\inetpub\SistemBeritaAcara\wwwroot\files\signatures\  ← gambar TTD pengguna
```

### 17.3 Hangfire Dashboard

Monitor background job di:
```
http://sba.dev.pertamina.com/hangfire
```

Hanya bisa diakses oleh user yang login sebagai **Admin IT**.

Background jobs yang berjalan otomatis:
- **DueDateCheckerJob** — cek BA peminjaman yang mendekati tanggal kembali
- **AutoApproveJob** — auto-approve BA jika PJ tidak menandatangani dalam batas waktu

### 17.4 Log Aplikasi

```
C:\inetpub\SistemBeritaAcara\logs\stdout*.log
```

Untuk melihat log real-time via PowerShell:
```powershell
Get-Content "C:\inetpub\SistemBeritaAcara\logs\stdout*.log" -Tail 50 -Wait
```

### 17.5 Update Aplikasi

```cmd
# 1. Stop Application Pool
%windir%\system32\inetsrv\appcmd stop apppool /apppool.name:SistemBeritaAcara

# 2. Publish baru dari developer machine
dotnet publish SistemBeritaAcara.Web -c Release -r win-x64 --self-contained false -o ./publish

# 3. Salin ke server — kecualikan appsettings.Production.json dan folder files
robocopy ".\publish" "C:\inetpub\SistemBeritaAcara" /E /XF appsettings.Production.json /XD wwwroot\files

# 4. Start Application Pool
%windir%\system32\inetsrv\appcmd start apppool /apppool.name:SistemBeritaAcara
```

---

## 18. Troubleshooting

### HTTP Error 500.30 — ASP.NET Core app failed to start

**Penyebab:** Hosting Bundle tidak terinstall atau versi salah.

**Solusi:**
```cmd
dotnet --version   # harus 10.x.x
iisreset /restart
```

---

### HTTP Error 500.19 — web.config error

**Penyebab:** ASP.NET Core Module (ANCM) tidak ditemukan atau web.config salah.

**Solusi:**
1. Install ulang .NET 10 Hosting Bundle
2. `iisreset /restart`
3. Periksa `stdoutLogEnabled="true"` di web.config dan baca log di folder `logs/`

---

### Aplikasi jalan tapi tidak bisa login / blank page

**Penyebab:** Database tidak bisa diakses.

**Solusi:**
1. Cek connection string di `appsettings.Production.json`
2. Pastikan SQL Server service berjalan: `services.msc` → SQL Server
3. Test koneksi: `sqlcmd -S NAMA_SERVER\SQLEXPRESS -Q "SELECT 1"`

---

### Notifikasi real-time tidak berfungsi (SignalR)

**Penyebab:** WebSocket Protocol tidak aktif di IIS.

**Solusi:**
```powershell
Install-WindowsFeature Web-WebSockets
iisreset /restart
```

---

### Email tidak terkirim

**Penyebab:** Konfigurasi SMTP salah atau server email memblokir.

**Solusi:**
1. Cek log aplikasi untuk error SMTP
2. Test koneksi SMTP: `telnet smtp.pertamina.com 587`
3. Koordinasikan dengan tim IT untuk whitelist IP server di Exchange
4. Pastikan port 587/25 outbound dibuka di firewall

---

### Link tanda tangan PJ di email tidak bisa dibuka

**Penyebab:** `App:PublicUrl` masih berisi `localhost`.

**Solusi:** Update `appsettings.Production.json`:
```json
"App": {
  "PublicUrl": "http://sba.dev.pertamina.com"
}
```

---

### Preview PDF tidak muncul

**Penyebab:** Konversi DOCX ke PDF gagal (Spire.Doc atau Gotenberg tidak tersedia).

**Solusi:**
1. Cek log aplikasi — cari `[DocumentService]` atau `[EditorBA]`
2. Pastikan Gotenberg berjalan: `curl http://localhost:3000/health`
3. Pastikan template `.docx` ada di `wwwroot/files/templates/`
4. Coba download file `.docx` dari `/api/ba/{id}/download` dan buka manual

---

### OnlyOffice "Dokumen tidak bisa dimuat"

**Penyebab:** OnlyOffice tidak bisa memanggil callback ke server aplikasi.

**Solusi:**
1. Pastikan `App:PublicUrl` bisa diakses dari mesin OnlyOffice
2. Pastikan `local.json` sudah berisi `allowPrivateIPAddress: true`
3. Restart container: `docker restart onlyoffice`
4. Periksa `OnlyOffice:CallbackSecret` sama antara `local.json` dan `appsettings.Production.json`

---

## 19. Checklist Deploy

### Persiapan Server
- [ ] Windows Server 2019/2022 aktif dan bisa diakses
- [ ] IIS terinstall dengan **WebSocket Protocol** aktif
- [ ] .NET 10 Hosting Bundle terinstall (`dotnet --version` = `10.x.x`)
- [ ] SQL Server instance berjalan
- [ ] (Opsional) Docker terinstall untuk OnlyOffice dan Gotenberg

### Aplikasi
- [ ] `dotnet publish` berhasil tanpa error
- [ ] Folder publish tersalin ke server
- [ ] Template `.docx` ada di `wwwroot/files/templates/`
- [ ] Folder `wwwroot/files/` bisa ditulis oleh Application Pool

### Database
- [ ] Database `SistemBeritaAcara` dibuat dengan collation `Latin1_General_CI_AS`
- [ ] Connection string di `appsettings.Production.json` benar
- [ ] Test koneksi dari server berhasil

### IIS
- [ ] Application Pool `SistemBeritaAcara` dibuat (No Managed Code, AlwaysRunning)
- [ ] Website mengarah ke folder yang benar
- [ ] Permission folder diset dengan `icacls`
- [ ] `web.config` ada dan valid (`ASPNETCORE_ENVIRONMENT=Production`)
- [ ] Folder `logs/` dibuat

### Konfigurasi
- [ ] `appsettings.Production.json` dibuat di server (bukan di Git)
- [ ] `App:PublicUrl` diisi dengan URL yang bisa diakses dari luar
- [ ] `OnlyOffice:CallbackSecret` diisi string acak ≥32 karakter
- [ ] Connection string production benar
- [ ] `NomorSurat` format sudah sesuai (BA-{counter}/PPNEG1000/{year}-S0)

### Email
- [ ] SMTP host, port, username, password benar
- [ ] Test kirim email undangan berhasil
- [ ] Link undangan di email bisa dibuka dari client

### Layanan Eksternal
- [ ] (Opsional) OnlyOffice berjalan di port 8081 dan bisa diakses dari aplikasi
- [ ] (Opsional) `local.json` OnlyOffice sudah dikonfigurasi
- [ ] (Opsional) Gotenberg berjalan di port 3000 (`/health` mengembalikan `{"status":"up"}`)

### DNS & Network
- [ ] DNS `sba.dev.pertamina.com` sudah didaftarkan ke IP server
- [ ] Port 80 dan 443 terbuka dari intranet Pertamina
- [ ] Port 587/25 outbound ke mail server terbuka
- [ ] Port 8081 dan 3000 diblokir dari akses eksternal

### HTTPS (Opsional untuk Dev, Wajib untuk Production)
- [ ] Sertifikat SSL dari CA internal Pertamina tersedia
- [ ] Binding HTTPS 443 dikonfigurasi di IIS
- [ ] Redirect HTTP → HTTPS aktif

### First-Run & Fungsional
- [ ] Buka URL → diarahkan ke `/setup`
- [ ] Akun Admin IT berhasil dibuat
- [ ] Login berhasil masuk ke Dashboard
- [ ] Import data Pekerja berhasil
- [ ] Import data Barang berhasil
- [ ] Tambah pengguna (AdminGB + Reviewer) → email undangan terkirim
- [ ] Pengguna bisa setup akun via link undangan
- [ ] Alur lengkap BA berhasil: Buat → Kirim → PJ Tanda Tangan → Reviewer → Arsip

### Monitoring
- [ ] Log stdout aktif dan bisa dibaca
- [ ] Hangfire Dashboard bisa diakses di `/hangfire` (login Admin IT)
- [ ] Backup database dijadwalkan
- [ ] Backup folder files dijadwalkan

---

<div align="center">

*SIMBA — Sistem Informasi Manajemen Berita Acara*  
*PT Pertamina Patra Niaga SSC ICT · Regional Sumbagsel*  
*.NET 10 · SQL Server 2019+ · Blazor Server*

</div>
