# BeautyShop - Hệ Thống Bán Lẻ & Quản Lý Chuỗi Kho Mỹ Phẩm

Hệ thống Website Thương Mại Điện Tử & Quản trị phân quyền chuỗi kho phân tán, luân chuyển hàng hóa và tối ưu giao hàng chặng cuối cho ngành mỹ phẩm.

---

## 🚀 Công Nghệ Sử Dụng
- **Backend:** ASP.NET Core 8.0 MVC, Entity Framework Core
- **Database:** SQLite (`beautyshop.db` - có sẵn dữ liệu mẫu, không cần cài SQL Server)
- **Frontend:** HTML5, CSS3 (Custom Design System), JavaScript, Bootstrap 5, Bootstrap Icons
- **Tích hợp bên thứ ba:**
  - **Google Gemini AI:** Trợ lý AI tư vấn mỹ phẩm trực tuyến
  - **Giao Hàng Nhanh (GHN):** Tự động tính cước vận chuyển 63 tỉnh/thành & tạo vận đơn
  - **VietQR:** Tạo mã QR thanh toán ngân hàng tự động (MB Bank)
  - **Google SMTP:** Gửi email xác nhận đơn hàng tự động

---

## 🛠️ Hướng Dẫn Cài Đặt & Chạy Dự Án

### 1. Yêu cầu môi trường
- Cài đặt [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).
- IDE khuyên dùng: Visual Studio 2022 hoặc Visual Studio Code.

### 2. Các bước chạy dự án
```bash
# 1. Clone repository về máy
git clone https://github.com/nguyengiabao28032005-svg/KhoaLuan.git

# 2. Di chuyển vào thư mục dự án
cd KhoaLuan

# 3. Restore các thư viện NuGet
dotnet restore

# 4. Khởi chạy ứng dụng
dotnet run
```

Mở trình duyệt và truy cập: **`http://localhost:5209`**

> **Lưu ý:** Cơ sở dữ liệu SQLite (`beautyshop.db`) đã có sẵn trong dự án với đầy đủ danh mục, sản phẩm, kho bãi và tài khoản mẫu. Bạn **không cần** chạy migration hay cấu hình cơ sở dữ liệu bên ngoài.

---

## 👥 Danh Sách Tài Khoản Mẫu Để Kiểm Thử (Mật khẩu chung: `123456`)

| Vai trò | Email đăng nhập | Mật khẩu | Phạm vi / Chức vụ |
| :--- | :--- | :---: | :--- |
| **Admin** | `admin@beautyshop.com` | `123456` | Quản trị toàn hệ thống |
| **Thu ngân** | `thungan@beautyshop.com` | `123456` | Thu ngân - Cửa hàng / Kho TP.HCM |
| **Thu ngân** | `thungan_hn@beautyshop.com` | `123456` | Thu ngân - Cửa hàng / Kho Hà Nội |
| **Thủ kho** | `kho_hcm@beautyshop.com` | `123456` | Nhân viên kho - Kho tổng TP.HCM |
| **Thủ kho** | `kho_hn@beautyshop.com` | `123456` | Nhân viên kho - Kho tổng Hà Nội |
| **Shipper** | `shipper_tb@beautyshop.com` | `123456` | Nhân viên giao hàng - Cụm Tây Bắc TP.HCM |
| **Khách hàng**| `nguyengiabao28032005@gmail.com` | `123456` | Tài khoản khách hàng mua sắm |

---

## ⚙️ Cấu Hình Tùy Chọn (`appsettings.json`)

Mặc định khi clone về, toàn bộ chức năng cốt lõi (Mua sắm, Giỏ hàng, Đặt hàng COD/QR, Quản lý kho, Luân chuyển kho, Phân quyền) **đều chạy hoàn toàn bình thường**.

Nếu muốn kích hoạt thêm các dịch vụ mở rộng bên ngoài, chỉ cần cập nhật API key vào `appsettings.json`:
- **AI Chatbot (Gemini):** Điền `Gemini:ApiKey` của bạn vào `appsettings.json`.
- **Gửi Email tự động:** Điền Gmail App Password vào `EmailSettings:Password`.
- **Tính phí ship GHN:** Điền API Token vào `GHN:Token` (mặc định có fallback phí cố định 30.000đ).
