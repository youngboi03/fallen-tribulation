# VẠN KIẾP TU TIÊN

Roguelite 2D đánh theo đợt, chủ đề tu tiên.
Lấy vòng lặp đợt quái + shop từ Brotato, build cộng hưởng từ Balatro, kỳ ngộ kiểu Slay the Spire, cảnh giới từ Tale of Immortal.
Một lượt chơi mất khoảng 25–30 phút.

## Chơi ngay
Chạy file **`Builds/Windows/VanKiepTuTien.exe`**.
Game mở ở dạng cửa sổ 1600×900. Có 3 ngôn ngữ **Tiếng Việt / English / 中文** (nút *Language* góc trên phải ở menu và màn tạm dừng). Bấm **F11** (hoặc Alt+Enter) để chuyển sang toàn màn hình.
Lần chơi đầu có **hướng dẫn** tự hiện đúng lúc; muốn xem lại thì bấm "Xem lại hướng dẫn" ở menu.

| Phím | Chức năng |
|---|---|
| WASD / phím mũi tên | Di chuyển (vũ khí tự nhắm và tự đánh) |
| Space / Shift / chuột phải | Lướt (không nhận sát thương trong lúc lướt, hồi chiêu 1.2s) |
| **E / J / chuột trái** | **Thần Thông**: chiêu riêng của từng nhân vật, tự tìm mục tiêu; mạnh lên theo boss (Tiến Giai) và thẻ Ngộ Đạo |
| Esc / P | Tạm dừng (trong menu: quay lại) |
| 1–4 | Chọn nâng cấp khi lên cấp, chọn phương án ở Kỳ Ngộ |
| R | Làm mới shop |
| F11 | Toàn màn hình |

**Tay cầm** (Xbox/XInput, Steam Deck): cần trái di chuyển, A lướt, X Thần Thông, Start tạm dừng. Trong menu: D-pad chọn nút, A bấm, B quay lại.
**Cảm ứng** (bản điện thoại; thử trên PC bằng `VanKiepTuTien.exe -touch`): joystick nổi ở nửa trái, nút Lướt / Thần Thông / Tạm dừng bên phải.

## Vòng chơi
1. **Đánh đợt** (20–60 giây): quái luôn có vòng đỏ cảnh báo trước khi xuất hiện. Diệt quái rơi **Linh Khí**, vừa là kinh nghiệm vừa là tiền (**Linh Thạch**). Diệt liên tục tạo **Liên Sát**, cứ mỗi 25 mạng được thưởng thêm linh thạch.
2. **Hết đợt**, lần lượt qua các bước:
   - Mở **Bảo Rương** (do tinh anh rơi). Sau boss là **Bảo Khố**: chọn 1 trong 3 pháp bảo Huyền/Tiên phẩm.
   - **Kỳ Ngộ** sau đợt 3, 7, 12, 17: sự kiện có lựa chọn, đánh đổi rủi ro lấy phần thưởng.
   - **Lên cấp**: chọn 1 trong 4 công pháp. Mỗi 10 cấp là **Đột Phá** cảnh giới (Luyện Khí → Trúc Cơ → Kim Đan → Nguyên Anh…) và nhận lựa chọn phẩm Tiên.
   - **Linh Thị** (shop): mua vũ khí và pháp bảo, khoá thẻ, làm mới, bán vũ khí. **2 vũ khí cùng loại cùng phẩm thì hợp nhất thành phẩm cao hơn** (Phàm → Linh → Huyền → Tiên).
   - **Lộ Tuyến**: chọn Đường Bằng (bình thường) hoặc Đường Hiểm (khó hơn nhưng thưởng nhiều hơn).
3. **Boss** ở đợt 5, 10, 15, 20, kèm **Thiên Kiếp** (sét đánh xuống, có vòng vàng cảnh báo). Sét đánh trúng cả quái nên có thể dụ quái vào. Boss 1 và 2 có **boss thay thế** (Huyết Bức Vương, Vạn Độc Chu Hậu), mỗi lượt ngẫu nhiên gặp một trong hai.
   - 4 vùng đất đổi màu và thời tiết theo đợt: Hoang Sơn → Hàn Băng Cốc (tuyết) → Huyết Sát Hải (tàn lửa) → Thiên Ma Giới.
   - Quái đặc biệt: **Bạo Hồn** chạy tới rồi tự nổ (giết trước thì nó nổ vào đồng bọn), **Bảo Rương Yêu** giả làm rương rồi lao tới, **Ảnh Sát** đánh dấu một chỗ cạnh bạn rồi dịch chuyển tới.
4. Thắng đợt 20 là **Phi Thăng**. Thắng hay thua đều nhận **Đạo Hạnh**, dùng để nâng cấp vĩnh viễn ở **Tàng Kinh Các**.

## Build
- Mỗi vũ khí thuộc một hoặc nhiều **Đạo**: Kiếm, Thể, Hỏa, Lôi, Phù, Độc.
- Có 2, 4 hoặc 6 vũ khí cùng Đạo thì kích hoạt **cộng hưởng**. Ví dụ: Hỏa 4 làm thiêu đốt gấp đôi, Độc 4 làm độc lây sang quái khác khi quái chết.
- **Pháp bảo Joker** tạo combo kiểu Balatro. Ví dụ:
  - Kim Thiềm: giữ càng nhiều linh thạch thì càng mạnh.
  - Phá Quân Kích: đánh mạnh hơn lên quái đang bị thiêu, trúng độc hoặc đóng băng.
  - Sát Lục Ấn: thưởng khi giữ được Liên Sát cao.
  - Ngũ Hành Bàn: ×1.5 sát thương khi có 5 Đạo khác nhau.

## Cày 100%
| Hạng mục | Số lượng |
|---|---|
| Nhân vật | 6 (5 nhân vật cần mở khoá): Kiếm Tu, Đan Tu, Thể Tu, Phù Tu, Ma Tu, Lôi Tu |
| Kiếp (độ khó) | 0–5 cho mỗi nhân vật. Thắng Kiếp k thì mở Kiếp k+1 |
| Thành tựu | 20 |
| Đồ Giám | ghi lại mọi vũ khí, pháp bảo, yêu ma đã gặp |
| Tàng Kinh Các | 6 nâng cấp × 5 cấp |

Tất cả gộp lại thành **% Hoàn thành** hiển thị ở menu chính.

## Mở project trong Unity
- Unity Hub → **Add** → chọn thư mục này. Phiên bản editor: **6000.3.27f1**.
- Không có scene hay prefab dựng bằng tay. Toàn bộ game được tạo bằng code lúc chạy (`Assets/Scripts`).
- Muốn chỉnh cân bằng thì sửa **`Assets/Scripts/Data/Content.cs`** (vũ khí, pháp bảo, quái, boss, nhân vật, cộng hưởng, thành tựu) và `Data/Events.cs` (Kỳ Ngộ).
- Build bản exe: menu **Vạn Kiếp → Build Windows**, hoặc chạy `bash tools/build_and_test.sh <số đợt> [-god]`. Lệnh này build rồi cho bot tự chơi, chụp ảnh vào `Logs/shots`.

### Cấu trúc code
| Thư mục | Nội dung |
|---|---|
| `Core/` | `Game` (khởi tạo, luồng chơi), `RunState` (chỉ số, shop, lên cấp), `Fx`, `Sfx` + `Synth` (nhạc tự tổng hợp), `Res` |
| `Play/` | `Player`, `Enemy`, `Weapon` + `Combat`, `Projectile` + `Orb`, `WaveDirector`, `Skills`, `Arena` |
| `UI/` | Toàn bộ màn hình uGUI dựng bằng code |
| `Data/` | Chỉ số, định nghĩa, nội dung game, sự kiện, lưu game |
| `Auto/` | Bot tự chơi, chạy bằng tham số `-autotest` (thêm `-smart`, `-skillbuild`, `-kiep k`, `-meta n`, `-char i`); kiểm thử kịch bản Thần Thông `-skilltest [-only <tiền tố>]`, kiểm thử giao diện `-uxtest` |

Tài liệu thiết kế: `docs/DESIGN.md`. Chuẩn bị phát hành Steam / Google Play: `docs/PORTING.md`.
Đo cân bằng nhiều bot song song: `bash tools/probe.sh "ten|-char 0 -kiep 2 -meta 2 -smart" ...` (kết quả trong `Logs/probe/`). Tỉ lệ sát thương Thần Thông theo dải đợt: `python -I tools/skill_share.py Logs/probe/*.log` (bot `-smart -skillbuild` là người chơi đầu tư vào chiêu). Kiểm thử Thần Thông: `bash tools/build_and_test.sh 1 -skilltest -only ""`.

## Bản quyền asset
- **Nhân vật, yêu ma và boss**: vẽ bằng **PixelLab** (https://www.pixellab.ai), nằm ở `Assets/Resources/PixelLab/`. Công cụ và prompt của từng hình được ghi ở `Assets/ThirdParty/PixelLab-Assets.md`.
- **Vũ khí, pháp bảo, nền, tường và âm thanh**: từ **Kenney.nl** (CC0): Tiny Dungeon, Impact Sounds, Interface Sounds, RPG Audio, Music Jingles. License nằm ở `Assets/ThirdParty/`.
- **Chữ Hán** (bản 中文): dùng font có sẵn trong hệ điều hành (Microsoft YaHei / Noto Sans CJK…), game không kèm file font nào.
Nhạc nền được tổng hợp bằng code (`Synth`).
