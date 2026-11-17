# Neo Tower of Cards — Juice & Feel Plan

> **Status:** Presentation plan, aligned with the [Master Plan](Master%20Plan%20Real.md) and [Core Gameplay Loop](Core%20Gameplay%20Loop.md).
> **Date:** 2026-11-11 · **Revision 2:** 2026-11-16, after A6 (see [Revision notes](#revision-notes))
> **Authority:** The Core Gameplay Loop owns the rules; the Master Plan owns architecture and build order. This document describes how those actions look and sound.

**Reference:** Mix and Jam — "Recreating Balatro's Game Feel"
**Scope:** No shader polish. No fold card effect. The effects below are candidates for the prototype, in the Master Plan's order.

## Before Adding Effects

- `GameFlowController` is the only phase controller (`TurnManager` is gone since A6); do not add another flow manager on top.
- Hand roots keep the authoritative position, input target, card identity, selection, and order. A `Visual` child receives hover, tilt, punch, arc, and lag effects.
- Basic selection highlights, usable layout, drag input, and reorder behavior belong to the normal view/controller. They still work when every juice component is disabled.
- UI input sends commands to the conductor/core for validation. Juice components listen to accepted results and never change cards, score, Pot, or phase.
- Core actions complete independently of animation. The conductor may wait through a **bounded optional presentation gate**. Missing, disabled, interrupted, or timed-out effects use the instant display and finish the gate; they cannot strand a phase.
- The **File / integration** notes below identify where data or input comes from. They do not instruct us to place animation coroutines inside gameplay managers. Animation lives in presentation components.
- No effect moves a tower Rigidbody or collider. A ghost's visual lag cannot move the actual placement target. Presentation offsets must be reset on disable and scene reload.

---

# PART 1 — JUICE

## Category 1 — On Hover

### 1.1 Shake on Hover

When the mouse enters an interactable card, play a quick rotation shake — left-right wobble, 2–3 cycles, about 0.2 seconds total. It signals "this is clickable."

- **Trigger:** Hover input from `HandCardView`.
- **Animation:** Oscillate the `Visual` child's Z rotation by about ±6°, decaying to its normal visual pose.
- **File / integration:** `HandCardJuice.cs` listens to the view's hover state.

### 1.2 3D Tilt Toward Mouse (Parallax Tilt)

While the mouse is over a card, it tilts in 3D according to the mouse position relative to the card center. Mouse left of center → card tilts right; mouse above center → card tilts back.

```text
offset    = mouseScreenPos - cardScreenCenter
rotationX = -offset.y * tiltStrength
rotationY =  offset.x * tiltStrength
```

Apply this only as a visual offset on top of the base fan pose. Lerp back to zero on mouse exit.

- **File / integration:** `HandCardJuice.cs`, reading hover state from `HandCardView` and the base pose from the normal layout.
- **Strength:** About 8–12 degrees maximum, adjusted by feel.

### 1.3 Fan Spread on Mouse Proximity

As the mouse moves across the fan, nearby cards push sideways away from it. Cards outside the spread radius are unaffected. Return to normal spacing when the mouse leaves.

- **Animation:** Distance-weighted visual offsets, with a falloff radius of about two card widths.
- **File / integration:** A hand presentation component reads the base hand layout and offsets the `Visual` children.
- **Input requirement:** Card hit targets and selection must remain predictable. If the effect makes the visible card hard to click, reduce the spread or deliberately update the view's hit-region presentation; never reorder the card data as a side effect.

---

## Category 2 — On Click / Select

### 2.1 Position Punch on Select

When an accepted selection changes a card from unselected to selected, it lifts with a slight overshoot, then settles. Use a spring curve rather than a plain lerp.

- **Curve:** Y goes `0 → selectedLift × 1.3 → selectedLift` over about 0.15 seconds.
- **File / integration:** `HandCardJuice.cs` reacts to the accepted selection state. The base view shows selection immediately if juice is absent.

### 2.2 Toggle-Pop Deselect

Clicking a selected card removes that card from the selected set. It punches down with a small undershoot, then returns to its normal visual pose.

- **Rule:** Toggling one selected card never clears the others. Commit allows up to 5 selected cards; Swap allows as many as there are swaps left (up to 3).
- **File / integration:** The normal input controller requests the selection change; `HandCardJuice.cs` animates its result.

### 2.3 Snap Punch on Card Arrival

When a newly drawn or reordered card's visual reaches its destination, play a one-time scale punch: `1.0 → 1.15 → 1.0` over about 0.15 seconds.

- **File / integration:** `DrawAnimator.cs` or the hand presentation component tracks visual arrival and fires once per movement.
- **Rule:** Arrival is a presentation event. It does not add the card to the hand or commit a reorder.

---

## Category 3 — On Drag / Movement

### 3.1 Velocity Tilt During Movement

While a card's visual moves through a draw arc, drag, or fan adjustment, it tilts in the direction of travel. It returns to its base rotation as its visual velocity approaches zero.

- **Formula:** `tiltAngle = visualVelocityX × tiltMultiplier`, clamped to about ±25°.
- **File / integration:** `HandCardJuice.cs` or `DrawAnimator.cs` tracks the visual motion, without changing the root or card data.

### 3.2 Drag to Reorder Hand

The player can drag a card left or right to rearrange the hand. Other cards move aside to show the new position. On release, the visual settles into the final slot with the snap punch from 2.3.

**Normal input behavior, implemented before its effects:**

1. Pointer movement past a threshold of about 10 pixels starts a drag; a short release without crossing the threshold remains a click.
2. The controller requests a reorder when the dragged card crosses a neighboring slot's midpoint.
3. The hand model validates the request for the current phase and changes its ordered card IDs once. Selection follows card identity, not list index.
4. On release, the view lays out the accepted order. With juice disabled, this happens instantly.

**Optional effects:** Neighbors glide aside; the dragged `Visual` tilts and lags; release plays a snap punch.

- **File / integration:** `HandCardView.cs` handles pointer/drag events; `HandManager` owns the accepted order. Presentation components animate the resulting poses.
- **Rule:** Reordering never changes the committed card set, poker result, or which cards have been placed. Phase permissions come from the core.

### 3.3 Mouse Lag While Dragging

A dragged card's visual follows the mouse with a slight lag, roughly one frame behind. Apply this to the hand card visual or the tower ghost visual.

- **File / integration:** `HandCardJuice.cs` for hand cards. For the tower, `TowerPlacementInput` already eases its preview card toward the snapped slot (`snapSpeed`); any extra lag goes in a separate presentation component that reads it.
- **Placement requirement:** Snapping, hit tests, preview legality, and the accepted slot use the immediate logical target. On placement, the real physics card appears at the accepted slot, never at the delayed visual position.

---

## Category 4 — Draw Animation

### 4.1 Sequenced One-by-One Draw

New cards appear one at a time, about 0.08–0.12 seconds apart. Unplayed cards remain in the hand; draw only enough cards to reach the hand limit of 8.

- **Core:** The deck and hand perform the draw and record the result independently of the effect.
- **Presentation:** `DrawAnimator.cs` plays the accepted draw result. The conductor's optional gate can wait for this sequence within its time budget.
- **Fallback:** All new cards appear in their final readable layout immediately. No animation callback is required to make them usable.

### 4.2 Bézier Arc from Deck

Each new card's `Visual` travels from the deck to its assigned hand pose along a Bézier curve, rising before descending.

- **Control point:** Start around two world units above the straight-line midpoint, then tune for the camera and hand scale.
- **File / integration:** `DrawAnimator.cs` reads card identities and destination poses from the accepted draw result.

### 4.3 Tilt During Flight

Apply the velocity tilt from 3.1 during the arc. The visual leans in its travel direction and settles at its destination.

---

## Category 5 — Card Info

### 5.1 Weight + Chips Tooltip on Hover

A small label near the hovered card shows:

```text
[Suit icon] Rank • Weight: X • Chips: Y
```

Read the card's actual gameplay mass and chip value. This is separate from the bottom hint text.

- **File / integration:** `CardTooltip.cs` reads the card snapshot; a presentation component supplies its fade.
- **Fallback:** Information appears without a fade. Essential card information remains accessible without juice.

---

## Category 6 — World / Tower

### 6.1 Camera Shake on Breaks and Collapse

Every `CardBroken` gives the camera a small jolt, and jolts stack: cards breaking close together build into a bigger shake (decided 2026-11-18 after play-testing C4). The authoritative `TowerCollapsed` event (added in C4, see [Events the effects need](#events-the-effects-need)) gives the full shake. Uses "shake energy" from 0 to 1: each event adds some, it drains over about half a second, and the X/Y offset and tilt follow what is left.

- **File / integration:** `CameraShaker.cs` listens to `CardBroken` and `TowerCollapsed` and adds an offset to the orbit camera's normal pose.
- **Rule:** A broken card alone does not imply collapse. `TowerBuilder` does not call camera effects directly, and the camera effect never shakes the physical table.

### 6.2 Deck Feedback

The deck responds to hover, cards entering or leaving, and rejected interaction.

- **Hover when available:** Brief rotation wobble to signal that it is clickable.
- **Card arrival:** Scale `1.0 → 1.15 → 1.0`, about 0.12 seconds.
- **Card departure:** Scale `1.0 → 0.88 → 1.0`, about 0.12 seconds.
- **Arrival flash:** Flash white once per accepted card return. If several cards return during a reset, the flashes may be queued or shortened within the presentation budget.
- **Unavailable interaction:** Brief X-axis shake and red flash after the core rejects the deck command. The rejection reason also has a readable plain-text or disabled-state equivalent.
- **File / integration:** `DeckFeedback.cs` listens to deck/swap results and rejection feedback. `DeckGlowBehavior.cs` reads whether a swap is available. Deck input stays in the normal view/controller, independent of these effects.

---

# PART 2 — PRESENTING THE GAME FLOW

## Overview

The [Master Plan](Master%20Plan%20Real.md) defines every transition and edge case. This document supplies presentation cues for those phases:

```text
Draw → Swap → Commit → Build → Score → Shake → resolve the hand
                                                   │
                         Bank / Ride, automatic Bank, or collapse
```

`GameFlowController` alone advances the game. Every visual component reads accepted core state or events. Completion of a visual sequence can release an optional presentation gate; it never applies a score or chooses the next phase.

## Phase 1 — Draw

Top up the hand to 8 while retaining unused cards. Present each new card with its deck arc, flight tilt, and arrival punch. A short **"Your Hand"** banner may accompany the sequence.

The hand model already contains the accepted draw. A missing animator, skip, or timeout immediately shows the final hand and releases the presentation gate.

## Phase 2 — Swap

The player picks **up to 3 cards** (one swap each), then clicks the deck once to exchange all of them. They may finish early with **Done Swapping**. Unused swaps do not carry to the next hand.

**Visual state:**

- The picked cards have a distinct tint and lift.
- The deck glows while a swap is possible (`GameFlowController.CanSwapSelectedCard`).
- The HUD says **"Swap: pick up to N cards, then click the deck"**.

**On an accepted swap:**

1. The outgoing cards pop, tilt, and arc toward the deck, slightly staggered.
2. The deck flashes on arrival.
3. The replacements arc into the hand and punch on arrival.

The swap is one core transaction (`SwapManager.TrySwapSelected`); an animation cannot cause an extra exchange. Clicking the deck with nothing picked gives rejection feedback, which needs the `SwapRejected` event first.

**Integration:** `SwapManager` owns the exchange. `SwapZone` sends the deck click to the conductor. Draw / deck / hand presenters animate accepted results.

## Phase 3 — Commit

The player selects **1–5 cards**. The live preview shows the evaluated poker hand before **Play Hand** is pressed. The core checks selection and build capacity before accepting the commitment.

**On an accepted commit:**

1. Non-committed cards dim and reduce toward 0.75× scale while remaining readable.
2. Committed cards lift, grow toward 1.2× scale, and align in the active area.
3. A short announcement, about 0.6 seconds, shows the hand name and its real scoring values. For example, the optional name **"DOUBLE BRACE"** represents Two Pair, whose current base values are `20 × 2`.
4. The presentation gate completes or skips, and the conductor enables Build.

The baseline view sets the readable committed/reserve layout instantly even without these effects. `CommitManager` owns the committed IDs; `ScoreRevealJuice` or an announcement presenter owns the slam. A rejected commitment shows its reason without spending a hand.

## Phase 4 — Build

The player places only the committed cards. The remaining hand stays visible but cannot be placed in this hand.

- The preview card (`TowerPlacementInput`) and snap markers (`SnapMarkerView`) show legal placements even with all effects off.
- Optional marker glow and visual lag help guide placement.
- Accepted placement uses the real slot, independent of the delayed visual.
- Undo, remaining-card checks, and protection against impossible completion follow the Master Plan.

When all committed cards are placed, the core reports completion and the conductor advances to Score. Animation completion never decides whether a card was placed.

## Phase 5 — Score

The scorer evaluates the committed set once, after Build completes. The conductor adds that hand score to the **Pot**, then emits the scoring result. The reveal animates those already-decided values:

1. The top strip shows the evaluated hand name.
2. Scoring cards flash gold in placement order; their actual chip values float up, such as `+10` or `+9`.
3. The chips counter rolls upward as each scoring contribution is shown.
4. The multiplier display punches when its displayed value changes.
5. The final chips × multiplier result receives a scale flash and particle burst.
6. The score travels into the **Pot display**. It does not become Safe Points here.

**Today:** `ScoreManager` still plays a step-by-step reveal itself (`stepDelay`), and the conductor waits for it (`WaitForScoreReveal`). C2 moves that reveal into a juice script, makes ScoreManager instant, and has the conductor wait on `PresentationGate` instead.

**Integration:** `ScoreRevealJuice` / `ScoreTickerUI` reads an immutable score result. `FloatingNumber` handles optional popups. Plain HUD values are correct immediately when reveal effects are absent. The gate is bounded and skippable; skipping must neither lose nor duplicate points.

## Phase 6 — Shake and Resolution

The core releases the tower, waits using the Master Plan's bounded settling policy, and captures one authoritative result. Juice may play contact or break sounds during physics, but it does not compute penalties or decide collapse.

- **Breaks:** Visual debris may appear on `CardBroken`. Numeric Pot loss is animated only from the resolved Pot change, using the deduplicated result; repeated collision notifications never subtract extra points.
- **Collapse:** `TowerCollapsed` triggers Pot shatter and camera shake. Until that event exists, `ShakeResolved` with `Collapsed == true` means the same thing. The Pot HUD shows the core's final zero.
- **Survival:** The HUD shows the final standing-level bonus and Bank value from the captured result. Presentation cannot change those measurements.
- **Last hand:** The core automatically Banks only if the Pot survives the shake, then finishes the blind. Do not present a false Bank/Ride choice.

## Phase 7 — Bank / Ride and Blind Results

- **Bank:** Accepted `PotBanked` data drives chips from the Pot to Safe Points. A cosmetic tower sweep can use visual copies; the core clears occupancy immediately and is never delayed by moving real colliders.
- **Ride:** The accepted choice keeps the Pot and standing tower. The conductor begins the next hand. A full tower's disabled Ride explanation remains readable without effects.
- **After Bank:** If Leave Table is legal, show **Continue** and **Leave Table**. Do not automatically draw while this choice is pending.
- **Blind result:** The menu slide, win/fail cues, and Glass crack or repair effects react to resolved results. A skip advances according to the core rules; an animation cannot apply a second repair.
- **Run intro:** Cards fly in to form the deck during the optional intro gate. `GamePhase.RunIntro` exists but the conductor does not enter it yet; C8 adds that step. With effects off, the ready deck and Blind Menu appear immediately.

---

## Live Hand Preview

The normal HUD always shows the evaluated hand during Commit. Architectural names below are optional flavor labels alongside the poker name; they do not introduce new hand rules.

| Selection example | Optional shape name | Poker name |
|---|---|---|
| 1 card, or unmatched cards with no stronger hand | Loose Cards | High Card |
| 2 matching ranks | Tent | Pair |
| 4 cards forming two pairs | Twin Tents | Two Pair |
| 3 matching ranks | Tripod | Three of a Kind |
| 5 consecutive ranks | Staircase | Straight |
| 5 cards of one suit | Wall | Flush |
| 5 cards: three of one rank and two of another | House | Full House |
| 4 matching ranks | Tower Block | Four of a Kind |
| 5 consecutive ranks of one suit | Spire | Straight Flush |
| Same-suit 10–J–Q–K–A | Palace | Royal Flush |

The names are flavour only. A Pair is called "Tent" even though it does not have to be built as a tent on the tower (names changed 2026-11-17, see [HUD Design.md](HUD%20Design.md) §4).

The evaluator decides the strongest hand and scoring-card indices. Extra non-scoring cards do not become chip contributions because of an animation. Zero selected cards shows **"Select 1–5 cards"** rather than a fabricated hand.

**Optional name-change effect:** The old name shrinks briefly; the new one enters with an overshoot and short sound. The plain text must update immediately even if that effect is disabled or interrupted.

---

# Implementation Order

Agreed order after A6 (2026-11-16). The [Master Plan](Master%20Plan%20Real.md) still owns the gates and acceptance checks.

| Order | Work | Why here |
|---|---|---|
| 1 | Revise this plan | Done (revision 2). |
| 2 | **Track B** (B1–B5) | Done (2026-11-16). |
| 3 | **HUD design** | Done: [HUD Design.md](HUD%20Design.md). Building names and the live Commit preview are already in the code. |
| 4 | **C3** break particles, **C4** collapse camera shake | They animate the tower and the camera, not the HUD. Can be slotted in any time before step 6. |
| 5 | **HUD rebuild** with plain placeholder art, area by area (H1–H7 below) | Done (2026-11-18). The HUD-bound effects need to know which elements exist and where they sit. |
| 6 | **C1, C2, C5, C6, C7** on the new HUD | Each animates a HUD element: hand name, score texts, Pot and Safe displays, blind menu, Glass. |
| Alongside 5–6 | **Artist replaces placeholder art** | Brief: [HUD Art Assets.md](HUD%20Art%20Assets.md). Art drops into fixed slots, so it never blocks code work. |
| Later | **C8** run intro | Needs the full run and a `RunIntro` step in the conductor. |

### HUD rebuild steps (step 5)

| # | Area | Contents |
|---|---|---|
| H1 | Missing game values | ✅ Done (2026-11-17): `SafeAfterBank`, `BankReachesTarget`, `PointsOverTarget`, `HeightBonusPercent`, `LastShakeLossPercent`, `RewardMultiplierBeforeLastWin` (HUD Design §13.2) |
| H2 | Style setup | ✅ Done (2026-11-17): TMP font assets in `Assets/Fonts`, `HudTheme` asset (colours, fonts, dimming), 59 placeholder sprites in `Assets/Images/HUD` (menu: Tower of Cards > HUD > Make Missing Placeholders; never overwrites) |
| H3 | Left HUD + hand plaque | ✅ Done (2026-11-17): `LeftHudView`, `HandPlaqueView`, `GlassStarsView`, `PipRow` (in `Assets/Scripts/Hud`). Old `ScoresPanel`, `RunText`, `GlassText` deleted. The plaque never dims |
| H4 | Right HUD | ✅ Done (2026-11-17): `BlindPlaqueView`, `SafeBarView`, `PotSignView`, `BrokenStripView`, `BreakdownView`, `LadderView`, `RightHudView`. Old `TurnText`, `TotalScoreText`, `PotText` and the card-details `HintText` deleted. Hand centred at the bottom; deck moved to the bottom-right corner below the right HUD. Bank / Ride / Continue / Leave / main button sit left of the panel until H5 / H6 |
| H5 | Centre + deck + buttons | ✅ Done (2026-11-17): instruction line and banner pills (`HudView`), `PotTagView`, `DeckHudView` (count badge, swap pips, blue glow, hover tooltip), Play hand / Done swapping and Undo buttons under the hand. Hand raised slightly to make room. Old `StateText`, `CardsText`, `DeckText`, `DiscardText`, `BannerText`, `EndTurnButton` deleted |
| H6 | Cashier drawer | ✅ Done (2026-11-17): drawer under the right HUD (`BankRideView` rewired: receipt line, BANK / RIDE keys, "Tower full"). Appears instantly; the slide is C5. Old `BankButton` / `RideButton` deleted |
| H7 | Full-screen panels | ✅ Done (play-tested 2026-11-18): Blind menu (`BlindMenuView`, upcoming blinds not dimmed), Continue / Leave (`ContinueLeaveView`), Blind result (`BlindResultView`, keeps the auto timer, no Next button), Run end (`RunEndView` texts inside the existing marquee art), Run Info (`RunInfoView`), Options mock-up (Back only). Run Info / Options use `HudPopup`: they block table clicks but do not pause. Panels slide up from the bottom (`PanelSlideJuice`) with no dark backdrop |
| ~~H8~~ | ~~Panel focus~~ | Dropped 2026-11-17: no dimming anywhere in the HUD |

**Rules for the rebuild:**

- **Replace one area at a time, never the whole HUD at once.** Build the new area beside the old texts, play and check the numbers, then delete the old texts it replaces and commit. The game stays playable after every step.
- **Code never depends on the art.** Each element is a prefab with fixed child names (for example `PotSign/Frame`, `PotSign/Label`, `PotSign/Number`, `PotSign/Line`). Scripts reference texts and roots, never a sprite, so the artist swaps the sprite in `Frame` and nothing else changes.
- **Animations move a wrapper, not the art.** Effects tween a `Visual` child (the same rule as the hand cards), so new art inside it cannot break a tween.
- **Placeholders use the final sizes** from [HUD Art Assets.md](HUD%20Art%20Assets.md), with stretchable sprites (9-slice), so swapping the art does not move the layout.
- **Small views, not one big** `HudView`**:** for example `LeftHudView`, `HandPlaqueView`, `PotPanelView`, `CashierView`, `DeckHudView`, `LadderView`. `HudView` keeps the instruction line, banners and main buttons.
- **Done when:** every juice component is off and a full run still shows every number correctly.

Track B notes: B4 needs plain drag-to-reorder input (a `HandManager` reorder command) before any reorder effect. B3 is the first effect that holds `PresentationGate`, so it also adds the conductor's wait on the gate after a draw (the gate exists but nothing waits on it yet).

## Events the effects need

Effects listen to `GameEvents`. Each missing event is added by the step that first needs it, in the same commit, and is nulled in `ClearAllListeners`.

| Event | Status | Used by | Added in |
|---|---|---|---|
| `HandChanged`, `SelectionChanged` | ✅ exists | B1, B3, C1 | — |
| `CardBroken(Card)` | ✅ exists (A6) | C3, C4 | — |
| `ShakeResolved`, `PotChanged`, `PotBanked`, `TableLeft` | ✅ exists | C3, C5 | — |
| `BlindOffered`, `BlindSkipped`, `BlindStarted`, `BlindWon`, `BlindFailed`, `RunEnded` | ✅ exists | C6 | — |
| `GlassChanged` | ✅ exists | C7 | — |
| `CardsDrawn(card IDs)` | ✅ exists (B3) | B3, 6.2 | B3 |
| `SwapRejected(reason)` | ✅ exists (B5) | 6.2 | B5 |
| `TowerCollapsed(ShakeResult)` | ✅ exists (C4) | C4, C3 Pot shatter | C4 |
| `HandCommitted(card IDs, hand)` | ❌ missing | C1 | C1 |
| `HandScored(score result)` | ❌ missing: only `ScoreManager.OnScoreStep` / `OnTurnScored` | C2 | C2 |

**Acceptance check:** Disable all optional juice components, play through selection, swaps, reorder, commit, build, score, Bank/Ride, last-hand resolution, a blind result, and replay. Inputs, totals, and transitions must still work. Re-enable effects and verify that skipping or interrupting them produces exactly the same results.

---

# Component Responsibilities

| Component | Responsibility |
|---|---|
| `GameFlowController` | Core conductor defined by the Master Plan (`TurnManager` was deleted in A6). |
| `HudView` | Plain, instant HUD texts and buttons (was `GameUI`). HUD juice animates on top of it. |
| `TowerPlacementInput` / `SnapMarkerView` | The aiming preview card and the snap markers (split out of `TowerBuilder` / `GameUI` in A6). |
| `SwapManager` / `SwapZone` | Core multi-card exchange and the deck click; no animation ownership. |
| `CommitManager` | Core accepted commitment and live hand result. |
| `HandManager` / normal `HandCardView` | Card identity/order, validated input, and usable base layout. |
| `HandCardJuice` / hand presentation component | Visual hover, punch, tilt, fan offsets, and movement effects. |
| `DrawAnimator` | Presentation of accepted draws and replacements. |
| `DeckGlowBehavior` / `DeckFeedback` | Visual availability, arrival/departure, and rejection feedback. |
| `CardTooltip` | Displays actual card information; fade is optional. |
| `ScoreRevealJuice` / `ScoreTickerUI` | Displays an already-computed score; never awards it. |
| `FloatingNumber` | Optional chip-value popups. |
| `PotJuice` / `CameraShaker` | Resolved Pot changes, banking, and collapse effects. |
| `GlassCrackJuice` / `MenuSlideJuice` | Resolved lives and menu transitions. |

Names are implementation suggestions. Combine small presentation helpers when that stays clear; do not create a class solely to match this table.

---

# Revision notes

**Revision 3 (2026-11-17), after the HUD design.**

- Track B and the HUD design marked done. Building names changed to Tent, Twin Tents, Tripod, Wall.
- Implementation order now has the HUD rebuild steps (H1–H8), the replace-one-area-at-a-time rule, and the artist working alongside from [HUD Art Assets.md](HUD%20Art%20Assets.md).
- `CardsDrawn` and `SwapRejected` marked as existing.

**Revision 2 (2026-11-16), after A6.**

- Swap is up to 3 cards in one deck click (built in A2), not one card at a time. Phase 2 and rule 2.2 rewritten.
- Script names follow A6: `HudView` (was `GameUI`), `SwapManager` / `SwapZone` (were `DiscardManager` / `DiscardZone`), `TowerPlacementInput` and `SnapMarkerView` (split out of `TowerBuilder` / `GameUI`), `GameEvents.CardBroken` (was `Card.Broken`).
- Added [Events the effects need](#events-the-effects-need): which events exist, and which step adds each missing one.
- C2 is described as moving the existing reveal out of `ScoreManager`.
- New implementation order: Track B, C3 and C4 first; then the HUD update; then the HUD-bound effects C1, C2, C5–C7.
- Links point to `Master Plan Real.md`.
