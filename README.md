# TAGlobal Backend

REST API marketplace dengan ASP.NET Core .NET 10, EF Core/Npgsql, PostgreSQL, Swagger dan JWT.

## Menjalankan

1. Pastikan PostgreSQL berjalan; connection string lokal sudah ada di `ta_global_backend/.env` (file diabaikan Git). Isi juga `Jwt__Secret` minimal 32 karakter.
2. Dari folder `ta_global_backend` jalankan:
   ```powershell
   .\scripts\start-api.ps1
   ```
   Pada Development API menjalankan migrasi dan menyiapkan kategori, seller serta setting contoh. Produk dimulai kosong.
3. Buka Swagger di http://localhost:5277/swagger dan health check di http://localhost:5277/api/health.
4. Untuk FE, terminal kedua:
   ```powershell
   cd "D:\Project Gabut\TA Global\ta_global"
   npm run dev
   ```
   Vite meneruskan `/api` ke backend.

## Endpoint utama

- Auth JWT: `POST /api/auth/register` (buyer), `POST /api/auth/login` (email + password), `GET /api/auth/me`.
- Katalog: `GET /api/products`, `GET /api/products/{id}`, `GET /api/categories`.
- Produk: `/api/products` (seller/admin terautentikasi); seller admin area: `/api/admin/sellers`, `/api/admin/products`.
- Profil/state akun: `/api/account/profile`, `/api/account/state`; seller profile: `/api/sellers/me`.
- Chat: `/api/threads` dan `/api/threads/{id}/messages`.
- Admin: `/api/admin/providers`, `/api/admin/hubs`, `/api/admin/vouchers`, `/api/admin/broadcast`.

## Batas saat ini

Cart/wishlist/preferences disimpan lewat API. Checkout dinonaktifkan dengan sengaja: belum ada payment gateway, live courier rates/tracking, atau provisioning akun admin. UI tidak meminta data kartu dan tidak membuat pesanan palsu. Beberapa analitik dan layar historis tetap placeholder.

Jangan commit atau bagikan `.env`. Password PostgreSQL yang pernah tampil di riwayat percakapan sebaiknya dirotasi sebelum dipakai di luar development lokal.

