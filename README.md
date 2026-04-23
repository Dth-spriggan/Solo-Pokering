# 🃏 Solo Pokering - Version 1.0.1

Chào mừng đến với **Solo Pokering** - Tựa game Texas Hold'em Poker Offline được xây dựng trên nền tảng Unity!

🎮 **Link Tải Game (Windows .exe):** [Tải Solo Pokering v1.1.1 tại đây](<https://drive.google.com/file/d/1N1awAvr4iSw2EtBoQqyxgI38udFPGa1J/view?usp=sharing>)

## 🌟 Trạng Thái Hiện Tại: Bản Cập Nhật 1.1.1 (The "Clean Table" Patch)
Bản cập nhật 1.0.1 tập trung vá các lỗ hổng Core Logic quan trọng, tối ưu hóa giao diện (UI) cho màn hình 16:9 và mang lại trải nghiệm người dùng (UX) mượt mà, chuyên nghiệp hơn rất nhiều so với bản MVP.

### 🛠️ Patch Notes (Cập nhật mới)
* **Core Logic:**
  * Vá lỗi thuật toán Straight Flush (Sảnh rồng/Sảnh chúa) với low-Ace (A-2-3-4-5).
  * Fix lỗi "bốc hơi chip lẻ" khi Split Pot (odd chip giờ được phân bổ chuẩn theo vị trí từ Dealer).
  * Vá lỗ hổng Under-raise All-in (không mở lại vòng cược sai luật).
  * Thêm cơ chế "Đốt bài" (Burn Cards) chuẩn xác theo luật Texas Hold'em quốc tế để bảo toàn RNG.
* **UI & UX Polish:**
  * Căn chỉnh lại layout ghế ngồi trên bàn Oval chuẩn tỉ lệ 16:9 (1920x1080) rộng rãi, không bị dính đè UI.
  * Tiền cược (ChipStack) giờ đây được cập nhật realtime ngay khi người chơi hoặc Bot hành động (Bet/Raise/Call) thay vì phải đợi đến Showdown.
  * Tối ưu bảng Raise Panel gọn gàng, hiển thị lượng tiền trực quan.
  * Cải thiện luồng Game Over: Có thời gian delay 4s để người chơi xem kết quả bài River trước khi chuyển cảnh, kèm theo hiệu ứng âm thanh (BGM) riêng biệt.
  * Xử lý triệt để các xung đột trạng thái (Flickering UI) giữa các Panel Settings và Lobby.

### ✅ Tính năng nổi bật (Features)
* **Luật Poker Chuẩn Xác:** Xử lý trơn tru các luồng hành động Fold, Check, Call, Raise, All-in và tự động tính toán chia tiền (Side-pots) nhiều tầng.
* **Hệ thống Bot AI Đa Dạng:** Tích hợp sẵn các Bot với "nhân cách" và lối đánh khác nhau (được tính toán bằng thuật toán AI Monte Carlo mô phỏng tỉ lệ thắng).
* **Quản lý Bàn Chơi Tự Do:** Cho phép người chơi tự do nạp Bot, thiết lập số tiền vốn (Starting Bank), tiền mù (Blinds) và thời gian suy nghĩ.

### 🚧 Dự định tương lai (Roadmap)
* ⏳ **Animation & VFX:** Bổ sung hoạt ảnh chia bài, bay chip, và lật bài trong tương lai để tăng Game Feel.
* 💬 **Bot Interaction:** Thêm hệ thống Chatbox để các con Bot (như Stewie trùm lừa bịp hay Rachel khát máu) có thể "cà khịa" người chơi dựa trên tình huống ván bài.

## 🛠️ Công Nghệ Sử Dụng (Tech Stack)
* **Engine:** Unity 6
* **Ngôn ngữ:** C# (Áp dụng triệt để MVC Pattern giúp tách biệt Core Logic và UI).
* **UI:** Unity Canvas & TextMeshPro.

---
*Dự án phát triển bởi: **SilverFlagSyndicate & Allies***
