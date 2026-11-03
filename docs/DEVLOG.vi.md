# Lục Đạo Đọa Kiếp: nhật ký phát triển

Lục Đạo Đọa Kiếp là một roguelite 2D sinh tồn theo đợt, lấy chủ đề tu tiên, làm bằng Unity 6. Lúc làm, tôi gọi nó là "Vạn Kiếp Tu Tiên". Về sau tôi đổi sang tên hiện tại, vì con số 6 chạy xuyên suốt game: sáu Đạo, sáu nhân vật, sáu boss, sáu Kiếp. Hiện tại game có 6 nhân vật, 6 boss, 6 cấp độ khó gọi là Kiếp (Kiếp 0 tới Kiếp 5), 6 Đạo để xây build, và chơi được trọn vẹn từ đợt đầu tới lúc Phi Thăng. Tôi đang tạm dừng nó, và đây là lúc nhìn lại: cái gì ổn, cái gì làm tôi đau đầu, và cái gì còn dang dở.

## Khởi đầu và cảm hứng

Tôi bắt đầu bằng cách liệt kê những game mình từng chơi tới 100%. Chúng gom thành bốn nhóm: roguelike xây build (Balatro, Slay the Spire, Brotato), hành động cần kỹ năng (Dead Cells, Elden Ring, Wukong), tu tiên (Tale of Immortal, Nobody), và kiểu sưu tầm, cày phần trăm hoàn thành.

Tôi có cân nhắc làm một deckbuilder kiểu Balatro, nhưng nó thiếu hẳn phần hành động mà tôi thích. Một platformer kiểu Dead Cells thì cần hoạt ảnh theo khung và tạo màn chơi, quá sức so với những gì tôi làm được cho chỉn chu. Cuối cùng tôi chọn công thức: vòng lặp đợt quái và cửa hàng của Brotato, các pháp bảo cộng hưởng kiểu Joker của Balatro, sự kiện rủi ro kiểu Slay the Spire, và hệ cảnh giới của Tale of Immortal làm khung tiến trình.

Một quyết định kỹ thuật từ đầu định hình cả dự án: không có scene hay prefab dựng tay. Toàn bộ game được dựng bằng code lúc chạy trong một scene trống, va chạm tự viết bằng khoảng cách hình tròn thay cho physics, và mọi số liệu cân bằng nằm trong một file dữ liệu. Nhờ vậy tôi có thể build, chạy bot và so sánh mà không phải lo asset nằm sai chỗ trong Editor.

## Vòng lặp cốt lõi và hệ thống build

Một lượt chơi dài khoảng 25 đến 30 phút, mỗi đợt từ 20 đến 60 giây. Vũ khí tự đánh; người chơi di chuyển, lướt và tung Thần Thông. Quái giết rơi Linh Khí, vừa là kinh nghiệm vừa là tiền. Giữa hai đợt là phần tôi thích nhất: mở rương, gặp Kỳ Ngộ, lên cấp và chọn 1 trong 4 công pháp, mua sắm ở Linh Thị, rồi chọn Đường Bằng hoặc Đường Hiểm cho đợt sau. Boss xuất hiện mỗi 5 đợt, thắng đợt 20 là Phi Thăng.

Build xoay quanh 6 Đạo: Kiếm, Thể, Hỏa, Lôi, Phù, Độc. Gom 2, 4 hoặc 6 vũ khí cùng Đạo thì mở cộng hưởng, và vũ khí giống nhau gộp thành phẩm cao hơn. Cuối cùng game có 24 vũ khí, 36 pháp bảo và 16 Kỳ Ngộ. Phần "Joker" là chỗ build bùng nổ: một món lớn dần theo số Linh Thạch đang giữ, một món nhân sát thương khi chạy đủ năm Đạo khác nhau. Cảnh giới đi theo cấp, từ Luyện Khí lên Trúc Cơ, Kim Đan, Nguyên Anh, và mỗi lần đột phá là một lựa chọn lớn.

Bên ngoài lượt chơi có Đạo Hạnh để mua nâng cấp vĩnh viễn ở Tàng Kinh Các, 20 thành tựu, Đồ Giám (bách khoa trong game), và một con số phần trăm hoàn thành ở menu. Chính tôi là kiểu người chơi bị con số đó kéo lại.

## Thần Thông

Phiên bản đầu chỉ có di chuyển và lướt, thiếu hẳn cảm giác chủ động. Vì vậy mỗi nhân vật có một Thần Thông riêng, bấm E hoặc chuột trái, tự tìm mục tiêu: Kiếm Tu tung mưa phi kiếm, Đan Tu nổ lửa và hồi máu, Thể Tu bất tử rồi dậm sóng chấn, Phù Tu định thân cả vùng, Ma Tu hiến máu đổi sức mạnh, Lôi Tu gọi sét làm choáng.

Công thức đầu tăng theo cấp và chỉ số, và vỡ đúng như dự đoán: đầu game yếu, cuối game một chiêu xoá sân. Giờ sức mạnh gốc tính theo máu của quái ở đợt hiện tại, với hệ số giảm dần qua 20 đợt, nên chiêu mạnh ở đầu game và chỉ còn mạnh ở cuối nếu người chơi đầu tư. Mỗi boss hạ gục nâng chiêu lên một Giai, từ I tới IV, đổi dạng và lớn dần về hình ảnh. Đột phá cảnh giới mở màn Ngộ Đạo: chọn 1 trong 4 thẻ, trong đó có thẻ đặc trưng của nhân vật. Kết quả chưa đạt hết mục tiêu; tôi sẽ nói ở phần dưới.

## Boss và Kiếp

Đây là phần tôi tự hào nhất. Lúc đầu cả 6 boss dùng chung một hàm: lao, bắn vòng, triệu hồi, xoắn ốc. "Phase 2" chỉ là nổi điên ở 50% máu. Kiếp cao chỉ nhân chỉ số, nên boss nào cũng như nhau.

Bản làm lại cho mỗi boss một bộ chiêu gốc và hai bộ mở rộng, mỗi bộ có tuyệt chiêu. Bộ chiêu cộng dồn: học boss ở Kiếp 0, lên Kiếp cao thì bị "nâng bài". Ở Kiếp 3 trở lên, boss xuất hiện kèm cutscene Kiếp Biến (boss đột biến). Ở Kiếp 5, khi boss còn nửa máu, cutscene Ma Thân mở thêm bộ chiêu thứ hai. Mọi boss có hai phase, phase II có chiêu riêng và một bản nhạc cao trào. Tuyệt chiêu kích hoạt theo mốc máu, luôn báo trước ít nhất một giây và luôn để lại chỗ đứng an toàn. Lần đầu gặp một boss, một thẻ giới thiệu kiểu Borderlands trượt vào với chân dung và cái tên thật to.

Nguyên tắc xuyên suốt: độ khó Kiếp cao phải đến từ bộ chiêu nhiều hơn từ chỉ số. Tôi cũng làm lại hình ảnh boss theo chủ đề phương Đông: một sơn yêu một mắt, một tướng tuồng cắm bốn cờ, một nhện tinh, một ma đế mặc long bào.

## Hình ảnh và âm thanh

Tôi nói thẳng: nhân vật, quái, boss, icon, chân dung và nền trong game được tạo bằng PixelLab, một công cụ AI tạo pixel art, rồi mới được sửa và làm sạch. Không có hình nào trong số đó là vẽ tay từ đầu. Công cụ và prompt của từng hình đều được ghi lại.

Ảnh AI ra lần đầu hiếm khi dùng ngay được: kích thước lớn, nhiều màu, đổ bóng mềm, trong khi game cần sprite nhỏ, khối màu to. Tôi dựng một quy trình để nắn lại: hạ kích thước, lượng tử về khoảng 20 màu không dither, thêm viền đen một pixel. Sprite nhân vật được làm lại ở 48 pixel. Chỗ nào hỏng, như đôi chân của Kiếm Tu, tôi vẽ lại từng vùng bằng inpaint. Sau đó mọi sprite đi qua một script làm sạch pixel chạy Aseprite ở chế độ batch và một bước kiểm tra asset. Dải khung đi bộ được sinh bằng script từ sprite gốc, rồi một công cụ khoá màu theo sprite gốc và căn chân về đáy khung. Chân dung boss ban đầu quá mịn và quá chi tiết so với phần còn lại, nên tôi chuyển sang kiểu đơn giản hơn.

Phần còn lại đến từ Kenney (CC0): bộ tile Tiny Dungeon, sprite vũ khí và pháp bảo ban đầu, và các gói âm thanh va chạm, giao diện, RPG và nhạc ngắn. Nhạc nền thì tôi tổng hợp hoàn toàn bằng code, trên âm giai ngũ cung, với tiếng gảy gợi nhớ đàn tranh. Bản nhạc cao trào cho phase II của boss là thứ tôi thích nghe lại nhất.

## Ba ngôn ngữ

Game có tiếng Việt, tiếng Anh và tiếng Trung, đổi ngay trong game, và lần đầu mở sẽ theo ngôn ngữ hệ điều hành. Tôi viết game bằng tiếng Việt, rồi dịch theo cụm từ ngay tại điểm hiển thị, dựa trên các bảng văn bản ba cột. Một công cụ kiểm tra báo chuỗi nào còn sót. Giới hạn: bản dịch chưa được người bản ngữ đọc lại, và bản tiếng Trung đang dựa vào font của hệ điều hành chứ chưa kèm font riêng.

## Cân bằng bằng bot và kiểm thử kịch bản

Tôi không có đội test, nên tôi làm một con bot tự chơi. Nó chơi hết lượt, chụp ảnh, ghi log; chế độ "smart" biết mua sắm, một chế độ khác dồn hết vào Thần Thông. Sau này nó còn học né vùng nguy hiểm của boss (một bài thử: số lần trúng đòn giảm từ 34 xuống 7). Bộ kiểm thử kịch bản thì dựng sẵn tình huống rồi kiểm tra kết quả. Lần chạy đầy đủ gần nhất có 1673 trên 1673 kiểm tra đạt.

Bot dạy tôi những điều tôi không tự thấy được. Mô phỏng kinh tế cho thấy khoảng 43% đồ cuối game đến từ rương tinh anh. Có lúc bot chuyên Thần Thông ôm từ 89 tới 163 pháp bảo ở đợt 19, và chuyện đó làm sai lệch mọi phép đo khác. Tôi phải sửa kinh tế trước khi chỉnh chiêu: tỉ lệ rơi rương tinh anh giảm dần trong mỗi đợt, và mỗi pháp bảo có giới hạn số bản theo phẩm. Ngay cả cách đo cũng phải sửa: sát thương thừa thổi phồng tỉ lệ của chiêu, và bot cũ chờ đủ 6 quái quanh mình mới bấm chiêu, nên nhiều đợt không bấm lần nào.

Bài học đau nhất là tác dụng dây chuyền. Siết kinh tế xong, Kiếp 3 từ 3 trên 6 lượt thắng rơi xuống còn 1 trên 6, và Kiếp 5 thì không thắng nổi lượt nào. Hoá ra độ khó Kiếp đã được cân trên nền rương dư thừa. Sau hai vòng chỉnh, bot thường thắng 6/6 ở Kiếp 0, 3/6 ở Kiếp 3 và 2/6 ở Kiếp 5, đúng mục tiêu đã đặt. Có điều mỗi ô chỉ là một lượt chạy, nên số liệu còn nhiễu.

## Giao diện

Mọi màn hình đều là uGUI dựng bằng code, hỗ trợ tay cầm và cảm ứng. Bản đầu đủ chức năng nhưng nhìn lạnh và rời rạc. Đợt làm lại cuối cùng đổi sang các khung bảng cắt riêng thay cho đường viền, một bảng màu mực ấm, font Baloo 2 tròn trịa, và icon giữ đúng tỉ lệ. Gợi ý hướng dẫn giờ chỉ hiện trong lượt chơi đầu tiên, đúng lúc người chơi cần, mỗi gợi ý một lần. Thay đổi nhỏ, nhưng game trông "có chủ" hơn hẳn.

## Những vấn đề khó nhất

- **Thứ tự vẽ.** Sắp lớp theo toạ độ y khiến quái ở nửa dưới sân vượt lên trên đạn và hiệu ứng, đạn bị vẽ khuất sau quái. Giờ mọi thứ nằm trong các dải lớp rõ ràng.
- **Rung màn hình.** Bạo kích và nhát đập nào cũng rung, cuối game rung không ngừng. Tôi bỏ rung khi bạo kích, giãn các lần rung nhỏ, thêm tuỳ chọn rung và số sát thương.
- **Bộ não boss.** Đổi phase giữa chiêu làm hỏng trạng thái, nên phase được hoãn tới khi chiêu kết thúc, và bộ não phải dừng trước khi boss bị huỷ.
- **Màu sắc mang nghĩa.** Tôi muốn boss cuối màu vàng, nhưng trong game vàng là màu của "an toàn". Một đòn đánh cùng màu với vùng an toàn là lỗi nặng, nên boss giữ màu tím.

## Bản hiện tại làm tốt điều gì

Game chơi được trọn vẹn, ổn định, lần chạy kiểm thử đầy đủ gần nhất không có exception nào. Vòng lặp giữa hai đợt có nhiều quyết định thật. Mỗi boss nhận ra được trong vài giây, và Kiếp cao mang lại chiêu mới chứ không chỉ thêm máu. Đòn đánh có lực, bộ kiểm thử bắt được hồi quy, và một lớp nền tảng cho lưu game, thành tựu, input giúp đường lên các cửa hàng ngắn hơn.

## Những gì còn dở dang

- **Vũ khí khó phân biệt.** Ở cỡ icon thật, 24 vũ khí rơi vào năm nhóm trùng hình (8 cái cùng là "lưỡi thẳng chéo"). Đạn chỉ có 3 kiểu hình. Kế hoạch vẽ lại theo phong cách chibi, mỗi vũ khí một hình bóng riêng, đã xong và vài bản thử đã duyệt, nhưng chưa vẽ xong.
- **Hoạt ảnh.** Bộ máy hoạt ảnh đã chạy, và nhân vật có dải đi bộ. Clip đánh, clip đứng yên và toàn bộ hoạt ảnh boss vẫn chưa có; boss vẫn chỉ nhún lên xuống.
- **Vài sprite chưa đồng bộ.** Ma Tu chưa được làm lại như năm nhân vật kia. Mũ miện của boss cuối còn những mảng trắng vỡ hình. Màu đỏ thẫm của một boss quá tối với người mù màu đỏ.
- **Thần Thông đầu game.** Ở đợt 1 đến 5, chiêu của Đan, Thể và Phù chỉ chiếm khoảng 5 đến 10% sát thương, so với mục tiêu 20 đến 35%. Quá ít quái lọt vào tầm và chỉ bấm được 1 đến 3 lần mỗi đợt, nên phải đổi thiết kế Giai I chứ không chỉnh hệ số. Giai I của Ma Tu không gây sát thương. Thể Tu gần như không đổi dù đầu tư vào chiêu.
- **Kinh tế cuối game.** Linh Thạch vẫn dư khoảng 500 đến 900 mỗi đợt ở cuối game và chảy hết vào cửa hàng.
- **Kỳ Ngộ "Nghênh chiến"** dự định biến thành một trận đánh nhỏ, nhưng chưa làm.
- **Kiểm thử bằng người.** Phần lớn việc cân bằng gần đây chỉ được bot kiểm tra; game cần được chơi thử bằng tay nhiều hơn.
- **Phát hành.** Chưa tích hợp Steam hay Google Play. Cài đặt thiếu chọn độ phân giải, chưa có biểu tượng nút tay cầm, và cần kiểm tra lại điều khoản thương mại của gói PixelLab trước khi bán.

## Vì sao tôi tạm dừng

Tôi đang bắt đầu một dự án mới, tập trung vào chiến đấu hành động thời gian thực. Làm Lục Đạo Đọa Kiếp giúp tôi nhận ra phần mình mê nhất là đối mặt với boss: đọc cảnh báo, né, phản đòn. Một game mà vũ khí tự đánh có giới hạn ở đúng chỗ đó. Tôi mang theo phần hình ảnh, quy trình asset và những bài học ở đây, nhất là thói quen để bot và kiểm thử nói thật trước khi tin vào cảm giác.

Dừng ở đây không có nghĩa là bỏ dở. Game đủ hoàn chỉnh để chơi từ đầu đến cuối, có đủ 6 nhân vật, 6 boss và 6 Kiếp. Nó chỉ chưa được đánh bóng hết.

## Nếu làm tiếp

Nếu tôi quay lại, hoặc nếu bạn thấy hứng thú, đây là thứ tự tôi sẽ làm:

1. Vẽ lại 24 vũ khí theo kế hoạch hình bóng, kèm đạn mang dấu hiệu riêng của từng vũ khí.
2. Hoàn thiện hoạt ảnh: clip đánh và đứng yên cho nhân vật, rồi tới boss, ưu tiên tuyệt chiêu gốc của từng boss.
3. Thiết kế lại Giai I của Thần Thông (hồi chiêu ngắn hơn hoặc tầm rộng hơn ở đầu game), và cho Ma Tu gây sát thương ngay từ đầu.
4. Thêm nơi tiêu Linh Thạch cuối game, ví dụ pháp bảo có cái giá phải trả kiểu Brotato, hoặc rương "chọn 1 trong 2".
5. Biến Kỳ Ngộ "Nghênh chiến" thành trận đánh nhỏ.
6. Chơi thử bằng người nhiều hơn, chạy thêm lượt cân bằng cho Kiếp 3 và Kiếp 5, nhờ người bản ngữ đọc bản dịch.
7. Sau đó mới tới phát hành: Steam, Steam Deck, rồi Android.

Cảm ơn bạn đã đọc tới đây.
