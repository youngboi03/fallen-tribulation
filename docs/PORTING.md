# Phát hành Steam / Google Play — những gì đã chuẩn bị và còn phải làm

## Đã có sẵn trong code
| Hạng mục | Ở đâu | Ghi chú |
|---|---|---|
| Input tập trung | `Core/GameInput.cs` | Bàn phím + chuột, tay cầm (XInput: A lướt, X Thần Thông, Start tạm dừng; RB/LB cũng được), cảm ứng. Code gameplay chỉ đọc `GameInput.*`. |
| Điều khiển cảm ứng | `TouchPad` trong `Core/GameInput.cs` | Joystick nổi nửa trái màn hình, nút Lướt / Thần Thông / Tạm dừng bên phải. Tự bật trên mobile; thử trên PC bằng `VanKiepTuTien.exe -touch` (chuột giả lập ngón tay). |
| Dịch vụ nền tảng | `Core/Platform.cs` (`IPlatformServices`) | Lưu game + mở thành tựu đi qua đây. Mặc định `LocalPlatform` (PlayerPrefs). Gắn Steam/Google Play = viết 1 class mới rồi gán `Platform.Services = new SteamPlatform();` khi khởi động. |
| Menu bằng tay cầm / phím mũi tên | `UI/PadNav.cs` | D-pad hoặc cần analog chọn nút gần nhất theo hướng, A / Enter bấm, B quay lại. Khung vàng tự ẩn khi chuột di chuyển. |
| Toàn màn hình | `F11` (và Alt+Enter mặc định của Unity) | |
| UI co giãn | `UIRoot` CanvasScaler 1920×1080, match 0.5 | Chạy được 16:9 và 16:10. Màn hình điện thoại 19.5:9 cần kiểm tra lại các bảng rộng (Shop, Pause). |
| Không phụ thuộc scene/prefab | toàn bộ dựng bằng code | Đổi nền tảng build không cần sửa asset. |

## Giấy phép tài nguyên (đều dùng thương mại được)
- Hình: Kenney *Tiny Dungeon* — CC0 (`Assets/ThirdParty/Kenney_tiny-dungeon-License.txt`).
- Âm thanh: Kenney *Impact Sounds*, *Interface Sounds*, *RPG Audio*, *Music Jingles* — CC0.
- Nhạc nền: tự sinh bằng code (`Core/Sfx.cs`, Synth) — không có bản quyền bên thứ ba.
- Font: `LegacyRuntime.ttf` có sẵn của Unity. Nên thay bằng font OFL hỗ trợ tiếng Việt (vd. *Be Vietnam Pro*, *Noto Sans*) trước khi phát hành để chủ động về giấy phép và nhận diện.
- Lá bùa (sprite 200), sprite procedural: tự vẽ trong code.

## Việc còn lại khi lên Steam
1. Thêm Steamworks.NET, viết `SteamPlatform : IPlatformServices` (SteamUserStats cho thành tựu — id trùng `Content.Achs`; Steam Cloud: chuyển lưu từ PlayerPrefs sang file trong `Application.persistentDataPath` rồi bật Auto-Cloud cho file đó).
2. Màn Cài đặt: đã có âm lượng nhạc/hiệu ứng, rung màn hình, số sát thương; còn thiếu cửa sổ/toàn màn hình và độ phân giải.
3. Steam Deck Verified: điều hướng menu bằng tay cầm đã có (`PadNav`); còn cần hiển thị biểu tượng nút tay cầm thay cho chữ phím, và kiểm tra chữ ở độ phân giải 1280×800.
4. Bản địa hoá tiếng Anh: chuỗi đang viết thẳng trong `Content.cs`, `Events.cs`, `UIRoot.cs` — gom vào bảng `Loc` (key → vi/en).
5. Trang store: capsule art, trailer (dùng `-autotest -shots` để lấy ảnh), mô tả.

## Việc còn lại khi lên Google Play
1. Build Android (IL2CPP, ARM64, target API mới nhất), khoá màn hình ngang.
2. Hiệu năng: giới hạn 60 FPS, kiểm tra số particle (pool 900) và số quái (150) trên máy tầm trung.
3. Safe area (tai thỏ): đặt HUD và TouchPad trong `Screen.safeArea`.
4. Chữ trên HUD/Shop cần to hơn trên màn hình nhỏ; chuỗi hướng dẫn nhắc phím (E, Space, R) nên đổi theo `Platform.Touch`.
5. `GooglePlayPlatform : IPlatformServices` (Play Games Services: thành tựu, Saved Games).
6. Mô hình kinh doanh: game trả phí một lần hoặc bản dùng thử + mở khoá — nếu cần IAP, dùng Unity IAP.
