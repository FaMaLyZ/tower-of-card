# Neo Tower of Cards — HUD Summary

> **Status:** Snapshot of every UI element in `MainGame` after A6 and Track B (2026-11-16).
> **Purpose:** Starting point for the HUD redesign (step 3 of the juice order in [juice-plan.md](juice-plan.md)).
> **Rule:** The HUD only *shows* the game. Every number comes from `GameFlowController` or a worker; buttons send commands. No UI decides anything.

---

## 1. What the player must always be able to read

Ordered by how often the player needs it during a hand.

| Priority | Information | Where it is today |
|---|---|---|
| 1 | What to do now (or why an action was refused) | `StateText` |
| 1 | The Pot, and what Bank would pay now | `PotText` |
| 1 | Safe points vs. the blind target | `TotalScoreText` + `TurnText` |
| 2 | Hand number / hands per blind | `TurnText` |
| 2 | Glass (lives) | `GlassText` |
| 2 | Live hand name while selecting | `HandNameText` |
| 3 | Ante, blind, Run total, reward multiplier | `RunText` |
| 3 | Swaps left | inside `StateText` only |
| 3 | Deck / discard counts | `DeckText`, `DiscardText` |
| 3 | Card details (rank, suit, weight, chips) | `CardTooltip` and `HintText` |

---

## 2. Every element today

Grouped by what the element is about. The script that drives it is in brackets; plain `HudView` texts have none.

### 2.1 Run and blind

| Element | Shows (example) | Visible |
|---|---|---|
| `RunText` | `Ante 1 / 4  Small blind   Run 0   Reward x1` (+ "(retry)") | Always |
| `TurnText` | `Hand 2 / 4   Target 150` | Always |
| `GlassText` (`GlassView`) | `Glass ### (3 / 3)` | Always |
| `GlassNoteText` (`GlassView`) | "The Glass cracked!" / "The Glass was repaired" | 2.5 s after a change |
| `BannerText` | Big centre text: "Turn 2", "Turn 2 complete", blind result + leave bonus | Draw, Score/Shake, between blinds |

### 2.2 Hand

| Element | Shows (example) | Visible |
|---|---|---|
| `StateText` | Phase instruction, Play Hand refusal reason, deck-click refusal reason | Always |
| `CardsText` | `Cards: 2 / 5` (placed / committed) | Always |
| `HintText` | Hovered card details, or the placing hint | Always |
| `CardTooltip` (`CardTooltip`) | Above the hovered card: "Nine of Diamonds / Weight 9  Chips 9" | Hovering a usable hand card |
| Snap markers (`SnapMarkerView`) | A dot per free slot; green = where the card will snap | While aiming a card |

### 2.3 Deck

| Element | Shows (example) | Visible |
|---|---|---|
| `DeckText` | `Deck: 39` | Always |
| `DiscardText` | `Discard: 5` | Always |
| Swaps left | Only as part of `StateText` today (see section 4) | Swap |

### 2.4 Score and Pot — `ScoresPanel`

| Element | Shows (example) | Visible |
|---|---|---|
| `HandNameText` | "Three of a Kind" (live preview, then the scored hand) | Commit, Build, scoring |
| `ChipsText` | `Chips: 25` | While scoring |
| `MultText` | `Mult: x2` | While scoring |
| `TurnScoreText` | `Turn score: 50` | After the count finishes |
| `TotalScoreText` | `Safe: 120` (banked this blind) | Always |
| `PotText` | `Pot: 16` / `Bank now: 17 (16 chips +10% height, 1 levels)` / `Last shake: 0 broken (-10% each)` or "The tower collapsed" | Always |

### 2.5 Buttons and choices

| Button | Does | Visible |
|---|---|---|
| `EndTurnButton` | "Done Swapping" (Swap) → "Play Hand" (Commit); greyed when not allowed | Swap, Commit, Build |
| `BankButton` / `RideButton` (`BankRideView`) | Bank the Pot / Ride and keep building; Ride greyed on a full tower | Bank-or-Ride choice |
| `ContinueButton` / `LeaveButton` (`ContinueLeaveView`) | Continue, or "Leave Table +N" | After a Bank that reaches the target with hands left |

### 2.6 Full screens

**Blind menu** — `BlindMenuPanel` (`BlindMenuView`), visible only while choosing a blind:

- `HeaderText`: `Ante 1 / 4` (+ " - Final Boss retry").
- Three columns — Small, Big, Boss — each with `NameText`, `StatusText` (Current / Upcoming / Won / Failed / Skipped), `TargetText`, `RewardText`, a **Select** button and a **Skip** button ("Skip (repair 1 crack)" / "Skip (Glass is whole)"; never on the Boss).
- Current blind highlighted; the other two are previews.

**Run end** — `ENDpanel` (`RunEndView`):

- `ScoreText`: "RUN CLEARED" / "GLASS SHATTERED", Run total, Ante reached, last blind.
- **Main Menu** and **Play Again** buttons (with separate label texts), two sparkles. Fades in.

### 2.7 Not part of the HUD

| Element | Shows | Visible |
|---|---|---|
| Debug label (`DebugCheats`) | Phase, ante, hand, safe, glass, F-keys | Editor / dev builds only |

---

## 3. What each phase needs on screen

| Phase | Must show | Buttons |
|---|---|---|
| Blind menu | Ante, three blinds (target, reward, status), Glass | Select, Skip |
| Draw | Hand number, banner | — |
| Swap | Instruction, swaps left | Done Swapping (deck click swaps) |
| Commit | Live hand name, instruction or refusal reason | Play Hand |
| Build | Cards left to place, placing hint, snap markers | (Undo by right-click) |
| Score | Hand name, chips × mult, turn score | — |
| Shake | Pot (unsettled) | — |
| Bank or Ride | Pot, Bank quote with breakdown, last shake result, Safe vs. target | Bank, Ride |
| Continue or Leave | Safe vs. target, leave bonus | Continue, Leave Table |
| Blind end | Won / failed, score vs. target, leave bonus, Glass change | — |
| Run end | Cleared / shattered, Run total, how far | Main Menu, Play Again |

---

## 4. Already in code, not in the scene

| Feature | Field | Note |
|---|---|---|
| Undo button | `HudView.undoButton` | Undo is right-click only today. Shown only in Build when wired. |
| Swaps left text | `HudView.swapsLeftText` | Format "Swaps left: {0}". Today swaps left appear only in `StateText`. |

---

## 5. Notes for the redesign

- **Overlap:** hovered card details appear in both `HintText` and `CardTooltip`. Pick one.
- **Clean-up:** five stray `SnapMarker` copies are saved under the Canvas. `SnapMarkerView` makes its own; they can be deleted.
- **No code needed to move or reskin:** every text is its own object, and every wording is a format string on its script (HudView, BlindMenuView, GlassView, RunEndView, BankRideView, ContinueLeaveView, CardTooltip). Only `CardTooltip` and the snap markers position themselves.
- **New elements:** if the design adds a display that has no field yet (for example a target progress bar), it needs a small view change — list it in the design so it can be built in the HUD step.
- **Keep these as separate elements — Track C animates them after the HUD update:**

| Effect | Animates |
|---|---|
| C1 | Hand name |
| C2 | Chips, mult, turn score (score roll, floating "+14") |
| C5 | Pot and Safe displays (chips fly from Pot to Safe on Bank) |
| C6 | Blind menu, blind won / failed screens |
| C7 | Glass display (crack / repair) |

- **Readability first:** the HUD must work with every effect switched off. Effects animate on top of correct, instant numbers.
