## Solo Pokering Gameplay Core

This folder contains the Unity-friendly orchestration layer for the migrated 2014 Texas Hold'em project.

Structure:

- `Core/Legacy`: the extracted Holdem engine from the desktop project
- `Gameplay/OfflinePokerGame.cs`: drives offline human-vs-bot hands for Unity
- `Gameplay/PokerTableSession.cs`: 7-seat lobby/gameplay session layer with pending add/kick support
- `Gameplay/BotAvatarProfile.cs`: queryable bot/avatar catalog for startup and in-game roster changes
- `Gameplay/PokerGameSnapshot.cs`: snapshot/state objects for UI binding later
- `Gameplay/PokerTableSessionSnapshot.cs`: lobby + table + seat snapshot for Unity UI
- `Gameplay/PokerMatchSettings.cs`: match configuration from the document (timer, bank, blinds)
- `Gameplay/GameController.cs`: MonoBehaviour wrapper for scene buttons, sliders, and future panels

Suggested Unity flow:

1. Create `GameController` or `PokerTableSession`
2. Update table settings and human name
3. Query bot avatars through `SetBotQuery(...)` and read `BotQueryResults`
4. Add bots with `QueueAddBot(profileId)` and mark removals with `ToggleKickBot(seatIndex)`
5. If a hand has already finished, call `ApplyPendingSeatChangesNow()` when the showdown/result panel is dismissed
6. Start a hand with `StartNextHand()`
7. Render from `PokerTableSessionSnapshot` and `PokerGameSnapshot`
8. When it is the human turn, call `HumanFold()`, `HumanCheckOrCall()`, `HumanRaiseOrBet(amount)`, or `HumanAllIn()`

Card asset note:

- `PokerCardSnapshot.ResourceKey` is a Resources path such as `Cards/21`
- the current imported card textures are sub-sprites, so UI should use `Resources.LoadAll<Sprite>(resourceKey)[0]`
- `Holdem.Card.GetSpriteSubAssetName()` is also available if you prefer prebuilding a sprite lookup table

Roster/pending rules:

- Total table capacity is 7 seats including the human at seat `0`
- Human stays at the bottom-center logical seat
- Bot seats are filled in a balanced order around the table instead of stacking one side first
- Bots added during a hand are queued and can be applied once showdown is done
- Bots kicked during a hand are only removed after the current hand finishes
- Query results expose avatar/name/style/status info so UI can show searchable bot cards before adding them
- Seat snapshots expose pending join/leave flags, replacement status, difficulty/style labels, and a friendly status line for overlays
