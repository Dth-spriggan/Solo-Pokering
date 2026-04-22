using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SoloPokering.Gameplay;
using System.Collections.Generic;

public class UIManager : MonoBehaviour
{
    [Header("Backend")]
    public GameController gameController; 

    [Header("Panels")]
    public GameObject lobbyPanel;
    public GameObject gameplayPanel;

    [Header("Lobby UI - Cơ bản")]
    public Button startButton;
    public Transform botListContainer; 
    public GameObject botCardPrefab;   

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
        }
        
        if (raiseSlider != null)
            raiseSlider.onValueChanged.AddListener(OnRaiseSliderChanged);
        
        if (raisePanel != null) raisePanel.SetActive(false);
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
        if (raiseAmountText != null) raiseAmountText.text = "$" + currentRaiseAmount.ToString();
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

    private float botThinkTimer = 100f;
    private float showdownTimer = 10f;
    private void HandleAutoPlay(PokerTableSessionSnapshot state)
    {
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
            showdownTimer += Time.deltaTime;
            if (showdownTimer > 4.0f) 
            {
                showdownTimer = 0f;
                if (activeSeats.Count > 0) 
                {
                    foreach (var s in activeSeats) if (s != null) Destroy(s.gameObject);
                    activeSeats.Clear();
                }

                var humanSeat = state.Seats.Find(s => s.IsHuman);
                if (humanSeat != null && humanSeat.ChipStack <= 0)
                    gameController.ResetSession(); 
                else
                    gameController.StartNextHand(); 
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
}