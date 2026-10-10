# EzAcrossControl

**Kết nối. Điều khiển. Tiếp tục.** · by ElementZero

[Tất cả ngôn ngữ và hướng dẫn kỹ thuật đầy đủ (tiếng Bồ Đào Nha)](../../README.md)

Điều khiển thiết bị Android bằng chuột và bàn phím Windows. Di chuyển con trỏ tới cạnh màn hình đã chọn để chuyển quyền điều khiển. Hai ứng dụng liên lạc qua mạng nội bộ, không cần tài khoản hoặc định tuyến qua đám mây.

## Tải phiên bản Windows 1.1.4 / Android 1.1.2

- [Bộ cài Windows](https://github.com/eddesignerez/EzAcrossControl/releases/download/v1.1.4/EZAcrossControl-1.1.4-windows-x64-setup.exe)
- [APK Android](https://github.com/eddesignerez/EzAcrossControl/releases/download/v1.1.2/EZAcrossControl-1.1.2-android.apk)
- [ZIP Windows di động](https://github.com/eddesignerez/EzAcrossControl/releases/download/v1.1.4/EZAcrossControl-1.1.4-windows-x64-portable.zip)
- [Các bản phát hành và kiểm tra tính toàn vẹn](https://github.com/eddesignerez/EzAcrossControl/releases)

## Yêu cầu và kết nối lần đầu

Cần Windows 10/11 64-bit, Android 7 trở lên và cả hai thiết bị trên cùng mạng LAN, kể cả khi chọn USB để điều khiển gốc. Hãy cho phép gỡ lỗi USB hoặc ghép đôi gỡ lỗi không dây (Android 11 trở lên). Điều khiển gốc phụ thuộc vào hỗ trợ UHID của thiết bị.

1. Cài đặt và mở EzAcrossControl trên Windows. Bộ cài yêu cầu quyền quản trị để cấu hình máy chủ cục bộ và tường lửa cho mạng riêng.
2. Cài APK. Trên Android, mở **Chế độ nâng cao → Gỡ lỗi → Mở cài đặt**, bật tùy chọn nhà phát triển và phương thức gỡ lỗi, rồi cho phép máy tính.
3. Để gỡ lỗi không dây, ghép đôi thiết bị bằng ADB đi kèm theo [hướng dẫn chi tiết](../../README.md#primeiro-uso). Cổng ghép đôi khác cổng kết nối.
4. Trong APK, chọn **Auto**, **Wi-Fi** hoặc **USB**. Nhập IP máy chủ hiển thị trên Windows và cùng một cổng trên hai ứng dụng (mặc định **8765**). Chạm **Kết nối**, rồi chọn cạnh màn hình Android trên Windows.

Android thường dùng ngôn ngữ hệ thống. Có thể thay đổi trong **Chế độ nâng cao → Ngôn ngữ**; trên Windows, bộ chọn nằm ở phần Chẩn đoán.

## Hình xem trước và giới hạn

![Đề xuất Day tĩnh](../../Brand/Assets/PNG/splash-day-1600x900.png)
![Đề xuất Night tĩnh](../../Brand/Assets/PNG/splash-night-1600x900.png)

Đây là **đề xuất thương hiệu tĩnh, không phải ảnh chụp ứng dụng đang chạy**. Kiểu chữ và phương án phối màu vẫn chờ phê duyệt. Kiểm thử trên thiết bị thật chỉ gồm HiPadPlus và LAVIE T11; thiết bị khác cần được kiểm tra riêng. Bộ cài Windows chưa có chữ ký Authenticode. Xem [khắc phục sự cố và kiến trúc](../../README.md#solução-de-problemas) cùng [thông báo bên thứ ba](../../THIRD-PARTY-NOTICES.md).
