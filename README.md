# ProjectFinal

## Cơ sở dữ liệu demo

Project có bộ script SQL Server cho quản lý ký túc xá trong [database/README.md](database/README.md). Database `KTX_Demo` đã được tạo và nạp dữ liệu mẫu trên instance LocalDB `KTXDemo` của máy này. Tài khoản demo được lưu trong bảng `UserAccounts`.

Để chạy ứng dụng, mở hai terminal tại thư mục project:

```powershell
npm run start:api
npm start
```

Mở `http://localhost:4200/` và đăng nhập bằng **admin / Admin@123456**. `npm run start:api` chạy ExpressJS ở `127.0.0.1:5100` và ASP.NET Core ở `127.0.0.1:5101`; Angular chuyển tiếp `/api` đến Express. Express cung cấp bản dịch Việt/Anh/Trung, giới hạn tốc độ và kiểm tra Origin trước khi chuyển các API dữ liệu đến backend SQL Server. Sau khi đăng nhập, ứng dụng chuyển đến `/main`. Trên máy khác, chạy các script database theo [hướng dẫn](database/README.md) trước khi khởi động backend.

Nếu cổng 4200 đang được dùng, chạy `npm start -- --port 4201` và mở `http://localhost:4201/`. Bản demo hiện tại đang chạy trên cổng 4201.

Trang quản trị gồm tổng quan, tòa nhà, phòng, giường, sinh viên, lượt ở, hóa đơn, thanh toán và bảo trì. Mỗi danh sách có tìm kiếm, biểu mẫu thêm/sửa và thao tác xóa. Khi trả phòng, sửa lượt ở sang `Đã trả phòng`; hệ thống điền ngày trả nếu để trống. Hóa đơn hiển thị số đã thu và số còn nợ, thanh toán không được vượt số còn nợ. Mục đã có dữ liệu liên quan sẽ không thể xóa cho đến khi xử lý các bản ghi phụ thuộc.

## Cài đặt và bảo mật

Trang **Cài đặt** lưu ngôn ngữ và giao diện theo tài khoản. Để bật xác thực 2 lớp, nhập mật khẩu hiện tại, quét QR bằng Google Authenticator, rồi nhập mã 6 số để xác nhận. Lưu 8 mã khôi phục ngay khi chúng xuất hiện; mỗi mã chỉ dùng một lần. Sau khi bật, đăng nhập cần thêm mã Authenticator hoặc mã khôi phục. Có thể tạo lại mã khôi phục, tắt 2FA hoặc đổi mật khẩu trong Cài đặt. Tắt 2FA và đổi mật khẩu sẽ đăng xuất mọi phiên cũ.

Tài khoản demo ban đầu **chưa bật 2FA** để bạn có thể tự quét QR trên thiết bị của mình. Không dùng mật khẩu demo cho dữ liệu thật. API dùng cookie `HttpOnly`, `Secure`, `SameSite=Strict`, giới hạn lần thử và khóa tạm tài khoản sau nhiều lần sai. Khóa TOTP được mã hóa bằng ASP.NET Data Protection trên máy chạy backend; mã khôi phục chỉ lưu hash. Khi triển khai qua mạng, cấu hình HTTPS ở proxy phía trước và giới hạn `ALLOWED_ORIGINS` của Express theo domain thật.

Kiểm thử tích hợp trên máy demo: `node scripts/security-smoke.mjs` tạo một tài khoản SQL tạm, thử đăng nhập/2FA/cài đặt/mã khôi phục rồi xóa tài khoản đó.

This project uses [Angular CLI](https://github.com/angular/angular-cli) 22.2.

## Development server

To start a local development server, run:

```bash
ng serve
```

Once the server is running, open your browser and navigate to `http://localhost:4200/`. The application will automatically reload whenever you modify any of the source files.

## Code scaffolding

Angular CLI includes powerful code scaffolding tools. To generate a new component, run:

```bash
ng generate component component-name
```

For a complete list of available schematics (such as `components`, `directives`, or `pipes`), run:

```bash
ng generate --help
```

## Building

To build the project run:

```bash
ng build
```

This will compile your project and store the build artifacts in the `dist/` directory. By default, the production build optimizes your application for performance and speed.

## Running unit tests

To execute unit tests with the [Vitest](https://vitest.dev/) test runner, use the following command:

```bash
ng test
```

## Running end-to-end tests

For end-to-end (e2e) testing, run:

```bash
ng e2e
```

Angular CLI does not come with an end-to-end testing framework by default. You can choose one that suits your needs.

## Additional Resources

For more information on using the Angular CLI, including detailed command references, visit the [Angular CLI Overview and Command Reference](https://angular.dev/tools/cli) page.
