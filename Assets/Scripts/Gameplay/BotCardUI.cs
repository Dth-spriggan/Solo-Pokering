using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SoloPokering.Gameplay;

public class BotCardUI : MonoBehaviour
{
    [Header("UI Elements")]
    public TMP_Text nameText;    // Tên Bot
    public TMP_Text styleText;   // Phong cách chơi
    public Button addButton;     // Nút cộng vào bàn
    public TMP_Text addBtnText;  // Chữ trên nút cộng

    private string myBotId;
    private GameController gameController;

    // Hàm này sẽ được UIManager gọi để bơm dữ liệu vào thẻ
    public void SetupCard(BotAvatarProfileSnapshot data, GameController controller)
    {
        myBotId = data.Id;
        gameController = controller;

        // Đổ chữ lên UI
        nameText.text = data.DisplayName;
        styleText.text = $"{data.DifficultyLabel} | {data.PlayingStyleLabel}";
        
        // Cập nhật trạng thái nút (Có cho bấm không, và hiện chữ gì)
        addButton.interactable = data.CanAdd;
        addBtnText.text = data.AvailabilityLabel; 

        // Gắn sự kiện khi bấm nút Add
        addButton.onClick.RemoveAllListeners();
        addButton.onClick.AddListener(OnClickAddBot);
    }

    private void OnClickAddBot()
    {
        gameController.QueueAddBot(myBotId);
    }
}