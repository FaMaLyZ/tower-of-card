# Neo Tower of Cards — HUD Design

> **Status:** Agreed design from the clickable mockup (2026-11-17). Replaces the layout in [HUD Summary.md](HUD%20Summary.md), which stays as the record of what exists today.
> **Mockup:** https://claude.ai/artifact/5sbRmxB3G6RhVnQ9VMZyaz (use the phase bar at the top to step through every phase).
> **Purpose:** Step 3 of the juice order in [juice-plan.md](juice-plan.md). Build these displays plain and instant first; Track C (C1, C2, C5, C6, C7) animates them afterwards.
> **Rules this design keeps:**
> 1. The HUD only *shows* the game. Every number comes from `GameFlowController` or a worker. The HUD never redoes rule maths.
> 2. Buttons send commands. Displays never look like buttons (see §2.4).
> 3. Every value is correct and readable with every effect switched off. Animations play on top of the correct number.

---

## Contents

1. [Screen map](#1-screen-map)
2. [Style guide](#2-style-guide)
3. [Left HUD — run status](#3-left-hud--run-status)
4. [Top HUD — hand plaque](#4-top-hud--hand-plaque)
5. [Right HUD — the Pot and the decision](#5-right-hud--the-pot-and-the-decision)
6. [Tower height ladder](#6-tower-height-ladder)
7. [Centre — instruction line, banners, Pot tag](#7-centre--instruction-line-banners-pot-tag)
8. [Deck](#8-deck)
9. [Player hand](#9-player-hand)
10. [Full-screen panels](#10-full-screen-panels)
11. [How the numbers move between sections](#11-how-the-numbers-move-between-sections)
12. [Phase by phase](#12-phase-by-phase)
13. [What changes in the code](#13-what-changes-in-the-code)
14. [Decisions](#14-decisions-were-open-questions)

---

## 1. Screen map

The whole screen, as blocks only. Each section has its own detailed wireframe in its chapter.

```
+--------------+   +------------------------------+   +--------------+
|              |   |   TOP HUD  (hand plaque)     |   |              |
|   LEFT HUD   |   +------------------------------+   |  RIGHT HUD   |
|              |                                      |              |
|  run status  |            TOWER           o  ladder |  blind, safe |
|              |                            o         |  pot, broken |
|              |                                      |  breakdown   |
|              |      [ instruction line ]            |              |
+--------------+                                      +--------------+
                  PLAYER HAND  (8 cards)       DECK      CASHIER DRAWER
                  [ action buttons ]                     (Bank / Ride only)
```

| Section | Job | Where |
|---|---|---|
| Left HUD | "How is my run going?" Run score, turn score, ante, blind, hands, glass | Left column |
| Top HUD | "What hand am I playing and what is it worth?" | Top centre |
| Right HUD | "What is at risk and what is it worth right now?" The Bank or Ride decision | Right column |
| Tower ladder | How many standing levels, and the height bonus | Between the tower and the right HUD |
| Centre | What to do now, big announcements | Middle of the table |
| Deck | Draw pile and swapping | Bottom right |
| Player hand | The 8 cards, selecting, placing | Bottom centre |
| Full-screen panels | Blind menu, Continue / Leave, blind result, run end, Run Info | Over everything |

There is **no top strip** any more. Everything it used to hold (Ante, hands, run, glass, blind name, reward) now lives in the left HUD or the blind plaque.

---

## 2. Style guide

**Theme:** Art Nouveau casino, with Balatro-style chunky numbers. Matches the existing menu art (`main.png`, `Score Panel.png`, `ScoreSum.png`).

### 2.1 Colours

| Use | Colour |
|---|---|
| Table background | Dark navy `#0D0C1D` with a teal felt glow `#1F4A46` under the tower |
| Panel fill | Navy gradient `#22203F` → `#191730` |
| Gold (frames, labels, bulbs) | `#D9A441`, light gold `#F2D58A`, pale gold text `#FFE3A0` |
| Cream (text on dark) | `#F4E6C8`, muted cream labels `#D8C49B`, dim labels `#BFA77A` |
| Chips (blue) | `#2F6FD6` |
| Mult (red) | `#D8452F` |
| Base chips (dark chip) | `#1C1A33` |
| Safe / good (green) | `#7DFFB6`, bar gradient `#2FA66A` → `#7DFFB6` |
| Danger / penalty (salmon) | `#FF9C8F` |
| Glass (red stained glass) | Whole `#D8322A`, cracked `#4A1512` |
| Swap selection (blue) | `#5B9CFF` |
| Commit selection (gold) | `#FFCF4D` |
| Blind colours | Small = blue `#2F6FD6`, Big = gold `#C8902F`, Boss = red `#C8322B` |

### 2.2 Fonts

| Use | Font |
|---|---|
| Big titles and names ("Scores", "Wall", "Small Blind", "Won!") | **Cinzel Decorative**, Black (900) |
| Labels ("TURN SCORE", "ANTE", "Broken") | **Cinzel**, Bold, small caps feel, letter-spaced |
| All numbers and buttons | **Pixelify Sans** (pixel font, like the NEO logo and Balatro) |

### 2.3 Panel frame (left HUD, right HUD, pop-up panels)

- Arched top: the top corners are wide ovals (the Art Nouveau arch), the bottom corners are small rounded corners.
- 2 px gold outer line, a thin navy gap, then a faint gold inner line (a double frame).
- A whiplash curl ornament in each top corner (gold line art with a small dot).
- Soft dark drop shadow so the panel floats above the table.

### 2.4 Displays vs buttons (important)

Players must never mistake a number display for a button.

| | Displays (readouts) | Buttons |
|---|---|---|
| Shape | Recessed well: very dark fill `#100E22`, inner shadow, thin gold (or silver) rim | Raised: bright fill, light top edge, thick dark "3D" edge underneath |
| Shadow | None outside, shadow *inside* | Drop shadow outside |
| On press | Nothing | Pushes down 3–5 px |
| Examples | Turn Score, Ante, Blind, Hands, Safe, Broken | Run Info, Options, Done Swapping, Play Hand, Undo, Bank, Ride, Select, Continue |

Marquee bulb boxes (Run Score, the Pot) are **signs**, not buttons: gold frame with a ring of dotted light bulbs.

---

## 3. Left HUD — run status

**Wireframe** (left = start of a blind, right of `>` = after the change)

```
 _______________________
| o  o  o  o  o  o  o  o|
| RUN                  0|   >  after Bank           RUN               336
| SCORE                 |                           SCORE
| o  o  o  o  o  o  o  o|
 -----------------------
 _______________________
| TURN                 -|   >  Score phase          TURN              260   +260 ^
| SCORE                 |                           SCORE
 -----------------------
 ___________  ___________
| ANTE   1/4 || BLIND 1/3|  >  next blind           ANTE 1/4 | BLIND 2/3
 -----------  -----------
 _______________________
| HANDS  * * * *  4 left|   >  Play Hand accepted   HANDS  . * * *  3 left
 -----------------------
 [ Run Info ] [ Options ]       (raised buttons)

          GLASS
    <*>   <*>   <*>          >  blind failed           <*>   <*>   <x>
   whole whole whole                                  whole whole cracked
```


A tall arched panel on the left, top to bottom: Run Score, Turn Score, Ante | Blind, Hands, Run Info | Options, Glass.

Panel focus (§12) lights it fully in Commit, Score and Bank / Ride. In other phases it dims to about 70% so the active area stands out. Numbers must stay readable when dimmed (Rule 3), so never go lower.

### 3.1 Run Score

**Look**
- A marquee sign: gold frame with a ring of small white bulbs, dark navy inside.
- On the left, written on two lines: **RUN** on top, **SCORE** under it, in gold Cinzel Decorative.
- On the right, the number, big (about 44 px in the mockup), in cream pixel font with a soft glow.

**What it shows**
- The **Run total**: every point banked in the whole run, including from blinds you failed. This is the final score.
- It never goes down and never resets between blinds.

**When it changes**
- On **Bank**: goes up by the Bank payout (the same amount that goes into Safe).
- On **Leave Table**: goes up by the leave bonus.
- On **automatic Bank** at the end of the last hand.
- Nothing else changes it. Scoring a hand does *not* change it (that goes into the Pot first, see §11).

**Animation (Track C5)**
- Counts up from the old value to the new one, ticking upward, after the chips arrive from the Pot.
- The bulbs chase around the frame while it counts, then all flash once when it stops.

### 3.2 Turn Score

**Look**
- A display well (§2.4).
- On the left, on two lines: **TURN** on top, **SCORE** under it, in muted-gold Cinzel.
- On the right, the number in big pale-gold pixel font.

**What it shows**
- What the current hand scored: `(base chips + card chips) × mult`.
- Shows **–** until the hand is scored.

**When it changes**
- **Score phase:** shows the final hand score (260 in the mockup) once the count finishes. A green **+260** floats up from the right edge and flies to the Pot sign (§11).
- Stays visible through Shake and Bank / Ride so the player remembers what the last hand earned.
- **Next Draw:** back to **–**.

**Animation (Track C2)**
- Rolls up as the plaque's chips × mult resolves, then punches (scale 1 → 1.15 → 1) when it lands.
- The floating "+260" travels toward the Pot (§11).

### 3.3 Ante | Blind

**Look**
- Two display wells side by side, each half the panel width, with a silver rim (to echo the silver "CHIPS" plate in `Score Panel.png`).
- Left well: label **ANTE**, value **1/4**.
- Right well: label **BLIND**, value **1/3**.

**What it shows**
- Ante: the current round of three blinds, out of 4.
- Blind: which blind of this ante: 1 = Small, 2 = Big, 3 = Boss. (The blind's name and colour are on the blind plaque, §5.1.)
- Final-Boss retry: the Blind well shows **3/3 R** (or "Retry") so the player knows it is the retry.

**When it changes**
- When a new blind starts (after Select). Ante changes after a Boss blind ends.

### 3.4 Hands

**Look**
- A display well. Label **HANDS** on the left, four round pips in the middle, **"3 left"** on the right in gold.
- A lit pip is a glowing gold ball. A spent pip is dark with a gold rim.

**What it shows**
- How many hands remain in this blind, out of 4.

**When it changes**
- A pip goes dark the moment **Play Hand** is accepted (the rules spend the hand there, not at Draw).
- All four relight when a new blind starts.

**Animation**
- The spent pip pops and fades its glow over about 0.2 s.

### 3.5 Run Info | Options

**Look**
- Two raised buttons side by side (§2.4): red **Run Info**, gold **Options**.

**Run Info** opens the Run Info panel (§10.5), with the hand table and how card chips work.
**Options** opens the pause / settings menu. Its contents are not designed yet (see §14).

They can be used in any phase. The game does not advance while either panel is open.

### 3.6 Glass

**Look**
- Small label **GLASS** (salmon) above three large **red stained-glass stars** in a row.
- Each star has gold "lead" outlines inside it, like a stained-glass window, and a white shine.
- **Whole:** bright red, with a soft red glow.
- **Cracked:** dark wine red, no glow, faint shine, and a white zig-zag crack across it.

**What it shows**
- Lives. 3 stars = 3 lives. A cracked star is a lost life. The mockup shows 1 cracked.

**When it changes**
- **Blind failed:** the rightmost whole star cracks.
- **Skip a blind** or **beat a Boss:** the leftmost cracked star is repaired (up to 3).
- All 3 cracked: the glass shatters and the run ends.
- A collapsing tower **never** cracks the glass.

**Animation (Track C7)**
- Crack: the star shakes, the crack line draws across it, red shards fly off, the glow dies.
- Repair: the crack line fades, the star refills with red from the bottom and its glow returns.
- "The Glass cracked!" / "The Glass was repaired" (today's `GlassNoteText`) can appear under the stars for 2.5 s.

---

## 4. Top HUD — hand plaque

**Wireframe**

```
 ______________________________________________________________
| Your hand                    ( - )  +  ( - )  x  ( - )        |   Draw / Swap
| Select 1-5 cards                                              |   (dim)
 --------------------------------------------------------------
                               v   Commit, cards picked (live)
 ______________________________________________________________
| WALL                         ( 35 )  +  ( 30 )  x  ( 4 )      |
| Flush                         base     cards      mult        |
 --------------------------------------------------------------
                               v   Score
 ______________________________________________________________
| WALL                         ( 35 )  +  ( 30 )  x  ( 4 )  = 260
| Flush                                                         |
 --------------------------------------------------------------
```


A wide horizontal plaque at the top centre of the screen.

**Look**
- Gold outer frame, then a dotted cream line, then a royal-blue fill (`#3148BD` → `#1D2A78`).
- Small gold whiplash curls in the top-left and bottom-left corners.
- **Left side, two lines:**
  - Top: the **building name** of the hand, big, gold, Cinzel Decorative (for example **Wall**).
  - Under it: the **poker name**, small, white, Cinzel (for example **Flush**).
- **Right side, three casino chips in a row:**
  - Dark chip = **base chips** of the hand type (35).
  - **+**
  - Blue chip = **card chips** from the scoring cards (30).
  - **×**
  - Red chip = **mult** (4).
  - Each chip has a white dashed edge like a real casino chip. The number sits in the middle in gold pixel font with a black outline.
- During Score only: **= 260** in green appears at the end of the row.

**Building names** (from the juice plan, plus Palace):

| Poker name | Building name | Base chips | Mult |
|---|---|---|---|
| High Card | Loose Cards | 5 | 1 |
| Pair | Tent | 10 | 2 |
| Two Pair | Twin Tents | 20 | 2 |
| Three of a Kind | Tripod | 30 | 3 |
| Straight | Staircase | 30 | 4 |
| Flush | Wall | 35 | 4 |
| Full House | House | 40 | 4 |
| Four of a Kind | Tower Block | 60 | 7 |
| Straight Flush | Spire | 100 | 8 |
| Royal Flush | **Palace** | 100 | 8 |

Card chips: 2–10 give their number, J / Q / K give 10, Ace gives 11. Only scoring cards count; kickers add nothing.

**By phase**

| Phase | Shows | Brightness |
|---|---|---|
| Draw, Swap | "Your hand" / "Select 1-5 cards", chips show **–** | Dim (50%) |
| Commit, 0 cards picked | Same as above | Full |
| Commit, cards picked | Live preview: updates instantly on every pick and unpick | Full |
| Build | The committed hand, locked | 85% |
| Score | The committed hand plus **= 260** | Full |
| Shake, Bank / Ride | The last scored hand plus **= 260** | Dim (60%) |
| Blind menu, results, run end | Hidden | — |

**Animation (Track C1 / C2)**
- Name change (Commit): the old name shrinks out, the new one pops in with a small overshoot and a click sound. The plain text updates at once even if the effect is off.
- Score: the base chip punches, then the card chips count up as each scoring card flashes gold, then the mult chip punches, then "= 260" slams in with a flash.

---

## 5. Right HUD — the Pot and the decision

**Wireframes**, one block per part, with how it changes.

Blind plaque (changes only between blinds):
```
 ___________________          ___________________          ___________________
|    SMALL BLIND    |        |     BIG BLIND     |        |    BOSS BLIND     |
|  score at least   |   >    |  score at least   |   >    |  score at least   |
|     (o) 150       |        |     (o) 225       |        |     (o) 300       |
|   Reward x1.2     |        |   Reward x1.4     |        |   Reward x1.6     |
 -------------------          -------------------          -------------------
     blue                         gold                         red
```

Safe bar (only Bank and Leave Table change it):
```
 Safe          0 / 150     >   Bank / Ride (preview)    >   after Bank
 [..........]                  Safe       0 / 150           Safe      336 / 150
                               [////////////]  striped      [############]  gold
                                                            +186 over
```
Scoring a hand does **not** change Safe. It stays 0 / 150 until the player Banks.

Pot sign (changes every phase):
```
 ___________________        ___________________        ___________________
|Riding on the tower|      |        POT        |      |  POT (unsettled)  |
|        52         |  >   |        312        |  >   |        312        |
| Rode from Hand 1  |      |52 + 260 this hand |      |  settling...      |
 -------------------        -------------------        -------------------
   Draw ... Build               Score                      Shake
                                                               v
                            ___________________        ___________________
                           |        POT        |      |     BANK NOW      |
                           |         0         |  <   |        336        |
                           |   Tower cleared   |      |312 - 1 broken = 280|
                            -------------------        -------------------
                              after Bank                   Bank / Ride
```

Broken strip:
```
 | Broken   none          |   >  Shake    | Broken  [/] (flicker)   -10% |   >  Bank / Ride  | Broken  [/]   -10% |
```

Breakdown rows:
```
 Draw ... Score      Shake                         Bank / Ride
 (empty)        >    Breaks       -10% each   >    Raw pot              280
                     Collapse      lose all        Height (2 levels)   +20%
                                                   Reward              x1.0
```

Cashier drawer (Bank / Ride only):
```
   (hidden behind panel)    >     ______________________________
                                 |  o        CASHIER          o  |
                                 |  ----------------------------  |
                                 |  SAFE 0 -> 336  (v) TARGET     |
                                 |  ----------------------------  |
                                 |  [  BANK  ]      [  RIDE    ]  |
                                 |  [  336   ]      [build high]  |
                                  \____________________________/
                                   slides down, bounces, closes on choice
```


A tall arched panel on the right, top to bottom: Blind plaque, Safe bar, Pot sign, Broken strip, Breakdown. The cashier drawer (§5.6) slides out under it.

It is fully lit in Score, Shake and Bank / Ride, and dims in the other phases. In Bank / Ride it also gets a warm gold glow around the frame.

### 5.1 Blind plaque

**Look**
- A rounded plaque filled with the blind's colour (Small blue, Big gold, Boss red), a gold rim, a dotted cream inner line and a soft glow in the same colour.
- Line 1: blind name, Cinzel Decorative, cream (**Small Blind**).
- Line 2: small label **SCORE AT LEAST**.
- Line 3: a small gold chip icon and the target, big, gold pixel font (**◎ 150**).
- Line 4: **Reward ×1.2** (the multiplier won by beating this blind).

**When it changes:** only when a new blind starts.

**Targets:** the examples use the Ante 1 targets from `GameConfig`: Small 150, Big 225, Boss 300. Each Ante multiplies them by 1.5. These will be raised after playtesting, so the HUD always shows `Target` from the game and never a fixed number.

### 5.2 Safe bar

**Look**
- Label **Safe** on the left. On the right, the Safe number in green, then **/ 150** in dim gold.
- Under it, a thin bar with a gold rim. The green fill shows Safe ÷ target.

**What it shows:** the Blind score, which is points banked in *this* blind and the only score that counts toward the target.

**Details**
- **Bank / Ride:** a striped green "ghost" fill shows where the bar *would* reach after Bank (Safe after Bank).
- **Target met:** the bar turns gold and a small **"+186 over"** tag appears when Safe goes past the target.
- **New blind:** resets to 0.

**Animation (Track C5):** on Bank the number counts up and the bar fills smoothly as the chips arrive.

### 5.3 Pot sign

**Look:** a marquee sign (gold frame with light bulbs). Inside, three lines, centred:
1. Small label (changes by phase)
2. Big number, gold pixel font
3. A small explanation line

**By phase**

| Phase | Label | Number | Explanation line |
|---|---|---|---|
| Blind menu | Pot | 0 | Tower cleared |
| Draw, Swap, Commit, Build (after a Ride) | Riding on the tower | 52 | Rode from Hand 1 |
| Draw … Build (fresh tower) | Pot | 0 | Tower cleared |
| Score | Pot | 312 | 52 + 260 from this hand |
| Shake | Pot (unsettled) | 312 | The tower is settling... |
| Bank / Ride | **Bank now** | **336** | 312 − 1 broken card = 280 |
| Continue / Leave, Blind result, Run end | Pot | 0 | Tower cleared |

In Bank / Ride the big number is the **Bank payout** (`BankQuote`), not the raw Pot, because that is what the player is deciding about.

### 5.4 Broken strip

**Look**
- A display well with a salmon rim. Label **Broken** on the left.
- In the middle, one small **cracked card icon** per broken card: a red card back split by a white diagonal crack, tilted slightly.
- On the right, the penalty in salmon: **−10%** per card (−10% for 1, −19% for 2 because breaks multiply: 0.9 × 0.9).
- When nothing broke: the word **none** in green.

**What it shows:** cards destroyed in the **last shake**. Each costs 10% of the remaining raw Pot.

**By phase**
- Before the shake: shows the previous shake's result (usually "none").
- **Shake:** each card that breaks pops in as an icon and flickers until the shake settles. The penalty updates at the end, once the result is final.
- **Bank / Ride:** the final count (1 card, −10% in the mockup).

**Animation (Track C3):** each icon pops in with a small burst of card fragments, timed with the break particles on the tower.

### 5.5 Breakdown rows

A small list under the Broken strip. Name on the left (cream Cinzel), value on the right (pixel font, coloured).

| Phase | Rows |
|---|---|
| Draw, Swap, Commit, Build, Score | *(empty, the Pot sign says enough)* |
| **Shake** | **Breaks −10% each** (salmon) · **Collapse lose all** (salmon), a reminder of what the shake can cost |
| **Bank / Ride** | **Raw pot 280** (gold) · **Height (2 levels) +20%** (green) · **Reward ×1.0** (salmon) |
| Pot empty phases | *(empty)* |

The Bank / Ride rows explain the payout: `floor(280 × (1 + 20%) × 1.0) = 336`. The HUD shows the values the core gives it and never computes them.

### 5.6 Cashier drawer (Bank / Ride)

**Look**
- A cash-register drawer that hangs below the right HUD panel.
- Outside: red lacquer (`#B5352B` → `#7A1F19`), gold trim, a deep rounded bottom, two brass rivets and a small **CASHIER** plate across the top.
- Inside: a cream tray with an inner shadow, holding:
  - A **receipt strip** between two dashed lines: `SAFE 0 → 336 ✓ TARGET` (dark brown pixel font).
  - Two chunky **register keys** side by side:
    - Gold key: **BANK** / **336**.
    - Red key: **RIDE** / **build higher**.
  - The keys press down 5 px when clicked.

**When it shows**
- **Only in the Bank / Ride phase.** It slides down from behind the panel with a small bounce (about 0.5 s) and tucks back up as soon as the player chooses.
- It never appears after the last hand (the last hand Banks automatically, so there is no false choice).

**States**
- **Tower full:** the Ride key is greyed out and its second line reads **"Tower full"**, and the receipt reads **"Tower full — Bank to continue"**.
- **Target not met after this Bank:** the receipt shows `SAFE 0 → 100 / 150` with no tick.

**Animation (Track C5):** Bank = drawer bell "ding", chips fly from the Pot sign to the Safe bar and the Run Score, then the drawer closes. Ride = the drawer closes and the tower ladder bulbs pulse once.

---

## 6. Tower height ladder

**Wireframe**

```
 After a Ride        Shake                   Bank / Ride             Collapse
      o                  o                       o                       o
      |                  |                       |                       |
      o                  o                       o                       o
      |                  |                       |                       |
      o              ~ * ~  settling...  Level 2 *  +20% height          o
      |                  |                       |                       |
Lv 1  *  +10%            *                       *                       o
```
`*` = lit gold dot, `o` = unlit, `~*~` = flickering.


**Look**
- A thin vertical gold line hanging off the left edge of the right HUD, near the bottom.
- **4 dots** on the line, Level 1 at the bottom and Level 4 at the top.
- **Lit dot:** glowing gold ball. **Unlit dot:** navy with a gold ring.
- Only the **highest lit level** has a label to its left, on two lines: **Level 2** (gold Cinzel, glowing), then **+20% height** (green pixel font).

**What it shows:** standing levels of the tower. Each one adds +10% to the Bank payout.

**By phase**

| Phase | Dots |
|---|---|
| Draw … Score (after a Ride) | The levels that survived last time (1 in the mockup) |
| **Shake** | Confirmed levels lit, the next one **flickers**, label reads **"settling..."** |
| Bank / Ride | Final levels (2 in the mockup), label **Level 2 / +20% height** |
| Collapse | All dots go dark, one after another, top to bottom |
| Bank (tower cleared), blind menu, results | All dark, the ladder dims |

---

## 7. Centre — instruction line, banners, Pot tag

### 7.1 Instruction line

A pill just above the hand: dark fill, gold rim, cream Cinzel Bold. It replaces today's `StateText` **and** `HintText`.

| Phase | Text |
|---|---|
| Draw | Dealing your hand... |
| Swap | Swap: pick up to **3** cards, then click the deck (the number follows swaps left) |
| Commit, 0 picked | Pick 1-5 cards for your poker hand |
| Commit, cards picked | Play hand to commit **5** cards |
| Build | Place your cards: **3** left |
| Score | Scoring... |
| Shake | The tower shakes! |
| Bank / Ride | Bank the pot to make it safe, or Ride and build higher (in gold) |
| Refusals | The reason, in salmon, for 2 s (e.g. "No swaps left this hand", "Those cards can't all fit on the tower") |

### 7.2 Banners

Big centre text in a dark pill with a gold rim, Cinzel Decorative, glowing gold. Placed below the hand plaque so they don't cover it.
- Draw: **Hand 2**
- Shake: **Hand 2 complete**

### 7.3 Pot tag on the tower

A small dark pill with a gold rim, floating above the tower: **◎ 52**. It shows the raw Pot that is physically "sitting on the tower". It is visible whenever the Pot is not 0 during a hand, and moves up as the tower grows.

---

## 8. Deck

**Wireframe**

```
     ______                ______                    ______
    |\\\\\|              |\\\\\|  (blue glow)      |\\\\\|
    | (39) |      >       | (39) |         >        | (36) |
    |\\\\\|              |\\\\\|                  |\\\\\|
     ------                ------                    ------
  SWAPS * * *          SWAPS * * *              SWAPS . . .
   Draw / Commit       Swap, cards picked       after swapping 3
```


**Look**
- A stack of face-down cards at the bottom right: red diagonal-stripe card back, gold edge, two offset darker cards underneath to look like a pile.
- A **count badge** in the middle: dark pill with a gold rim, the number of cards left in the draw pile (**39**).
- Under the deck: label **SWAPS** and **3 blue pips** (one per swap left this hand).

**Behaviour**
- **Swap phase, cards picked:** the deck pulses with a blue glow, meaning "click me".
- **Click in Swap:** the picked cards are exchanged in one go. Swap pips go dark (one per card), the count goes down, and new cards fly into the hand.
- **Click when not allowed:** the deck shakes sideways and flashes red, and the instruction line shows the reason (the `SwapRejected` event already exists and `HudView` already shows the reason).
- **Hover:** a small tooltip, **"Deck 39 · Discard 5"**. The discard count moves here because it no longer has its own text.
- **Bank / Ride:** the cashier drawer covers the deck, which is fine because the deck isn't used then.

---

## 9. Player hand

**Wireframe**

```
 Swap / Commit                               Build
        ___                                   [8h][10h][2h][4h][6h]   <- committed tray
   ___ |8h | ___                                 ^dim ^dim ^next(gold ring)
  |Kc ||   ||4h |  ... 8 cards in a fan          ___ ___ ___
  picked card lifts up                           |Kc||8s||Qd|   <- rest of hand, dimmed
  (blue ring in Swap, gold in Commit)
        [ Done swapping ]  /  [ Play hand ]                  [ Undo ]

  Hover a card:   ________________________
                 | Eight of Hearts        |
                 | Weight 8 . Chips 8     |
                  ------------------------
```


### 9.1 Cards
- Up to **8 cards** in a gentle fan at the bottom centre, each tilted a little (about 2.6° per step from the centre) and lowered slightly toward the ends to form an arch.
- Card face: cream with a gold rim. Rank top-left in pixel font, small suit under it, big suit bottom-right. Hearts and Diamonds red, Clubs and Spades navy.

### 9.2 Selecting
| Phase | Click a card | Look of a picked card | Limit |
|---|---|---|---|
| Swap | Pick / unpick for swapping | Lifts 26 px, **blue** ring | Swaps left (max 3) |
| Commit | Pick / unpick for the poker hand | Lifts 26 px, **gold** ring | 5 |
| Other phases | Nothing | — | — |

Unpicking one card never clears the others.

### 9.3 Card tooltip
On hover (Swap and Commit), a small dark box with a gold rim appears just above the card:
- Line 1: **Eight of Hearts** (gold Cinzel)
- Line 2: **Weight 8 · Chips 8** (pixel font). Weight is a whole number: 2-10 their number, J/Q/K 10, Ace 1.

This is today's `CardTooltip`. The bottom `HintText` card details are removed (the HUD Summary flagged them as duplicates).

### 9.4 Build: the committed tray
- After **Play Hand**, the committed cards leave the fan and line up in a small tray just above it. The rest of the hand stays in the fan, dimmed (they can't be placed this hand).
- In the tray, already-placed cards are dimmed, and the **next card to place has a gold ring**.
- The tower shows snap markers: a **green glowing dot** where the card will land, and white dots for other free slots.

### 9.5 Action buttons
Raised buttons centred under the hand. Only the ones that apply are shown:

| Phase | Button | Style | Notes |
|---|---|---|---|
| Swap | **Done swapping** | Navy | Moves to Commit |
| Commit | **Play hand** | Blue | Greyed out when no cards are picked |
| Build | **Undo** | Navy | Takes back this hand's last placement; greyed when nothing to undo (right-click still works) |

---

## 10. Full-screen panels

All use the arched panel frame (§2.3) over a dark see-through backdrop.

### 10.1 Blind menu (before every blind)
- Title **Ante 1 / 4** (gold Cinzel Decorative, glowing).
- Three arched panels side by side: Small, Big, Boss.
  - Status at the top: **Current** (green) / **Upcoming** / **Won** / **Failed** / **Skipped**.
  - Name in Cinzel Decorative (Boss name in salmon).
  - A big casino chip in the blind's colour with the target inside (**150**, **225**, **300** at Ante 1).
  - "Score at least 150", then **Reward ×1.2**.
  - Current blind: **Select** (gold) and **Skip · repair 1 crack** (navy) buttons. If the glass is whole, Skip reads "Skip (Glass is whole)".
  - Upcoming blinds are dimmed (75%) with a note: "Can be skipped" or "No skip · win repairs 1 crack" (Boss).
- Animation (C6): slides up from the bottom.

### 10.2 Continue / Leave (after a Bank that reaches the target with hands left)
- Title **Target reached** (green).
- **Safe 336 / 150**.
- "2 hands left. Keep playing for more, or leave now for a sure bonus."
- Buttons: **Continue** (blue) and **Leave table +30** (gold). The bonus is target × 10% × hands left = 150 × 10% × 2.

### 10.3 Blind result
- Small label with the blind name, then **Won!** (gold, glowing) or **Failed** (salmon).
- **366 / 150**
- "Banked 336 · Left the table +30"
- **Reward ×1.0 → ×1.2** (on a win), or the glass crack (on a fail).
- Button **Next blind**.

### 10.4 Run end
- **Run cleared** or **Glass shattered**.
- The Run Score in a marquee sign.
- "Ante 4 / 4 · Boss blind · Glass 2 / 3".
- Buttons **Main menu** (navy) and **Play again** (red).

### 10.5 Run Info
- Title **Run Info**.
- Section **Poker hands**: one row per hand, with the building name (gold), poker name (white), a blue chip with base chips, **×**, and a red chip with mult (the table in §4).
- A note: "Cards add chips too: 2-10 their number, J/Q/K 10, Ace 11. Kickers add nothing."
- Button **Back**.
- Later, it can also list the three blinds of this ante and the discard pile.

---

## 11. How the numbers move between sections

There are five scores. Points flow in one direction:

```
 Turn Score ──(Score)──▶ Pot ──(Bank)──▶ Safe (this blind)
 (left + plaque)        (right)          + Run Score (left)
                          │
                          ├─(break)──▶ −10% each
                          └─(collapse)─▶ 0
```

1. **Hand plaque → Turn Score** (Score phase)
   - The plaque resolves `(35 + 30) × 4`. The base chip punches, card chips count up card by card, the mult chip punches, then **= 260** slams in.
   - Turn Score shows **260** at the same time.
2. **Turn Score → Pot** (end of Score)
   - A green **+260** lifts off the Turn Score, a few gold chips fly across the table to the Pot sign, and the Pot counts up **52 → 312**.
   - The Pot tag on the tower updates to **◎ 312**.
   - **Turn Score does not go into Run Score directly.** It always goes into the Pot first.
3. **Shake → Pot** (end of Shake)
   - Each broken card appears in the Broken strip. When the shake settles, the Pot counts **down 312 → 280** with a red flash.
   - Collapse: the Pot sign shatters (the bulbs pop out), the number drops to **0**, and the ladder goes dark. Run Score and Safe are untouched.
4. **Pot → Bank quote** (Bank / Ride)
   - The Pot label changes to **Bank now** and the number rolls to the payout, **336**. The breakdown rows explain it, and the Safe bar shows the striped preview.
5. **Bank → Safe + Run Score**
   - The Bank key "dings", the Pot sign empties (336 → 0), and chips fly to the Safe bar (**0 → 336**, bar fills, turns gold once the target is met) and on to the Run Score (**0 → 336**).
   - The tower is swept away and the ladder goes dark.
6. **Leave Table → Safe + Run Score**
   - The bonus (+30) counts into both, with no height or reward multiplier.
7. **Blind won → reward**
   - The blind plaque's **Reward ×1.2** flies to the reward value in the Bank breakdown for the rest of the run.

**Counting speed:** a count-up never takes longer than about 0.8 s. It ticks 1 at a time for small changes and in bigger steps for big ones (step = change ÷ 40, rounded up), with a soft tick sound. The correct final number is always shown at the end, even if the animation is skipped.

---

## 12. Phase by phase

Brightness key: **On** = full, **Dim** = about 70% (panels) or 50–60% (plaque). Dimmed numbers must still be readable.

### Blind menu
- Blind menu panel open (§10.1). Everything behind it is dimmed by the backdrop.
- Left HUD dim, showing the new Ante / Blind values. Right HUD dim, Pot 0. Plaque hidden. Ladder dim.

### Draw
- Banner **Hand 2**. Instruction: "Dealing your hand..."
- Cards fly in one by one from the deck (B3) until there are 8.
- Hand area **On**. Left and right HUD dim. Plaque dim with "Your hand".
- Turn Score back to **–**. If the player rode, the Pot sign shows **Riding on the tower 52 / Rode from Hand 1** and the tower tag shows ◎ 52.

### Swap
- Instruction: "Swap: pick up to 3 cards, then click the deck."
- Picked cards lift with a blue ring. The deck glows blue once something is picked.
- Clicking the deck exchanges them. Swap pips go dark and the deck count drops.
- Hand area **On**. Others dim. **Done swapping** button shown.

### Commit
- Instruction: "Pick 1-5 cards for your poker hand" / "Play hand to commit 5 cards".
- Picked cards lift with a gold ring. The **hand plaque updates live** (name, building name, base + cards × mult).
- Hand area and left HUD **On**. Plaque **On**. **Play hand** shown (greyed with 0 cards).
- If the commit is refused, the reason appears in the instruction line and no hand is spent.
- On accept, one Hands pip goes dark.

### Build
- Committed cards move to the tray. The rest of the hand dims.
- Instruction: "Place your cards: 3 left". Snap markers on the tower (green = where it will land).
- Plaque shows the locked hand (85%). **Undo** button shown.
- Hand area **On**, the others dim.

### Score
- Plaque **On**: base chip → card chips → mult → **= 260**.
- Turn Score shows **260**, and **+260** floats away to the Pot.
- Pot: **Pot 312 / 52 + 260 from this hand**.
- Left and right HUD **On**. Hand area dim.

### Shake
- Banner **Hand 2 complete**. Instruction: "The tower shakes!"
- The tower wobbles. Pot: **Pot (unsettled) 312 / The tower is settling...**
- Breakdown: **Breaks −10% each**, **Collapse lose all**.
- Broken strip: icons pop in and flicker as cards break, and a broken card lies on the table.
- Ladder: the next level flickers, "settling...".
- Right HUD **On**, the others dim.

### Bank / Ride
- Right HUD **On** with a gold glow. Left HUD **On** (so Safe and Run Score sit side by side with the decision).
- Pot sign: **Bank now 336 / 312 − 1 broken card = 280**.
- Broken strip: 1 cracked card, **−10%**.
- Breakdown: **Raw pot 280**, **Height (2 levels) +20%**, **Reward ×1.0**.
- Safe bar shows the striped preview to 336.
- Ladder: **Level 2 / +20% height**.
- **Cashier drawer slides out**: receipt `SAFE 0 → 336 ✓ TARGET`, keys **BANK 336** and **RIDE**.
- Instruction (gold): "Bank the pot to make it safe, or Ride and build higher".
- Not shown after the last hand: that hand Banks automatically.

### Continue / Leave
- Only after a Bank that meets the target with hands left. Panel §10.2.
- Safe **336 / 150**, bar gold. Pot 0, ladder dark.

### Blind result
- Panel §10.3. On a fail, the glass crack plays in the left HUD.

### Run end
- Panel §10.4.

---

## 13. What changes in the code

Moving and reskinning needs no code (every text is its own object and every wording is a format string). The items below do need small view changes. The core rules don't change.

### 13.1 Existing elements and where they go

| Today | New place |
|---|---|
| `RunText` (Ante / blind / run / reward) | Split: Run Score (§3.1), Ante and Blind wells (§3.3). Blind name and reward move to the blind plaque (§5.1). Reward also in the Bank breakdown |
| `GlassText` (`GlassView`) | Three star images (§3.6) |
| `GlassNoteText` | Under the stars |
| `TurnText` (hand / target) | Removed: hands → Hands pips (§3.4), target → blind plaque |
| `StateText` | Instruction line (§7.1) |
| `HintText` | **Removed.** Card details only in `CardTooltip`. Placing hint goes in the instruction line |
| `CardsText` | Removed: the instruction line says "N left" and the tray shows it |
| `DeckText` | Deck count badge (§8) |
| `DiscardText` | Deck hover tooltip (§8) |
| `PotText` | Split into Pot sign label / number / line (§5.3), Broken strip (§5.4), breakdown rows (§5.5) |
| `BannerText` | Same, moved down (§7.2) |
| `HandNameText` | Plaque: building name + poker name (§4) |
| `ChipsText`, `MultText` | Plaque chips: base, card chips, mult (§4) |
| `TurnScoreText` | Turn Score well (§3.2) |
| `TotalScoreText` | Safe bar (§5.2) |
| `swapsLeftText` (not in scene yet) | Swap pips under the deck (§8) |
| `undoButton` (not in scene yet) | Undo button (§9.5) |
| `EndTurnButton` | Done swapping / Play hand (§9.5) |
| `BankButton` / `RideButton` (`BankRideView`) | Keys in the cashier drawer (§5.6) |
| `ContinueButton` / `LeaveButton` | Continue / Leave panel (§10.2) |
| `BlindMenuPanel` | Same content, new look (§10.1) |
| `ENDpanel` (`RunEndView`) | §10.4 |
| `CardTooltip` | Same (§9.3) |
| Snap markers | Same, green dot for the target slot (§9.4) |
| Stray `SnapMarker` copies under the Canvas | Delete |

### 13.2 New displays (need a small view script or fields)

| New element | Reads | Notes |
|---|---|---|
| Hand plaque building name | `shapeName` from `PokerHandEvaluator` | **Done.** The names in §4 are in the code |
| Base chips vs card chips | `ScoreManager.BaseChips`, `CurrentCardChips`, `CurrentMult` | **Done.** `CurrentCardChips` counts up during the reveal. `HudView` has optional `baseChipsText` / `cardChipsText` fields |
| Live preview in Commit | `Commit.PreviewScore` (`baseChips`, `cardChips`, `chips`, `mult`, `finalScore`) | **Done.** The scorer works it out on every pick, using the same code as the real score. It stays up through Build as the locked hand. Bonus effects are not in the preview (none exist yet) |
| Pot explanation lines ("52 + 260", "312 − 1 broken = 280") | Old and new Pot from the `PotChanged` event | The HUD shows the two numbers the event gives; it never subtracts |
| "Rode from Hand 1" | **New:** the hand number of the last Ride | Not tracked today. Could simply read "Rode from the last hand" instead |
| Blind result "Reward ×1.0 → ×1.2" | **New:** the reward multiplier before the win | Not kept today |
| Hands pips | `HandsLeft`, `HandsPerBlind` (already exist) | Read `HandsLeft`, so the pip goes dark the moment Play Hand is accepted |
| Swap pips | `SwapManager.SwapsLeft` | |
| Ladder (§6) | `StandingLevels` (already exists) | Max levels = 4 (see §14) |
| Broken strip (§5.4) | `ShakeResult.BrokenCount`, `breakPercent` (already exist) | Combined penalty for N breaks must come from the core, not be computed in the HUD |
| Safe-after-Bank preview and receipt | **New:** `GameFlowController` should expose `SafeAfterBank` | The HUD must not add Safe + BankQuote itself |
| Bank breakdown "Raw pot" | `PotChips` (already exists) | |
| Blind plaque reward | The blind's reward factor from config | |
| Blind number 1/3 | `CurrentBlind` | |
| Run Info panel | The scoring table | New panel |
| Options panel | — | Not designed yet |
| Cashier drawer slide | Phase = Bank / Ride | Presentation only. The keys still call the same Bank / Ride commands |

### 13.3 Events the animations need
Already in `GameEvents`: `CardsDrawn`, `SwapRejected`, `ShakeResolved`, `PotChanged`, `PotBanked`, `GlassChanged`, `BlindWon`, `BlindFailed`, `TableLeft`.
Still to add (listed in the juice plan): `HandCommitted`, `HandScored`, `TowerCollapsed`.
This design adds no other events: the Pot count-down after breaks uses `ShakeResolved` / `PotChanged`, and the Bank flight uses `PotBanked`.

### 13.4 Work outside the HUD views
- **Committed tray in Build (§9.4):** a hand layout change in `HandManager`, not only a view.
- **Pause while Run Info / Options is open (§3.5):** needs a pause in the game flow.
- **Pot tag on the tower (§7.3):** a new script that follows the top of the tower.
- **Fonts:** Cinzel, Cinzel Decorative and Pixelify Sans need TextMeshPro font assets.

---

## 14. Decisions (were open questions)

1. **Ladder dots: 4.** The tower has at most 4 base tents (`maxBaseTents = 4`), so it can stand at most 4 levels.
2. **Options menu:** sound volume (music, effects), restart run, quit to main menu. More later if needed.
3. **Blind well:** "Blind 1/3" confirmed.
4. **Over-target tag:** yes, show "+N over" next to Safe once it passes the target.
5. **Broken total:** only the last shake. A running total for the blind adds noise.
6. **Tower full:** the Ride key reads "Tower full". It matches `CanRide`, which is false when no card can be placed.
