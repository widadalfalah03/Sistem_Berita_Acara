<div align="center">

<img src="SistemBeritaAcara.Web/wwwroot/images/PT_Pertamina_Patra_Niaga.svg.png" alt="PT Pertamina Patra Niaga" width="180"/>

# Sistem Berita Acara Digital

**Platform pengelolaan dokumen Berita Acara berbasis web untuk PT Pertamina Patra Niaga SSC ICT**

![.NET](https://img.shields.io/badge/.NET_10-Blazor_Server-512BD4?style=flat-square&logo=dotnet)
![SQL Server](https://img.shields.io/badge/SQL_Server-Database-CC2927?style=flat-square&logo=microsoftsqlserver)
![SignalR](https://img.shields.io/badge/SignalR-Real--time-FF6C37?style=flat-square)
![Hangfire](https://img.shields.io/badge/Hangfire-Background_Jobs-darkgreen?style=flat-square)

</div>

---

## Latar Belakang

Proses penerbitan Berita Acara (BA) di lingkungan gudang barang PT Pertamina Patra Niaga SSC ICT sebelumnya berjalan secara manual — dokumen dicetak, ditandatangani fisik, difotokopi, lalu diarsipkan ke dalam tumpukan berkas. Proses ini lambat, rawan hilang, dan sulit dilacak statusnya.

**Sistem Berita Acara Digital** hadir sebagai solusi penuh: dari pembuatan dokumen, pengiriman ke penanggung jawab untuk tanda tangan, review oleh atasan, hingga pengarsipan — semua berjalan dalam satu platform web tanpa selembar kertas pun.

---

## Alur Kerja

Berita Acara melewati beberapa tahap yang saling terhubung secara otomatis:

```
┌─────────────────────────────────────────────────────────┐
│                                                         │
│   Admin Gudang        PJ (Penanggung Jawab)  Reviewer   │
│       │                       │                  │      │
│   Isi form BA                 │                  │      │
│   + Upload foto               │                  │      │
│       │                       │                  │      │
│   Dokumen .docx               │                  │      │
│   di-generate otomatis        │                  │      │
│       │                       │                  │      │
│       └──── Email dikirim ────►                  │      │
│                         Tanda tangan digital     │      │
│                               │                  │      │
│                               └── Email ─────────►      │
│                                           Review dokumen │
│                                           Setujui / Tolak│
│                                                  │      │
│                          ◄── Jika ditolak ───────┘      │
│                     Revisi & kirim ulang                 │
│                                                  │      │
│                          Nomor surat terbit      │      │
│                          Dokumen final diarsipkan│      │
│                                                         │
└─────────────────────────────────────────────────────────┘
```

Setiap perpindahan tahap disertai **notifikasi email** dan **notifikasi real-time** di dalam aplikasi.

---

## Jenis Berita Acara

Sistem mendukung empat jenis dokumen yang masing-masing memiliki template dan format nomor surat sendiri:

| Jenis | Keterangan |
|---|---|
| **Alokasi Barang** | Pencatatan distribusi/pengeluaran barang dari gudang |
| **Peminjaman Barang** | Barang dipinjam dengan tanggal pengembalian |
| **Penarikan Barang** | Barang ditarik kembali dari lokasi pemakaian |
| **Lainnya** | Dokumen bebas dengan judul dan tujuan kustom |

---

## Fitur Utama

### Dokumen Otomatis
Setiap Berita Acara di-generate langsung dari template `.docx` yang telah dikonfigurasi. Data dari form — nama, jabatan, daftar barang, tanggal, tujuan — diisi ke dalam placeholder template secara otomatis. Hasilnya bisa langsung diunduh atau dipreview sebagai PDF di browser.

### Tanda Tangan Digital
Penanggung Jawab (PJ) menerima **magic link** melalui email — satu tautan unik yang langsung membawa ke halaman penandatanganan tanpa perlu login. Tanda tangan berupa gambar yang diunggah saat setup akun, lalu di-embed langsung ke dalam dokumen Word di posisi yang tepat.

### Auto-Approve
Jika PJ tidak menandatangani dalam batas waktu yang ditentukan, sistem secara otomatis memberikan **stempel "Auto-Approved"** pada dokumen dan melanjutkan alur ke tahap review. Proses ini dijalankan oleh background job terjadwal tanpa intervensi manual.

### Penomoran Surat Otomatis
Nomor surat diterbitkan secara otomatis menggunakan format yang bisa dikonfigurasi, misalnya `BA 001/PPNEG1000/2025-S0`. Counter per tahun dijaga konsistensinya dengan mekanisme database yang aman dari race condition.

### Real-time Notifications
Notifikasi masuk secara langsung ke inbox pengguna menggunakan **SignalR** — tanpa perlu refresh halaman. Setiap tindakan (submit, tanda tangan, approval, penolakan) memicu notifikasi ke pihak yang relevan.

### Riwayat & Audit Trail
Setiap penolakan menyimpan catatan alasan ke dalam tabel riwayat yang permanen. Admin dapat menelusuri histori lengkap siklus hidup suatu Berita Acara kapan saja.

### Arsip & Export
Dokumen yang telah disetujui masuk ke halaman **Arsip** dengan fitur pencarian, filter, dan export rekap ke file Excel (`.xlsx`) lengkap dengan hyperlink ke dokumen final.

### Import Data Master
Data Pegawai dan Master Barang dapat diperbarui secara massal melalui upload file Excel — cocok untuk sinkronisasi dengan sistem HR atau ERP yang ada.

---

## Peran Pengguna

| Role | Tanggung Jawab |
|---|---|
| **Admin IT** | Manajemen akun pengguna, import data master, konfigurasi sistem |
| **Admin Gudang Barang** | Membuat dan mengelola Berita Acara, upload foto bukti |
| **Reviewer** | Mereview dokumen final, menyetujui atau menolak Berita Acara |

---

## Tumpukan Teknologi

| Lapisan | Teknologi |
|---|---|
| Framework | ASP.NET Core 10 — Blazor Server |
| Database | Microsoft SQL Server |
| ORM | Entity Framework Core 10 |
| Autentikasi | ASP.NET Core Identity |
| Background Jobs | Hangfire |
| Editor Dokumen | OnlyOffice Document Server |
| Konversi PDF | Spire.Doc |
| Email | MailKit (SMTP) |
| Excel | ClosedXML |
| Real-time | SignalR |

---

## Arsitektur

Proyek mengikuti pola **Clean Architecture** sederhana dengan tiga lapisan:

```
SistemBeritaAcara/
├── SistemBeritaAcara.Core/           # Domain: Entitas & Interface
│   ├── Entities/                     # BeritaAcara, Pegawai, User, ...
│   └── Interfaces/                   # IDocumentService, IEmailService, ...
│
├── SistemBeritaAcara.Infrastructure/ # Data & Layanan
│   ├── Data/                         # AppDbContext, EF Migrations
│   ├── Services/                     # DocumentService, EmailService, ...
│   └── Jobs/                         # AutoApproveJob, DueDateCheckerJob
│
└── SistemBeritaAcara.Web/            # Presentasi
    ├── Components/Pages/             # Semua halaman Blazor
    ├── Security/                     # Middleware, AuthHandler
    └── wwwroot/                      # Aset statis, template dokumen
```

Core tidak bergantung pada lapisan lain — Infrastructure dan Web bergantung ke Core. Ketergantungan hanya mengalir ke dalam.

---

## Tentang Proyek

Proyek ini dikembangkan sebagai bagian dari program **magang** di PT Pertamina Patra Niaga SSC ICT. Dibangun untuk menggantikan proses manual penerbitan Berita Acara di lingkungan gudang barang, dengan fokus pada kemudahan penggunaan, keandalan alur dokumen, dan jejak audit yang lengkap.

---

<div align="center">

*Internal — PT Pertamina Patra Niaga SSC ICT*

</div>
