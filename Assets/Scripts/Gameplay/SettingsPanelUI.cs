using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SettingsPanelUI : MonoBehaviour
{
    [Header("Backend")]
    public SoloPokering.Gameplay.GameController gameController;

    [Header("Sliders")]
    public Slider timerSlider;
    public Slider bankSlider;
    public Slider bigBlindSlider;

    [Header("Hiển thị Text")]
    public TMP_Text timerText;
    public TMP_Text bankText;
    public TMP_Text bigBlindText;
    public TMP_Text smallBlindText;

    [Header("Nút Tăng/Giảm (+/-)")]
    public Button decreaseTimerBtn;
    public Button increaseTimerBtn;
    public Button decreaseBankBtn;
    public Button increaseBankBtn;
    public Button decreaseBlindBtn;
    public Button increaseBlindBtn;

    [Header("Buttons Chính")]
    public Button closeButton;
    public Button changeSettingsButton;

    [Header("UI Panels")]
    public GameObject lobbyPanel;

    // --- CẤU HÌNH BƯỚC NHẢY CỐ ĐỊNH (STEPS) ---
    private const float TIMER_STEP = 5f;     // Nhảy mỗi 5 giây
    private const float BANK_STEP = 100f;    // Nhảy mỗi $100
    private const float BLIND_STEP = 10f;    // Nhảy mỗi $10

    void Start()
    {
        // 1. Lắng nghe sự kiện Kéo Slider
        timerSlider.onValueChanged.AddListener(UpdateTimerText);
        bankSlider.onValueChanged.AddListener(UpdateBankText);
        bigBlindSlider.onValueChanged.AddListener(UpdateBlindText);

        // 2. Lắng nghe sự kiện Bấm nút Tăng/Giảm (Gọi hàm Check Min/Max)
        if (decreaseTimerBtn != null) decreaseTimerBtn.onClick.AddListener(() => ChangeSliderValue(timerSlider, -TIMER_STEP));
        if (increaseTimerBtn != null) increaseTimerBtn.onClick.AddListener(() => ChangeSliderValue(timerSlider, TIMER_STEP));
        
        if (decreaseBankBtn != null) decreaseBankBtn.onClick.AddListener(() => ChangeSliderValue(bankSlider, -BANK_STEP));
        if (increaseBankBtn != null) increaseBankBtn.onClick.AddListener(() => ChangeSliderValue(bankSlider, BANK_STEP));
        
        if (decreaseBlindBtn != null) decreaseBlindBtn.onClick.AddListener(() => ChangeSliderValue(bigBlindSlider, -BLIND_STEP));
        if (increaseBlindBtn != null) increaseBlindBtn.onClick.AddListener(() => ChangeSliderValue(bigBlindSlider, BLIND_STEP));

        // 3. Nút Đóng & Lưu
        closeButton.onClick.AddListener(ClosePanel);
        changeSettingsButton.onClick.AddListener(ApplySettings);
    }

    void OnEnable()
    {
        // if (lobbyPanel != null) lobbyPanel.SetActive(false);

        if (gameController != null && gameController.CurrentState != null)
        {
            var state = gameController.CurrentState;
            timerSlider.value = state.TurnTimerSeconds;
            bankSlider.value = state.StartingBank;
            bigBlindSlider.value = state.BigBlind;

            UpdateTimerText(timerSlider.value);
            UpdateBankText(bankSlider.value);
            UpdateBlindText(bigBlindSlider.value);
        }
    }

    // ==========================================
    // MA THUẬT NẰM Ở ĐÂY: HÀM ÉP GIỚI HẠN MIN/MAX
    // ==========================================
    private void ChangeSliderValue(Slider slider, float step)
    {
        // Tính toán giá trị mới
        float newValue = slider.value + step;
        
        // Mathf.Clamp: Chặn đứng giá trị không cho vượt quá minValue và maxValue của Slider (do Fuyuki cài trên Inspector)
        slider.value = Mathf.Clamp(newValue, slider.minValue, slider.maxValue);
    }

    // ==========================================
    // LÀM TRÒN KHI KÉO TAY (Khóa step chuẩn chỉ)
    // ==========================================
    private void UpdateTimerText(float val)
    {
        float roundedTimer = Mathf.Round(val / TIMER_STEP) * TIMER_STEP;
        timerSlider.SetValueWithoutNotify(roundedTimer);
        timerText.text = roundedTimer.ToString("0") + " Seconds";
    }

    private void UpdateBankText(float val)
    {
        float roundedBank = Mathf.Round(val / BANK_STEP) * BANK_STEP;
        bankSlider.SetValueWithoutNotify(roundedBank);
        bankText.text = "$" + roundedBank.ToString("0");
    }

    private void UpdateBlindText(float val)
    {
        float bb = Mathf.Round(val / BLIND_STEP) * BLIND_STEP;
        // Bảo kê thêm 1 lớp Min value phòng trường hợp Inspector cài sai
        if (bb < bigBlindSlider.minValue) bb = bigBlindSlider.minValue; 

        bigBlindSlider.SetValueWithoutNotify(bb); 
        bigBlindText.text = "$" + bb.ToString("0");
        smallBlindText.text = "$" + (bb / 2f).ToString("0"); 
    }

    // ==========================================
    // ÁP DỤNG SETTINGS
    // ==========================================
    private void ApplySettings()
    {
        if (gameController == null) return;

        int timer = (int)timerSlider.value;
        int bank = (int)bankSlider.value;
        int bb = (int)bigBlindSlider.value;
        int sb = bb / 2;

        gameController.SetTurnTimer(timer);
        gameController.SetStartingBank(bank);
        gameController.SetBlindValues(sb, bb);
        
        Debug.Log($"[SettingsPanel] Đã lưu: Timer={timer}, Bank={bank}, BB={bb}, SB={sb}");
        ClosePanel();
    }

    private void ClosePanel()
    {
        // if (lobbyPanel != null) lobbyPanel.SetActive(true);
        
        gameObject.SetActive(false);
    }
}