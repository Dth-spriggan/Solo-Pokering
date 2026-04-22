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

    [Header("Blind Mark (Huy hiệu SB/BB)")]
    public GameObject blindBadgeObj; // Cái cục nền chứa chữ (Image)
    public TMP_Text blindText;       // Chữ bên trong (SB hoặc BB)

    // Hàm này sẽ được UIManager gọi liên tục để bơm dữ liệu vào
    public void UpdateSeat(PokerTableSeatSnapshot seatData)
    {
        // 1. Ghế trống và không có ai đang đợi vào -> Tắt tàng hình luôn
        if (!seatData.IsOccupied)
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

        if (blindBadgeObj != null && blindText != null)
        {
            // Kiểm tra thuộc tính từ Backend (Ông check lại tên biến cho chuẩn nhé)
            if (seatData.IsBigBlind) 
            {
                blindBadgeObj.SetActive(true);
                blindText.text = "BB";
            }
            else if (seatData.IsSmallBlind) 
            {
                blindBadgeObj.SetActive(true);
                blindText.text = "SB";
            }
            else if (seatData.IsDealer) // Nếu Backend có cờ Dealer thì show luôn chữ D
            {
                blindBadgeObj.SetActive(true);
                blindText.text = "D";
            }
            else
            {
                blindBadgeObj.SetActive(false); // Ghế thường thì tắt huy hiệu
            }
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
                card1.sprite = LoadCardSprite(seatData.HoleCards[0].ResourceKey);
            }
            if (card2 != null) 
            {
                card2.gameObject.SetActive(true);
                card2.sprite = LoadCardSprite(seatData.HoleCards[1].ResourceKey);
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

    private Sprite LoadCardSprite(string resourceKey)
    {
        if (string.IsNullOrWhiteSpace(resourceKey))
            return null;

        Sprite[] subSprites = Resources.LoadAll<Sprite>(resourceKey);
        if (subSprites != null && subSprites.Length > 0)
            return subSprites[0];

        return Resources.Load<Sprite>(resourceKey);
    }
}
