using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SoloPokering.Gameplay;

public class UISeat : MonoBehaviour
{
    [Header("UI Elements")]
    public Image avatarFrame;       // Cái vòng tròn viền ngoài cùng
    public Image avatarMaskImage;   // Cái khuôn hình tròn (có gắn Mask) để cắt ảnh
    public TMP_Text nameText;       // Tên người/Bot
    public TMP_Text chipText;       // Số tiền
    public GameObject statusPanel;  // Cục nền đen chứa Status
    public TMP_Text statusText;     // Chữ FOLD/RAISE...
    public Image card1;             // Lá bài 1
    public Image card2;             // Lá bài 2
    public GameObject timingRing;   // Vòng đếm ngược lúc đến lượt

    // Hàm này sẽ được UIManager gọi liên tục để bơm dữ liệu vào
    public void UpdateSeat(PokerTableSeatSnapshot seatData)
    {
        // 1. Ghế trống và không có ai đang đợi vào -> Tắt tàng hình luôn
        if (!seatData.IsOccupied && !seatData.IsPendingJoin)
        {
            gameObject.SetActive(false);
            return;
        }

        gameObject.SetActive(true);

        // 2. Điền Tên và Tiền (Snapshot đã xử lý sẵn chữ "Reserved" nếu có người đợi)
        nameText.text = seatData.DisplayName;
        chipText.text = "$" + seatData.ChipStack.ToString();

        // 3. Đổi màu Avatar cho ngầu (Cập nhật màu từ Backend)
        if (!string.IsNullOrEmpty(seatData.AvatarAccentHex) && ColorUtility.TryParseHtmlString(seatData.AvatarAccentHex, out Color accentColor))
        {
            if (avatarFrame != null) avatarFrame.color = accentColor;
            
            // Đổ màu luôn cho cái nền của Mask để đồng bộ
            if (avatarMaskImage != null) avatarMaskImage.color = accentColor;
        }

        // 4. Xử lý cái bảng Trạng Thái (Status)
        if (!string.IsNullOrEmpty(seatData.SeatStatusLabel) && 
            seatData.SeatStatusLabel != "Seated" && 
            seatData.SeatStatusLabel != "Ready" && 
            seatData.SeatStatusLabel != "Open seat")
        {
            if (statusPanel != null) statusPanel.SetActive(true);
            if (statusText != null) statusText.text = seatData.SeatStatusLabel;
        }
        else
        {
            if (statusPanel != null) statusPanel.SetActive(false);
        }

        // 5. Hiển thị 2 lá bài tẩy (Hole Cards)
        if (seatData.HoleCards != null && seatData.HoleCards.Count == 2)
        {
            if (card1 != null) 
            {
                card1.gameObject.SetActive(true);
                // Nạp ảnh cho lá số 1
                card1.sprite = Resources.Load<Sprite>(seatData.HoleCards[0].ResourceKey);
            }
            if (card2 != null) 
            {
                card2.gameObject.SetActive(true);
                // Nạp ảnh cho lá số 2
                card2.sprite = Resources.Load<Sprite>(seatData.HoleCards[1].ResourceKey);
            }
        }
        else
        {
            if (card1 != null) card1.gameObject.SetActive(false);
            if (card2 != null) card2.gameObject.SetActive(false);
        }

        // 6. Bật vòng sáng nếu đang đến lượt đánh
        if (timingRing != null)
        {
            timingRing.SetActive(seatData.IsCurrentTurn);
        }
    }
}