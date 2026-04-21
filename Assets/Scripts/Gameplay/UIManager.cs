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

    [Header("Lobby UI")]
    public Button startButton;
    public Transform botListContainer; 
    public GameObject botCardPrefab;   
    public TMP_InputField searchInput; 

    [Header("Gameplay UI - Bàn & Nút")]
    public TMP_Text potText;      
    // Đã tiễn vong thằng bannerText theo yêu cầu của Tech Lead
    public Button foldButton;
    public Button callButton;
    public Button raiseButton;
    public TMP_Text callButtonText;

    [Header("Gameplay UI - Ghế ngồi")]
    public Transform seatContainer; // Kéo cái Mặt Bàn (hoặc GameplayPanel) vào đây
    public GameObject seatPrefab;   // Kéo file UI_Seat_Prefab từ Project vào đây

    private List<GameObject> activeBotCards = new List<GameObject>();
    private List<UISeat> activeSeats = new List<UISeat>(); // Lưu trữ 7 cái ghế

    [Header("Community Cards")]
    public Image[] communityCards; // Chứa 5 lá bài trên bàn (Flop 1,2,3, Turn, River)
    public Sprite cardBackSprite;  // Ảnh mặt lưng lá bài

    void Start()
    {
        if (searchInput != null)
            searchInput.onValueChanged.AddListener((query) => gameController.SetBotQuery(query));
    }

    void Update()
    {
        if (gameController == null) return; 
        PokerTableSessionSnapshot state = gameController.CurrentState;
        if (state == null) return; 

        bool isPlaying = state.IsHandRunning || state.IsShowingResolvedHand;
        lobbyPanel.SetActive(!isPlaying);
        gameplayPanel.SetActive(isPlaying);

        if (!isPlaying)
        {
            startButton.interactable = state.CanStartGame;
            UpdateBotList(state.BotQueryResults);
        }
        else
        {
            UpdateGameplayUI(state);
        }

        HandleAutoPlay(state);
    }

    private void UpdateBotList(List<BotAvatarProfileSnapshot> bots)
    {
        if (activeBotCards.Count == bots.Count) 
        {
            for (int i = 0; i < bots.Count; i++)
                activeBotCards[i].GetComponent<BotCardUI>().SetupCard(bots[i], gameController);
            return;
        }

        foreach (GameObject card in activeBotCards) Destroy(card);
        activeBotCards.Clear();

        foreach (var botData in bots)
        {
            GameObject newCard = Instantiate(botCardPrefab, botListContainer);
            newCard.GetComponent<BotCardUI>().SetupCard(botData, gameController);
            activeBotCards.Add(newCard);
        }
    }

    private void UpdateGameplayUI(PokerTableSessionSnapshot state)
    {
        potText.text = "POT: $" + state.HandSnapshot.PotAmount.ToString();
        // Không còn bannerText.text = state.BannerMessage nữa!

        // --- ĐẺ GHẾ (Chỉ chạy 1 lần) ---
        if (activeSeats.Count == 0 && seatPrefab != null && seatContainer != null)
        {
            foreach (var seatData in state.Seats)
            {
                GameObject newSeat = Instantiate(seatPrefab, seatContainer);
                
                // Phép thuật căn tọa độ siêu chuẩn của Unity Anchors
                RectTransform rect = newSeat.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(seatData.AnchorX, seatData.AnchorY);
                rect.anchorMax = new Vector2(seatData.AnchorX, seatData.AnchorY);
                rect.anchoredPosition = Vector2.zero; // Hút chặt vào mỏ neo

                activeSeats.Add(newSeat.GetComponent<UISeat>());
            }
        }

        // --- BƠM DỮ LIỆU LIÊN TỤC VÀO GHẾ ---
        for (int i = 0; i < activeSeats.Count; i++)
        {
            if (i < state.Seats.Count)
                activeSeats[i].UpdateSeat(state.Seats[i]);
        }

        // --- XỬ LÝ 5 LÁ BÀI CHUNG TRÊN BÀN ---
        var commCards = state.HandSnapshot.CommunityCards;
        for (int i = 0; i < communityCards.Length; i++)
        {
            if (commCards != null && i < commCards.Count)
            {
                // Lá bài đã được lật (Flop, Turn, hoặc River)
                communityCards[i].gameObject.SetActive(true);
                
                // Nạp ảnh trực tiếp từ ResourceKey của Backend
                Sprite cardSprite = Resources.Load<Sprite>(commCards[i].ResourceKey);
                if (cardSprite != null)
                {
                    communityCards[i].sprite = cardSprite;
                }
            }
            else
            {
                // Lá bài chưa tới vòng lật -> Hiện mặt lưng
                communityCards[i].gameObject.SetActive(true);
                if (cardBackSprite != null) communityCards[i].sprite = cardBackSprite;
            }
        }

        // --- CẬP NHẬT NÚT BẤM ---
        bool isMyTurn = state.HandSnapshot.IsWaitingForHumanInput;
        PokerActionOptions options = state.HandSnapshot.HumanActionOptions;

        foldButton.interactable = isMyTurn && options.CanFold;
        callButton.interactable = isMyTurn && options.CanCheckOrCall;
        raiseButton.interactable = isMyTurn && options.CanRaiseOrBet;
        if (callButtonText != null) callButtonText.text = options.CheckOrCallLabel;
    }

    private float botThinkTimer = 0f;
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
    }

    public void OnClickStartGame() => gameController.StartNextHand();
    public void OnClickFold() => gameController.HumanFold();
    public void OnClickCall() => gameController.HumanCheckOrCall();
    public void OnClickRaise() => gameController.HumanRaiseOrBet(10); 
}