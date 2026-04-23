using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SoloPokering.Gameplay;
using System.Collections.Generic;

public class UIManager : MonoBehaviour
{
    [Header("Backend")]
    public GameController gameController;
    public CanvasGroup lobbyCanvasGroup;

    [Header("Panels")]
    public GameObject lobbyPanel;
    public GameObject gameplayPanel;

    [Header("Lobby UI - Cơ bản")]
    public Button startButton;
    public Transform botListContainer; 
    public GameObject botCardPrefab;

    [Header("GameOver UI")]
    public GameObject gameOverPanel;
    public AudioClip gameOverBGM;

    [Header("Audio")]
    public AudioSource bgmSource;
    public AudioClip BGM;

    [Header("Lobby UI - Thông số Bàn (table in4)")]
    public TMP_Text turnTimerStatText;
    public TMP_Text startingBankStatText;
    public TMP_Text bigBlindStatText;
    public TMP_Text smallBlindStatText;

    [Header("Gameplay UI - Bàn & Nút")]
    public TMP_Text potText;      
    public TMP_Text bannerText;   
    public Button foldButton;
    public Button callButton;
    public Button raiseOpenButton; 
    public TMP_Text callButtonText;

    [Header("Raise/Bet UI Panel")]
    public GameObject raisePanel;       
    public Slider raiseSlider;          
    public TMP_Text raiseAmountText;    
    public Button confirmRaiseButton;   
    public Button minRaiseBtn, quarterRaiseBtn, halfRaiseBtn, threeQuarterRaiseBtn, allInRaiseBtn; 

    [Header("Gameplay UI - Ghế ngồi")]
    public Transform seatContainer;
    public GameObject seatPrefab;
    public Image[] communityCards;
    public Sprite cardBackSprite;

    [Header("New UI - Settings & Info")]
    public GameObject mainSettingsPanel; 
    public GameObject handRankPanel;     
    public Slider bgmSlider;            
    public TMP_Text botModeText;

    private string[] botModes = { "EASY", "MEDIUM", "HARD", "RANDOM" };
    private int currentBotModeIndex = 1;

    private List<GameObject> activeBotCards = new List<GameObject>();
    private List<UISeat> activeSeats = new List<UISeat>();
    private string lastBotListHash = "";
    private SettingsPanelUI settingsPanel;
    private Button editTableSettingsButton;
    private bool hasWiredLobbyControls;
    private int currentRaiseAmount;

    private void Start()
    {
        if (gameController != null)
        {
            gameController.SetTurnTimer(30);          
            gameController.SetStartingBank(1000);     
            gameController.SetBlindValues(5, 10);
            ApplyBotModeSetting();
        }
        
        if (raiseSlider != null)
            raiseSlider.onValueChanged.AddListener(OnRaiseSliderChanged);
        
        if (raisePanel != null) raisePanel.SetActive(false);
        if (mainSettingsPanel != null) mainSettingsPanel.SetActive(false);
        if (handRankPanel != null) handRankPanel.SetActive(false);

        if (bgmSource != null && BGM != null)
        {
            bgmSource.clip = BGM;
            bgmSource.loop = true; // Cho nhạc lặp đi lặp lại không ngừng nghỉ
            bgmSource.Play();

            if (bgmSlider != null)
            {
                bgmSlider.value = bgmSource.volume;
                bgmSlider.onValueChanged.AddListener(OnBGMSliderChanged);
            }
        }

        UpdateBotModeUI();

    }

    void Update()
    {
        if (gameController == null || gameController.CurrentState == null) return; 
        PokerTableSessionSnapshot state = gameController.CurrentState;

        CacheOptionalReferences();
        TryWireLobbyControls();

        bool isSettingsVisible = (settingsPanel != null && settingsPanel.gameObject.activeSelf);
        bool isPlaying = state.IsHandRunning || state.IsShowingResolvedHand;

        if (lobbyPanel != null) lobbyPanel.SetActive(!isPlaying && !isSettingsVisible);
        if (gameplayPanel != null) gameplayPanel.SetActive(isPlaying);

        if (!isPlaying)
        {
            if (startButton != null) startButton.interactable = state.CanStartGame && !isSettingsVisible;
            if (editTableSettingsButton != null) editTableSettingsButton.interactable = !state.SettingsApplyToNextHand && !isSettingsVisible;
            UpdateLobbyTableInfo(state);           
        }
        else
        {
            UpdateGameplayUI(state);
        }

        UpdateLobbyBotList(state.BotQueryResults, isPlaying);

        HandleAutoPlay(state);
    }

    // ==========================================
    // LOGIC CHO SETTINGS (HAND RANK, BGM, BOT MODE)
    // ==========================================

    // Bật/Tắt bảng Hand Rank (Gắn vào nút Dấu Hỏi "?")

    
    public void ToggleHandRankPanel()
    {
        if (handRankPanel != null)
            handRankPanel.SetActive(!handRankPanel.activeSelf);
    }

    // Bật/Tắt bảng Main Settings (Gắn vào nút Bánh Răng)
    public void ToggleMainSettingsPanel()
{
    bool isActive = !mainSettingsPanel.activeSelf;
    mainSettingsPanel.SetActive(isActive);
    
    // Nếu Settings mở, khóa tương tác của Lobby. Nếu đóng, mở lại.
    if (lobbyCanvasGroup != null)
    {
        lobbyCanvasGroup.interactable = !isActive;
        lobbyCanvasGroup.blocksRaycasts = !isActive;
    }
}
    public void OnClickConfirmMainSettings()
    {
        if (mainSettingsPanel != null)
            mainSettingsPanel.SetActive(false);
    }
    // Chỉnh âm lượng (Gắn tự động ở hàm Start)
    public void OnBGMSliderChanged(float vol)
    {
        if (bgmSource != null) bgmSource.volume = vol;
    }

    // Nút sang Trái (Left) của Bot Mode
    public void OnClickPrevBotMode()
    {
        currentBotModeIndex--;
        if (currentBotModeIndex < 0) currentBotModeIndex = botModes.Length - 1;
        UpdateBotModeUI();
    }

    // Nút sang Phải (Right) của Bot Mode
    public void OnClickNextBotMode()
    {
        currentBotModeIndex++;
        if (currentBotModeIndex >= botModes.Length) currentBotModeIndex = 0;
        UpdateBotModeUI();
    }

    private void UpdateBotModeUI()
    {
        if (botModeText != null) botModeText.text = botModes[currentBotModeIndex];
        ApplyBotModeSetting();
    }

    // ==========================================
    // XỬ LÝ LOBBY BOT LIST (CHUẨN: CHỈ AVATAR, CHỐNG TÀNG HÌNH)
    // ==========================================
    private void UpdateLobbyBotList(List<BotAvatarProfileSnapshot> allBots, bool isPlaying)
    {
        if (botListContainer == null || botCardPrefab == null || allBots == null) return;

        // LOGIC LỌC THÔNG MINH:
        // - Ở Sảnh (!isPlaying): Hiện cả tụi đã xí ghế + tụi đang đợi
        // - Đang chơi (isPlaying): CHỈ hiện tụi đang Đợi (Vì tụi xí ghế đã hiện thẳng trên bàn Oval rồi)
        List<BotAvatarProfileSnapshot> displayBots = allBots.FindAll(b => isPlaying ? (b.IsQueued && !b.IsSeated) : (b.IsSeated || b.IsQueued));

        // TẠO MÃ BĂM (Hash) ĐỂ CHỐNG LAG & TÀNG HÌNH:
        // Chỉ vẽ lại UI khi thực sự có thằng thêm vào hoặc rút lui
        string currentHash = displayBots.Count + "_" + string.Join(",", displayBots.ConvertAll(b => b.Id));
        if (lastBotListHash == currentHash) return;
        lastBotListHash = currentHash;

        // Xóa sạch UI cũ
        foreach (GameObject card in activeBotCards) Destroy(card);
        activeBotCards.Clear();

        // Đẻ cục UI mới
        foreach (var bot in displayBots)
        {
            GameObject newCard = Instantiate(botCardPrefab, botListContainer);
            
            Image avatarImg = newCard.GetComponent<Image>();
            if (avatarImg != null)
            {
                Sprite faceSprite = Resources.Load<Sprite>("Avatars/" + bot.Id);
                if (faceSprite != null) avatarImg.sprite = faceSprite;
            }

            activeBotCards.Add(newCard);
        }
    }

    public void OnClickAddRandomBot()
    {
        if (gameController == null || gameController.CurrentState == null) return;

        List<BotAvatarProfileSnapshot> pool = gameController.CurrentState.BotQueryResults.FindAll(b => b.CanAdd);
        if (pool.Count > 0)
        {
            int randomIndex = Random.Range(0, pool.Count);
            gameController.QueueAddBot(pool[randomIndex].Id);
        }
        else
        {
            // Bơm vô hạn Bot vào hàng chờ (Clone)
            List<BotAvatarProfileSnapshot> allBots = gameController.CurrentState.BotQueryResults;
            if (allBots.Count > 0)
            {
                int randomIndex = Random.Range(0, allBots.Count);
                gameController.QueueAddBot(allBots[randomIndex].Id); 
            }
        }
    }

    // ==========================================
    // XỬ LÝ GAMEPLAY & BẢNG RAISE
    // ==========================================
    private void UpdateGameplayUI(PokerTableSessionSnapshot state)
    {
        if (potText != null) potText.text = "POT: $" + state.HandSnapshot.PotAmount.ToString();
        if (bannerText != null) bannerText.text = state.BannerMessage; 

        if (activeSeats.Count == 0 && seatPrefab != null && seatContainer != null)
        {
            foreach (var seatData in state.Seats)
            {
                GameObject newSeat = Instantiate(seatPrefab, seatContainer);
                RectTransform rect = newSeat.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(seatData.AnchorX, seatData.AnchorY);
                rect.anchorMax = new Vector2(seatData.AnchorX, seatData.AnchorY);
                rect.anchoredPosition = Vector2.zero;
                activeSeats.Add(newSeat.GetComponent<UISeat>());
            }
        }

        for (int i = 0; i < activeSeats.Count; i++)
        {
            if (activeSeats[i] != null && i < state.Seats.Count)
                activeSeats[i].UpdateSeat(state.Seats[i]);
        }

        if (communityCards != null)
        {
            var commCards = state.HandSnapshot.CommunityCards;
            for (int i = 0; i < communityCards.Length; i++)
            {
                if (communityCards[i] == null) continue; 
                if (commCards != null && i < commCards.Count)
                {
                    communityCards[i].gameObject.SetActive(true);
                    Sprite cardSprite = LoadCardSprite(commCards[i].ResourceKey);
                    if (cardSprite != null) communityCards[i].sprite = cardSprite;
                }
                else
                {
                    communityCards[i].gameObject.SetActive(true);
                    if (cardBackSprite != null) communityCards[i].sprite = cardBackSprite;
                }
            }
        }

        bool isMyTurn = state.HandSnapshot.IsWaitingForHumanInput;
        PokerActionOptions options = state.HandSnapshot.HumanActionOptions;

        if (foldButton != null) foldButton.interactable = isMyTurn && options.CanFold;
        if (callButton != null) callButton.interactable = isMyTurn && options.CanCheckOrCall;
        if (raiseOpenButton != null) raiseOpenButton.interactable = isMyTurn && options.CanRaiseOrBet;
        if (callButtonText != null) callButtonText.text = options.CheckOrCallLabel;
    }

    public void OnClickOpenRaisePanel()
    {
        PokerActionOptions options = gameController.CurrentState.HandSnapshot.HumanActionOptions;
        if (!options.CanRaiseOrBet) return;

        raisePanel.SetActive(true);
        raiseSlider.minValue = options.MinimumRaise;
        raiseSlider.maxValue = options.MaximumRaise;
        raiseSlider.value = options.MinimumRaise;
        OnRaiseSliderChanged(options.MinimumRaise);
    }

    public void OnRaiseSliderChanged(float val)
    {
        currentRaiseAmount = Mathf.RoundToInt(val);
        if (gameController != null && gameController.CurrentState != null)
    {
        PokerActionOptions options = gameController.CurrentState.HandSnapshot.HumanActionOptions;
        
        // Nếu không có ai Bet trước đó (Tức là đang Bet)
        if (options.AmountToCall == 0)
        {
            if (raiseAmountText != null) 
                raiseAmountText.text = "$" + currentRaiseAmount;
        }
        if (raiseAmountText != null) 
    {
        raiseAmountText.text = "$" + currentRaiseAmount.ToString();
    }
    }
    }

    public void OnClickConfirmRaise()
    {
        gameController.HumanRaiseOrBet(currentRaiseAmount);
        raisePanel.SetActive(false);
    }

    public void SetRaisePercentage(float percent)
    {
        PokerActionOptions options = gameController.CurrentState.HandSnapshot.HumanActionOptions;
        float target = options.MaximumRaise * percent;
        raiseSlider.value = Mathf.Clamp(target, options.MinimumRaise, options.MaximumRaise);
    }
    public void SetRaiseMin() => SetRaisePercentage(0f);
    public void SetRaiseQuarter() => SetRaisePercentage(0.25f);
    public void SetRaiseHalf() => SetRaisePercentage(0.5f);
    public void SetRaiseThreeQuarter() => SetRaisePercentage(0.75f);
    public void SetRaiseAllIn() => SetRaisePercentage(1f);

    private Sprite LoadCardSprite(string resourceKey)
    {
        if (string.IsNullOrWhiteSpace(resourceKey)) return null;
        Sprite[] subSprites = Resources.LoadAll<Sprite>(resourceKey);
        if (subSprites != null && subSprites.Length > 0) return subSprites[0];
        return Resources.Load<Sprite>(resourceKey);
    }

    private float botThinkTimer = 0f;
    private float showdownTimer = 0f;
    private void HandleAutoPlay(PokerTableSessionSnapshot state)
{
    // Nếu bảng Game Over đã bật rồi thì khóa chặt luồng AutoPlay lại luôn
    if (gameOverPanel != null && gameOverPanel.activeSelf) 
        return;

    var humanSeat = state.Seats.Find(s => s.IsHuman);
    bool isHumanBusted = (humanSeat != null && humanSeat.ChipStack <= 0);

    if (state.IsHandRunning && !state.HandSnapshot.IsWaitingForHumanInput)
    {
        botThinkTimer += Time.deltaTime;
        if (botThinkTimer > 1.5f) 
        {
            botThinkTimer = 0f;
            gameController.AdvanceAutoPlay(); 
        }
    }
    else if (state.IsShowingResolvedHand)
    {
        // 1. Cứ đếm giờ bình thường để player ngắm bài
        showdownTimer += Time.deltaTime;
        
        // 2. Chạm mốc 4 giây mới bắt đầu phân xử
        if (showdownTimer > 4.0f) 
        {
            showdownTimer = 0f;

            if (isHumanBusted)
            {
                // HẾT TIỀN: Hiện bảng Game Over và đổi nhạc
                if (gameOverPanel != null)
                {
                    gameOverPanel.SetActive(true);
                    
                    // Xử lý Audio
                    if (bgmSource != null && gameOverBGM != null)
                    {
                        bgmSource.clip = gameOverBGM;
                        bgmSource.loop = true; // Nhạc thua thường chỉ phát 1 lần rồi im lặng
                        bgmSource.Play();
                    }
                }
            }
            else
            {
                // CÒN TIỀN: Bắt đầu ván mới
                gameController.StartNextHand(); 
            }
        }
    }
}

    private void CacheOptionalReferences()
    {
        if (settingsPanel == null) settingsPanel = Object.FindFirstObjectByType<SettingsPanelUI>(FindObjectsInactive.Include);
        if (editTableSettingsButton != null || lobbyPanel == null) return;
        Button[] buttons = lobbyPanel.GetComponentsInChildren<Button>(true);
        foreach (Button button in buttons)
        {
            if (button != null && button.name == "Edit Table Setting")
            {
                editTableSettingsButton = button;
                break;
            }
        }
    }
    private void TryWireLobbyControls()
    {
        if (hasWiredLobbyControls || editTableSettingsButton == null) return;
        editTableSettingsButton.onClick.RemoveListener(OnClickOpenSettings);
        editTableSettingsButton.onClick.AddListener(OnClickOpenSettings);
        hasWiredLobbyControls = true;
    }
    private void UpdateLobbyTableInfo(PokerTableSessionSnapshot state)
    {
        if (turnTimerStatText != null) turnTimerStatText.text = state.TurnTimerSeconds.ToString() + " Seconds";
        if (startingBankStatText != null) startingBankStatText.text = "$" + state.StartingBank.ToString("0");
        if (bigBlindStatText != null) bigBlindStatText.text = "$" + state.BigBlind.ToString("0");
        if (smallBlindStatText != null) smallBlindStatText.text = "$" + state.SmallBlind.ToString("0");
    }
    public void OnClickOpenSettings() { ShowSettingsPanel(); }
    private void ShowSettingsPanel() { if (settingsPanel != null) settingsPanel.gameObject.SetActive(true); }
    public void OnClickStartGame() => gameController.StartNextHand();
    public void OnClickFold() => gameController.HumanFold();
    public void OnClickCall() => gameController.HumanCheckOrCall();
    public void OnClickBackToLobby()
{
    if (gameOverPanel != null) gameOverPanel.SetActive(false);
    gameController.ResetSession();
    if (bgmSource != null && BGM != null)
    {
        bgmSource.clip = BGM;
        bgmSource.loop = true; // Bật lặp lại
        bgmSource.Play();
    }
}

// Hàm gán cho nút "Thoát Game"
public void OnClickQuitGame()
{
    Debug.Log("Thoát game...");
    Application.Quit();
}
    private void ApplyBotModeSetting()
    {
        if (gameController == null)
            return;

        PokerBotMode mode = PokerBotMode.Medium;
        switch (currentBotModeIndex)
        {
            case 0:
                mode = PokerBotMode.Easy;
                break;
            case 2:
                mode = PokerBotMode.Hard;
                break;
            case 3:
                mode = PokerBotMode.Random;
                break;
        }

        gameController.SetBotMode(mode);
    }
}
