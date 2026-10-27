# Cân bằng Thần Thông — 27/10 (Task 13, phần B)

Mục tiêu lấy từ the Than Thong design §1.
Mỗi lượt: `tools/probe.sh`, 20 đợt, meta 2, tốc độ 3, một lượt mỗi nhân vật (c0 Kiếm · c1 Đan · c2 Thể · c3 Phù · c4 Ma · c5 Lôi).
Số đo: `python -I tools/skill_share.py <log>` — tỉ lệ sát thương Thần Thông trên tổng sát thương, theo dải đợt.
`s_` = bot thường (`-smart`), `b_` = bot chuyên Thần Thông (`-smart -skillbuild`). Log giữ ở `Logs/probe_r*/` (không commit).

> Lưu ý độ nhiễu: mỗi ô là **một** lượt chạy. Cùng một cấu hình, Kiếm Tu bot thường dao động 22–44% giữa hai lần chạy, nên chỉ đọc xu hướng, không đọc từng số.

## Thay đổi đo lường (trước vòng 1)
1. **Sát thương thực.** `dmgDealt` cộng cả phần thừa vượt máu còn lại; một đòn chiêu lớn lên quái thường thổi phồng tỉ lệ. Thêm `RunState.dmgEff` / `skillEff` (giới hạn bằng máu còn lại), `Skills.InHit` đánh dấu đòn của chiêu. Log ghi `skill` (thực) và `skillRaw` (cũ).
2. **Bot bấm chiêu như người.** Bot cũ chỉ thi triển khi ≥ 6 quái trong 4 ô, bot thả diều nên nhiều đợt 0 lần. Bot mới: tinh anh/boss trong tầm + 2 ô, hoặc ≥ 3 quái trong tầm chiêu của nhân vật (`AutoTest.SkillReach`).
3. **Bot thường không lấy chỉ số chiêu** khi lựa chọn chỉ có Thần Lực/Tĩnh Tâm (lỗi phát hiện ở vòng 2: điểm phẩm làm thẻ "Thần Lực phẩm Tiên" thắng thẻ Phàm). Bot thường vẫn nhận Thần Lực từ Uy Áp và thẻ đột phá 2 chỉ số bắt buộc — đó là phần tự động spec cho phép.

## Vòng 0 — mốc (giá trị theo spec)
`SkillR` 1.5 → 0.5 · Kiếm hệ số 1 / 0.6 / 0.3 / 4 · Thần Lực lên cấp 8/12/18/25.

| log | đợt 1–5 | 9–12 | 18–20 | từ 10 | kết quả |
|---|---|---|---|---|---|
| s Kiếm | 36.5 | 59.0 | 34.3 | 44.3 | thắng |
| s Đan | 6.9 | 17.1 | 15.2 | 19.5 | thắng |
| s Thể | 6.7 | 9.3 | 19.9 | 16.7 | thắng |
| s Phù | 4.5 | 14.3 | 20.3 | 16.2 | thắng |
| s Ma | 0.0 | 1.8 | 4.8 | 3.2 | thắng |
| s Lôi | 11.5 | 13.7 | 25.2 | 20.9 | thắng |
| b Kiếm | 47.9 | 76.3 | 66.7 | 71.8 | thắng |
| b Đan | 6.6 | 37.2 | 45.8 | 40.9 | thắng |
| b Thể | 11.2 | 10.7 | 15.9 | 16.9 | thắng |
| b Phù | 5.5 | 28.0 | 31.0 | 26.0 | thắng |
| b Ma | 0.0 | 19.8 | 20.2 | 22.6 | thắng |
| b Lôi | 13.3 | 39.5 | 32.4 | 34.5 | thắng |

Kết luận: sai cùng một hướng — đầu game thấp, cuối game cao (các giai II–IV cộng nhiều đòn, quái cuối game đông). Kiếm vượt mọi nhân vật. Đầu tư chỉ nhân tỉ lệ chiêu/vũ khí ×2–3.

## Vòng 1
Đổi: `SkillR` 1.5 → **0.2** · Kiếm ×0.5 (0.5 / 0.3 / 0.15 / 2.5) · Thần Lực lên cấp **12/18/27/38**.

| log | 1–5 | 9–12 | 18–20 | từ 10 | kết quả |
|---|---|---|---|---|---|
| s Kiếm / Đan / Thể / Phù / Ma / Lôi | 33 / 11 / 7 / 7 / 0 / 11 | 19 / 9 / 9 / 24 / 1 / 13 | 24 / 17 / 7 / 13 / 6 / 20 | 22 / 15 / 12 / 18 / 2 / 14 | 6 thắng |
| b Kiếm / Đan / Thể / Phù / Ma / Lôi | 25 / 6 / 11 / 5 / 0 / 11 | 47 / 21 / 10 / 13 / 2 / 14 | 54 / 33 / 19 / 36 / 2 / 48 | 49 / 27 / 17 / 25 / 2 / 29 | 6 thắng |

Phát hiện: bot chuyên có **89–163 pháp bảo** và Thần Lực 478–1100% ở đợt 19 (mua hàng chục Ngưng Thần Hương; Tinh Anh Tụ Tập cho 10–12 rương một đợt). Kinh tế pháp bảo chi phối mọi phép đo → sửa trước khi chỉnh tiếp (user duyệt, tham khảo Brotato / Vampire Survivors):
- **Rương tinh anh giảm dần trong đợt:** tỉ lệ = gốc / (1 + rương tinh anh đã rơi trong đợt); gốc 30%, Tinh Anh Tụ Tập 60% (trước: 100%). Rương boss giữ nguyên. Kiểm thử: 40 tinh anh trên Tinh Anh Tụ Tập → 8 rương (trước: 40).
- **Giới hạn chồng theo phẩm:** Phàm 6 · Linh 4 · Huyền 3 · Tiên 2 (`Content.StackCap`, `ItemDef.Cap`); món đạt giới hạn không còn hiện ở shop/rương/Kỳ Ngộ.

## Mốc sau khi sửa kinh tế (giữ thay đổi vòng 1)

| log | 1–5 | 9–12 | 18–20 | từ 10 | kết quả |
|---|---|---|---|---|---|
| s Kiếm / Đan / Thể / Phù / Ma / Lôi | 40 / 7 / 10 / 3 / 0 / 10 | 44 / 17 / 10 / 14 / 3 / 13 | 40 / 10 / 6 / 11 / 11 / 14 | 45 / 11 / 10 / 12 / 6 / 12 | 6 thắng |
| b Kiếm / Đan / Thể / Phù / Ma / Lôi | 30 / 11 / 8 / 9 / 0 / 10 | 57 / 31 / 11 / 16 / 3 / 38 | 53 / 20 / 19 / 23 / 4 / 51 | 61 / 25 / 15 / 18 / 4 / 46 | 6 thắng |

Pháp bảo ở đợt 19: bot thường 55–96, bot chuyên 63–104 (trước 89–163). Phần còn lại đến từ shop — linh thạch cuối game vẫn dư (xem "Còn mở").

## Vòng 2
Đổi: `SkillR` đợt 20 **0.15** · Kiếm ×0.5 nữa (**0.25 / 0.15 / 0.08 / 1.25**) · Thần Lực lên cấp **15/23/34/48** · Ngưng Thần Hương +15 · Thần Thông Ngọc Giản +30. Bot thường đã sửa lỗi lấy Thần Lực.

| log | 1–5 | 9–12 | 18–20 | từ 10 | kết quả |
|---|---|---|---|---|---|
| s Kiếm | 26.8 | 20.7 | 10.8 | 14.8 | thắng |
| s Đan | 7.6 | 14.5 | 8.9 | 11.3 | thắng |
| s Thể | 9.4 | 10.3 | 9.5 | 12.9 | thắng |
| s Phù | 4.5 | 8.9 | 5.7 | 6.6 | thắng |
| s Ma | 0.0 | 0.4 | 2.4 | 1.4 | thắng |
| s Lôi | 15.2 | 27.5 | 23.2 | 21.9 | thắng |
| b Kiếm | 23.2 | 45.0 | 27.8 | 39.2 | thắng |
| b Đan | 11.2 | 26.9 | 26.0 | 29.7 | thắng |
| b Thể | 5.6 | 9.6 | 15.9 | 14.0 | thắng |
| b Phù | 4.3 | 21.4 | 18.8 | 27.6 | thắng |
| b Ma | 0.0 | 9.1 | 29.3 | 27.2 | thắng |
| b Lôi | 12.0 | 54.4 | 54.1 | 58.3 | thắng |

## Vòng 3
Đổi: Lôi Tu ×~0.6 (LoiHit 2.2 → 1.4, nảy 0.8 → 0.5, vùng sấm 0.8 → 0.5, cột sét 5 → 3). Chạy thêm Kiếp 3 và Kiếp 5 cho cả hai bot để so tỉ lệ thắng.

Kiếp 0, Lôi Tu sau khi giảm: bot thường 16.5 / 16.6 / 12.2 / 13.3 (trước 15 / 28 / 23 / 22), bot chuyên 11.8 / 37.7 / 33.2 / 33.7 (trước 12 / 54 / 54 / 58). Cả hai thắng.

Kiếp 3 và 5 (cùng bản code, meta 2). Ô ghi đợt chết hoặc "thắng"; trong ngoặc là tỉ lệ chiêu từ đợt 10 nếu có.

| | Kiếm | Đan | Thể | Phù | Ma | Lôi | thắng |
|---|---|---|---|---|---|---|---|
| s Kiếp 3 | 10 | 9 | 11 | thắng (17%) | 10* | 9 | 1/6 |
| b Kiếp 3 | 9 | 9 | 10 | 5 | 5* | thắng (35%) | 1/6 |
| s Kiếp 5 | 3 | 9 | 11 | 10 | 10* | 9 | 0/6 |
| b Kiếp 5 | 5 | 10 | 10 | 10 | 5* | 10 | 0/6 |
| **trước Task 13**, s Kiếp 3 | thắng | thắng | 12 | 12 | 10 | thắng | 3/6 |

\* Ma chạy lại sau khi sửa bot: hiến tế tốn 15% máu, bot mới bấm chiêu nhiều nên tự rút cạn máu (trước khi sửa Ma chết ở đợt 5 cả hai Kiếp). Bot giờ chỉ hiến tế khi còn trên 50% máu.

Dòng "trước Task 13" chạy trên bản code commit 9e54f7a (bot cũ, kinh tế cũ, hệ số cũ). Ba lượt thắng ở đó có 123–153 pháp bảo ở cuối lượt.

## Kết quả so với mục tiêu
| Tiêu chí (spec §1) | Kết quả | |
|---|---|---|
| Bot thường: 20–35% đợt 1–5 | Kiếm 27%; Đan/Thể/Phù/Lôi 5–17%; Ma 0% | **trượt** (trừ Kiếm) — xem "Còn mở" |
| Bot thường: 8–15% đợt 9–12 | Đan 15, Thể 10, Phù 9, Lôi 17, Kiếm 21, Ma 0 | đạt 3/6, Kiếm và Lôi hơi cao |
| Bot thường: dưới 8% đợt 18–20 | Phù 6, Ma 2; Đan 9, Thể 10, Kiếm 11, Lôi 12 | đạt 2/6, còn lại cao 1–4 điểm |
| Bot chuyên: ≥ 35% từ đợt 10 | Kiếm 39, Lôi 34–35; Đan 30, Phù 28, Ma 27, Thể 14 | đạt 2/6 (Lôi sát ngưỡng) |
| Bot chuyên thắng không hơn bot thường quá 1/3 | Kiếp 0: 6 = 6 · Kiếp 3: 1 = 1 · Kiếp 5: 0 = 0 | **đạt** |
| Bot thường không thắng nhiều hơn bảng cũ; Đan/Ma không chết sớm hơn | thắng ít hơn (Kiếp 3: 1/6 so với 3/6). Ma chết đợt 10 = trước. **Đan chết đợt 9, trước thắng** | **trượt** về phía khó hơn |
| 0 exception | 0 trên mọi lượt chạy | **đạt** |

Đã hết 3 vòng theo plan. Hai việc cần quyết định (không phải chỉnh hệ số chiêu):
1. **Độ khó Kiếp** được cân trên nền rương dư thừa; siết kinh tế làm Kiếp 3–5 khó hẳn. Chỉnh lại đường độ khó Kiếp thuộc phần C (boss / Kiếp).
2. **Đầu game của chiêu** cần đổi thiết kế giai I (tầm / hồi chiêu đầu game), không chỉnh được bằng hệ số.

## Còn mở
- **Đầu game (đợt 1–5)** của Đan / Thể / Phù vẫn 5–10% (mục tiêu 20–35%). Không phải do sát thương — một đòn đã giết gọn quái thường — mà do ít quái lọt vào tầm chiêu và chỉ 1–3 lần thi triển mỗi đợt. Muốn đạt cần đổi thiết kế giai I (hồi chiêu ngắn hơn đầu game, hoặc tầm rộng hơn), không chỉ hệ số.
- **Thể Tu** gần như không đổi theo đầu tư (bot thường ≈ bot chuyên ≈ 13–15%): chiêu và thẻ của Thể thiên về phòng thủ (bất tử, giáp, gai). Coi là đặc tính nhân vật.
- **Ma Tu** giai I không gây sát thương (hiến tế tăng Hút Máu); tỉ lệ chiêu phụ thuộc thẻ bốc được (2% → 27% giữa hai lượt chạy).
- **Linh thạch dư cuối game** (500–900 mỗi đợt) vẫn đổ ra shop; để phần C/D (giá phải trả cho pháp bảo, rương chọn 1 trong 2).
