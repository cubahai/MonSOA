# SQL Server demo: Quản lý ký túc xá

Database `KTX_Demo` gồm 10 bảng: tòa nhà, phòng, giường, sinh viên, lượt ở, hóa đơn, thanh toán, yêu cầu bảo trì, tài khoản đăng nhập và mã khôi phục 2FA. Hai view hỗ trợ xem số giường trống và công nợ. Dữ liệu mẫu có 2 tòa, 6 phòng, 12 giường, 8 sinh viên, 6 lượt ở, 6 hóa đơn, 3 khoản thanh toán và 1 tài khoản quản trị.

## Cài đặt

Cần SQL Server 2016 SP1 trở lên, SQL Server Express hoặc LocalDB và tài khoản có quyền tạo database. Mở các file sau trong SQL Server Management Studio và chạy **theo thứ tự**, mỗi file một lần:

1. `00_create_database.sql`
2. `01_schema.sql`
3. `02_seed_demo.sql`
4. `04_demo_account.sql` (có thể chạy lại)
5. `05_security_settings.sql` (có thể chạy lại)
6. `03_demo_queries.sql` (chỉ đọc, có thể chạy lại)

Hoặc dùng PowerShell với `sqlcmd` (thay tên server cho phù hợp):

```powershell
sqlcmd -S "(localdb)\KTXDemo" -E -b -f 65001 -i database/00_create_database.sql
sqlcmd -S "(localdb)\KTXDemo" -E -b -f 65001 -i database/01_schema.sql
sqlcmd -S "(localdb)\KTXDemo" -E -b -f 65001 -i database/02_seed_demo.sql
sqlcmd -S "(localdb)\KTXDemo" -E -b -f 65001 -i database/04_demo_account.sql
sqlcmd -S "(localdb)\KTXDemo" -E -b -f 65001 -i database/05_security_settings.sql
sqlcmd -S "(localdb)\KTXDemo" -E -b -f 65001 -i database/03_demo_queries.sql
```

Instance LocalDB `KTXDemo` đã được tạo và database đã được nạp trên máy demo này. Trên máy khác, tạo instance bằng `SqlLocalDB create KTXDemo` hoặc thay tên server trong lệnh. `-E` dùng Windows Authentication. Nếu server dùng SQL Server Authentication, thay bằng `-U <user>` và nhập mật khẩu theo cách an toàn của môi trường chạy. File seed báo lỗi khi chạy lại trên database đã có dữ liệu demo; không cần xóa dữ liệu để xem lại kết quả, chỉ chạy `03_demo_queries.sql`.

## Quan hệ chính

`Buildings → Rooms → Beds → Stays ← Students`; `Stays → Invoices → Payments`; `Rooms → MaintenanceRequests`. Hai unique index trên `Stays` bảo đảm mỗi sinh viên và mỗi giường chỉ có một lượt ở `ACTIVE`. Số tiền đã trả được cộng từ bảng `Payments`, không nhập tay vào hóa đơn.

Tài khoản demo: `admin` / `Admin@123456`. Mật khẩu được lưu dưới dạng PBKDF2-HMAC-SHA256 có salt; backend ASP.NET Core kiểm tra mật khẩu và cấp cookie phiên. Khi đăng nhập thành công, hash cũ được nâng lên 600.000 vòng PBKDF2. Bảng `UserRecoveryCodes` lưu hash mã khôi phục; khóa TOTP được mã hóa bằng Data Protection trong `UserAccounts`. Cấu hình kết nối mặc định nằm ở `backend/DormApi/appsettings.json`; có thể ghi đè qua biến môi trường `ConnectionStrings__DormDb`. Đây là tài khoản phục vụ demo, cần thay mật khẩu trước khi dùng với dữ liệu thật.
