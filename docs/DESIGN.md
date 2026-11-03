# LỤC ĐẠO ĐỌA KIẾP (Fallen Tribulation) — Game Design Doc

> 2D arena roguelite (Unity 6000.3, Built-in RP, toàn bộ runtime dựng bằng code).
> Thiết kế dựa trên gu: Brotato (vòng lặp đợt quái + shop), Balatro / Slay the Spire (build cộng hưởng,
> số nhân lên, quyết định mỗi lượt), Dead Cells (đánh có lực, dash), Tale of Immortal / Nobody (tu tiên, cảnh giới),
> Elden Ring / Wukong (boss có pattern, telegraph rõ), Palworld / Dave the Diver (tiến trình meta, mở khoá).

## 1. Trụ cột (pillars)
1. **Mỗi lượt chơi 20–30 phút, mỗi đợt 20–60 giây** — chơi lúc rảnh, dễ "thêm 1 đợt nữa".
2. **Build bùng nổ** — vũ khí có *Đạo* (tag); gom đủ 2/4/6 kích hoạt cộng hưởng; pháp bảo đặc biệt nhân sát thương (kiểu Joker của Balatro).
3. **Cảm giác đánh** — hit-flash, knockback, hit-stop, rung màn, số sát thương bay, hạt nổ, âm thanh mỗi đòn.
4. **Cày 100%** — 5 nhân vật × 6 cấp Kiếp, thành tựu, Tàng Kinh Các (nâng cấp vĩnh viễn), % hoàn thành hiển thị ở menu.

## 2. Vòng lặp cốt lõi
```
Chọn nhân vật + Kiếp → [Đợt quái (auto-attack, bạn di chuyển + dash)]
  → nhặt Linh Khí (vừa là XP vừa là tiền "Linh Thạch")
  → hết giờ: mỗi cấp lên = chọn 1 trong 4 nâng cấp chỉ số (mỗi 10 cấp = ĐỘT PHÁ cảnh giới: lựa chọn huyền thoại)
  → Linh Thị (shop): 4 thẻ vũ khí/pháp bảo, reroll, khoá, bán, hợp nhất vũ khí
  → đợt tiếp theo … Boss ở đợt 5/10/15/20 kèm Thiên Kiếp (sét giáng có cảnh báo)
→ Thắng đợt 20 = Phi Thăng. Chết = Thân tử đạo tiêu. Cả hai đều nhận Đạo Hạnh (tiền meta).
```

## 3. Điều khiển
- WASD / phím mũi tên: di chuyển · Space/Shift: lướt (bất tử ngắn, hồi chiêu 1.2s) · Esc: tạm dừng
- Vũ khí tự nhắm kẻ gần nhất trong tầm (tối đa 6 vũ khí, quay quanh người).

## 4. Chỉ số
Sinh Lực, Hồi Phục, Hút Máu, Sát Thương%, Cận Chiến%, Pháp Thuật%, Tốc Đánh%, Bạo Kích%, ST Bạo Kích%,
Tầm Đánh%, Hộ Giáp, Né Tránh%, Thân Pháp%, Cơ Duyên, Thu Hoạch, Thu Linh%, Thần Lực% (sát thương Thần Thông), Tĩnh Tâm% (giảm hồi chiêu, tối thiểu 3 giây).
- Giáp: nhận × 1/(1+giáp/15) (giáp âm thì ngược lại). Né tối đa 60%.
- Hút máu = % cơ hội hồi 1 HP mỗi đòn trúng. Hồi phục: HP/s = 0.12 × Hồi Phục.

## 5. Vũ khí (14) & phẩm cấp
Phẩm: Phàm (trắng) → Linh (xanh) → Huyền (tím) → Tiên (cam). Sát thương × 1 / 1.7 / 2.8 / 4.5.
**Hợp nhất**: 2 vũ khí cùng loại cùng phẩm → 1 vũ khí phẩm cao hơn.

| Kiểu | Hành vi |
|---|---|
| Đâm (Thrust) | lao thẳng tới mục tiêu, xuyên theo đường |
| Chém/Đập (Swing) | lao tới điểm, nổ vùng, knockback mạnh |
| Pháp đạn (Bolt) | bắn đạn, có thể nổ vùng / thiêu đốt / trúng độc |
| Lôi (Chain) | sét nảy giữa các kẻ địch |
| Phi Kiếm (Fly) | kiếm bay đuổi mục tiêu, xuyên nhiều kẻ |

## 6. Đạo (tag) & cộng hưởng 2/4/6
- **Kiếm**: Bạo kích → ST bạo kích → Tốc đánh
- **Thể**: Sinh lực → Giáp + Cận chiến → Sinh lực lớn
- **Hỏa**: Pháp thuật → thiêu đốt ×2 → Pháp thuật lớn
- **Lôi**: +1 nảy → Tốc đánh → +2 nảy & Bạo kích
- **Phù**: Tầm đánh → xuyên +1 & Cơ duyên → Pháp thuật
- **Độc**: độc +50% → độc lây khi chết → độc ×2

## 7. Pháp bảo (28) — 4 phẩm
- Mỗi lượt chơi giữ tối đa **6 / 4 / 3 / 2** bản của cùng một pháp bảo Phàm / Linh / Huyền / Tiên (`Content.StackCap`; món có giới hạn riêng giữ giới hạn đó). Món đã đạt giới hạn không còn hiện ở shop, rương, Kỳ Ngộ.
- Rương tinh anh: 30% (Tinh Anh Tụ Tập 60%), chia cho (1 + số rương tinh anh đã rơi trong đợt). Boss luôn rơi rương.
Chỉ số thuần (Phàm/Linh) và hiệu ứng đặc biệt kiểu Joker (Huyền/Tiên): Bát Quái Kính (mỗi đòn thứ 8 chắc chắn bạo),
Tụ Bảo Bồn (lãi 10% linh thạch cuối đợt — kiểu interest Balatro), Lôi Linh Châu (bạo kích gọi sét), Hỏa Long Châu
(quái chết phát nổ), Huyết Kiếm Tâm (mỗi 25 mạng +1% ST vĩnh viễn trong lượt), Cuồng Chiến Ý (thiếu máu càng mạnh),
Song Tu Bí Kíp (+1 ô vũ khí), Ngũ Hành Bàn (≥5 Đạo khác nhau → ×1.5 ST), Vạn Kiếm Quy Tông (+12% ST mỗi vũ khí Kiếm),
Bất Tử Phù (hồi sinh 1 lần)…

## 8. Kẻ địch
10 loại thường (đuổi, bầy dơi lượn sóng, lao húc có báo trước, bắn đạn giữ khoảng cách, tách đôi khi chết, tinh anh)
+ 4 boss (đợt 5/10/15/20), mỗi boss chu kỳ: húc (vạch cảnh báo) / vòng đạn / triệu hồi; dưới 50% HP nhanh hơn.
Quái xuất hiện sau 0.8s cảnh báo (vòng đỏ) — luôn công bằng.

## 9. Cảnh giới (theo cấp)
Luyện Khí (1–9) → Trúc Cơ (10) → Kim Đan (20) → Nguyên Anh (30) → Hóa Thần (40) → Luyện Hư (50).
Mỗi lần lên cảnh giới: hồi đầy máu, lựa chọn nâng cấp phẩm Tiên (2 chỉ số).

## 10. Meta
- **Đạo Hạnh**: = đợt đạt × 3 + mạng giết/40 + thắng 60, × (1 + 0.5×Kiếp). Thành tựu +20.
- **Tàng Kinh Các**: 6 nâng cấp vĩnh viễn × 5 cấp.
- **Nhân vật**: Kiếm Tu (mặc định), Đan Tu (đạt đợt 5), Thể Tu (tổng 1500 mạng), Phù Tu (đạt Kim Đan), Ma Tu (thắng 1 lần).
- **Kiếp 0–5**: thắng Kiếp k mở Kiếp k+1. Quái +25% máu, +20% ST mỗi Kiếp; Kiếp ≥ 4 có Thiên Kiếp mọi đợt.
- **Hoàn thành %**: thành tựu + ma trận nhân vật×Kiếp + Tàng Kinh Các.

## 11. Kiến trúc kỹ thuật
- Không scene/prefab thủ công: `Game` tự khởi tạo qua `RuntimeInitializeOnLoadMethod`, dựng camera, sân, UI (uGUI) bằng code.
- `Game.Dt` = delta có hit-stop/pause; mọi gameplay dùng nó.
- Va chạm tự viết (khoảng cách tròn), không dùng physics → ổn định, dễ cân bằng.
- `Content.cs` chứa toàn bộ dữ liệu cân bằng ở một chỗ.
- `-autotest` : bot tự chơi, chụp ảnh màn hình, log → dùng để kiểm thử build.
- Save: JSON trong PlayerPrefs.

## 12. Hệ thống bổ sung (sau review 16:05)
- **Thần Thông** (làm lại 27/10): mỗi nhân vật có 1 chiêu chủ động (phím E hoặc chuột trái), tự tìm mục tiêu.
  - Sức mạnh gốc `SP(w) = r(w) × máu trung bình quái thường ở đợt w`, `r` giảm từ 1.5 (đợt 1) xuống 0.15 (đợt 20): đầu game mạnh, cuối game chỉ mạnh nếu đầu tư. Boss nhận 50%.
  - **Tiến Giai**: mỗi boss hạ gục nâng chiêu lên giai II → III → IV (đổi dạng, thêm khống chế/phòng thủ, hình ảnh lớn dần). Nhãn thanh chiêu ghi "[E] <tên dạng> · Giai N".
  - **Uy Áp Cảnh Giới**: đột phá cấp 10 / 20 / 30 thưởng chỉ số vĩnh viễn, hào quang theo cảnh giới, Nguyên Anh hộ chủ đỡ 1 đòn chí mạng mỗi đợt.
  - **Ngộ Đạo**: lúc đột phá chọn 1 trong 4 thẻ (2 thẻ chỉ số phẩm Tiên, 1 thẻ Thần Thông đặc trưng của nhân vật, 1 thẻ chung); 18 thẻ đặc trưng + 8 thẻ chung.
  - **Đầu tư**: chỉ số Thần Lực / Tĩnh Tâm khi lên cấp (trọng số 0.5) và 6 pháp bảo Thần Thông; shop ưu tiên pháp bảo Thần Thông khi build đã nghiêng về chiêu.
  - Cân bằng: `docs/balance/2026-10-27-than-thong.md`.
- **Liên Sát**: combo diệt quái liên tục, cứ mỗi 25 mạng thưởng linh thạch.
- **Bảo Rương**: tinh anh và boss rơi rương; giữa các đợt mở rương, chọn nhận hoặc bán.
- **Kỳ Ngộ**: 9 sự kiện lựa chọn kiểu Slay the Spire, xuất hiện sau đợt 3, 7, 12, 17.
- **Lộ Tuyến**: trước mỗi đợt thường, chọn Đường Bằng hoặc Đường Hiểm (Ma Triều / Tinh Anh Tụ Tập / Huyết Nguyệt).
- **Vùng**: Hoang Sơn (1–5) → Hàn Băng Cốc (6–10) → Huyết Sát Hải (11–15) → Thiên Ma Giới (16–20).
- **Pháp bảo Joker**: tăng sức mạnh theo đợt, theo linh thạch đang giữ, theo combo và theo trạng thái của kẻ địch.

### 12b. Bổ sung tối 26/10 (19:00–22:00)
- **Hướng dẫn theo ngữ cảnh**: gợi ý hiện đúng lúc trong lượt đầu (di chuyển, lướt, Thần Thông, linh khí, lên cấp, shop, Lộ Tuyến, rương, Kỳ Ngộ, boss), mỗi gợi ý một lần. Gợi ý của màn shop không đè lên trận đánh.
- **Bảo Khố sau boss**: chọn 1 trong 3 pháp bảo Huyền/Tiên (từ boss 2 trở đi chắc chắn có 1 món Tiên).
- **Boss thay thế**: Huyết Bức Vương (slot 1), Vạn Độc Chu Hậu (slot 2), ngẫu nhiên 50% mỗi lượt. Boss 3–4 có thêm đòn bão đạn xoắn ốc.
- **Quái mới**: Bạo Hồn (tự nổ, giết trước thì nổ vào đồng bọn), Bảo Rương Yêu (mimic), Ảnh Sát (dịch chuyển có báo trước).
- **Nhân vật thứ 6 — Lôi Tu**: +20% Tốc Đánh, Thần Thông *Thiên Lôi Dẫn* (8 tia sét, làm choáng 1s). Mở khi đạt đợt 10.
- **Vũ khí**: 24 cái, mỗi Đạo có ít nhất 3 (Kiếm 8, Thể 8, Phù 6, Hỏa 5, Lôi 5, Độc 3). Pháp bảo: 36. Quái: 13 thường + tiểu quái + 6 boss.
- **Kỳ Ngộ**: 16 sự kiện. Phần được hiện màu xanh, cái giá hiện màu đỏ.
- **Hình ảnh**: viền sáng cho tinh anh (đỏ) và boss (tím), đột phá có cột sáng, thời tiết theo vùng, menu có nhân vật ngồi thiền và yêu ma đi lại, màn kết thúc hiện build.
- **Cân bằng**: độ khó Kiếp tăng dần theo đợt (`ramp = 0.2 + đợt/12`), sát thương quái có thêm hệ số bậc 2 và máu quái thêm hệ số bậc 3 cho cuối game. Lộ Tuyến không áp dụng ở đợt boss.
- **Sẵn sàng phát hành**: input / lưu game / thành tựu đi qua lớp nền tảng, có tay cầm và cảm ứng. Xem `docs/PORTING.md`.
- **Ngôn ngữ**: Tiếng Việt / English / 中文, đổi ngay trong game (nút *Language*). Game viết bằng tiếng Việt; `Loc.Tr` dịch theo cụm từ tại điểm hiển thị nên thêm nội dung mới chỉ cần thêm dòng vào `tools/loc/*.txt`.
- **Hình ảnh PixelLab**: 6 nhân vật, 13 yêu ma (+ dạng ngụy trang của Bảo Rương Yêu), 6 boss, nền Wang-tile riêng cho 4 vùng (rải mảng địa hình bằng nhiễu Perlin), tranh phong cảnh cho menu. Vũ khí/pháp bảo vẫn dùng Kenney.
- **Cân bằng (bot "smart", có meta)**: Kiếp tăng dần tới 1.5× ở nửa sau; từ đợt 12 quái thường +6% máu và +3.5% sát thương mỗi đợt; Tinh Anh Tụ Tập tối đa 28% tinh anh; Bạo Hồn nổ ×1.15 sát thương, châm ngòi 0.9s; Ma Tu khởi đầu với Huyết Nguyệt Đao; Đan Tu +20% Pháp Thuật, +5 Hồi Phục, +5 Sinh Lực, Thần Thông hồi 25% máu (9s); Lôi Tu +3 Sinh Lực.

## 13. Asset
Kenney (CC0): Tiny Dungeon (sprite), Impact/Interface/RPG Audio, Music Jingles. Nhạc nền tự tổng hợp bằng code
(ngũ cung — A minor pentatonic, tiếng gảy kiểu đàn tranh).
