using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SettingsPanelUI : MonoBehaviour
{
    [Header("Sliders")]
    public Slider timerSlider;
    public Slider bankSlider;
    public Slider bigBlindSlider;

    [Header("Hiển thị Text")]
    public TMP_Text timerText;
    public TMP_Text bankText;
    public TMP_Text bigBlindText;
    public TMP_Text smallBlindText; // Thằng này tự tính, không có slider

    [Header("Buttons")]
    public Button closeButton;
    public Button changeSettingsButton;

    void Start()
    {
        // Gắn "tai nghe" cho các Slider: Cứ kéo là tự gọi hàm cập nhật chữ
        timerSlider.onValueChanged.AddListener(UpdateTimerText);
        bankSlider.onValueChanged.AddListener(UpdateBankText);
        bigBlindSlider.onValueChanged.AddListener(UpdateBlindText);

        // Gắn sự kiện nút bấm
        closeButton.onClick.AddListener(ClosePanel);
        changeSettingsButton.onClick.AddListener(ApplySettings);

        // Chạy lần đầu tiên để nạp số mặc định lên màn hình
        UpdateTimerText(timerSlider.value);
        UpdateBankText(bankSlider.value);
        UpdateBlindText(bigBlindSlider.value);
    }

    private void UpdateTimerText(float val)
    {
        timerText.text = val.ToString("0") + " Seconds";
    }

    private void UpdateBankText(float val)
    {
        // Nhảy mỗi bước là $100 cho nó chẵn (500, 600, 700...)
        float roundedBank = Mathf.Round(val / 100f) * 100f;
        bankSlider.SetValueWithoutNotify(roundedBank);
        bankText.text = "$" + roundedBank.ToString("0");
    }

    private void UpdateBlindText(float val)
    {
        // Big Blind nhảy mỗi bước là $10 (10, 20, 30...)
        float bb = Mathf.Round(val / 10f) * 10f;
        if (bb < 10f) bb = 10f; // Bét nhất phải là 10
        bigBlindSlider.SetValueWithoutNotify(bb); 

        bigBlindText.text = "$" + bb.ToString("0");
        
        // Ma thuật: Small Blind luôn = 1/2 Big Blind
        smallBlindText.text = "$" + (bb / 2f).ToString("0"); 
    }

    private void ApplySettings()
    {
        // TODO: Chỗ này sau này ông móc nối với GameController hoặc PokerConfig
        // Ví dụ: gameController.SaveConfig(timerSlider.value, bankSlider.value, bigBlindSlider.value);
        
        Debug.Log($"Đã lưu: Timer={timerSlider.value}, Bank={bankSlider.value}, BB={bigBlindSlider.value}");
        ClosePanel();
    }

    private void ClosePanel()
    {
        gameObject.SetActive(false); // Ẩn cái bảng này đi
    }
}