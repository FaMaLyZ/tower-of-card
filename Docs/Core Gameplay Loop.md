# Neo Tower of Cards — Core Gameplay Loop

> **Status:** Current design. Replaces the old "House of Card: Core Gameplay" doc. The new loop is planned, not yet implemented.
> **Date:** 2026-11-11 · **Revision:** 2 — aligned with the revised [Master Plan](Master%20Plan%20Real.md).
> **⚙ = a starting number for playtesting. It can change.**
> **Prototype assumption = a new clarification to test, not a final design decision.** This document owns player rules; the Master Plan owns implementation/order; the [juice plan](juice-plan.md) owns presentation.

---

## The Game in One Sentence

**Play poker hands, build them into a house of cards, and decide when to Bank your points before the tower falls.**

---

## 1. The Big Idea

- Your points **sit on top of the tower** until you Bank them.
- **Bank** = your points are safe. Small, but certain.
- **Ride** = keep building for more points. If the tower falls, you **lose them**.
- The shakier your tower gets, the harder the choice. That's the game.

It works like Jenga: every turn, you decide whether to push your luck.

### Design rules
1. **Feel first.** Cards should feel real and respond to everything you do.
2. **Small punishment.** A falling tower hurts, but it never ends the game by itself.
3. **Fragile is the theme.** The tower, your points, and your lives can all break.

---

## 2. Words You Need to Know

| Word | Meaning |
|---|---|
| **Hand** | One turn: pick cards, play a poker hand, build them into the tower. |
| **Pot** | Points sitting on the tower. **Not safe yet.** |
| **Bank** | Move the Pot to Safe Points. The tower is cleared. |
| **Ride** | Keep the Pot on the tower and play another hand. |
| **Safe Points** | Banked points. They can never be lost. Shown as current Blind Score and cumulative Run Total. |
| **Blind Score** | Safe Points earned in this blind. Resets to zero at the next blind. |
| **Run Total** | All Safe Points earned throughout the run, including failed blinds. Never resets between blinds. |
| **Blind** | One level. Reach the target score in Safe Points. |
| **Glass** | Your lives. It has 3. Failing a blind cracks it. |

---

## 3. How the Game Is Built

```
RUN
 └─ 3 Blinds in a row:  Small Blind → Big Blind → Boss Blind   (= 1 Ante)
     └─ Each Blind = 4 Hands ⚙
         └─ Each Hand = Draw → Swap → Commit → Build → Score → Shake → Resolve
```

After a non-final Boss Blind, the next Ante starts with bigger targets if Glass survives. A surviving final hand automatically Banks after its shake; other surviving hands offer Bank/Ride.

---

## 4. Menus

### 4.1 Main Menu
| Button | Does |
|---|---|
| Play | Start a new run |
| Settings | Sound, controls, screen |
| Exit Game | Quit |

### 4.2 Starting a Run
Cards fly in one by one and stack into a deck on the table.

### 4.3 Blind Menu
It slides up from the bottom **before every blind**. Only the current blind can be selected or skipped; later blinds are previews. The reward factors shown below are starting examples ⚙.

```
|------------------|------------------|------------------|
|     [Select]     |     [Select]     |     [Select]     |  ← play this blind
|   Small Blind    |    Big Blind     |    Boss Blind    |  ← name
|  Score at least  |  Score at least  |  Score at least  |
|      ◎ 300       |      ◎ 450       |      ◎ 600       |  ← target (Safe Points)
|  Reward  ×1.2    |  Reward  ×1.4    |  Reward  ×1.6    |  ← bonus if you win
|   [Skip Blind]   |   [Skip Blind]   |   (no skip)      |  ← skip
|------------------|------------------|------------------|
```

---

## 5. One Hand, Step by Step

| # | Step | What you do |
|---|---|---|
| 1 | **Draw** | Cards fly in until you hold 8. Cards you didn't play stay in your hand. |
| 2 | **Swap** | Select up to 3 cards, then click the deck to swap them all at once. **3 swapped cards per hand ⚙** (each card costs one swap). Press **Done Swapping** to continue; swapping is optional. |
| 3 | **Commit** | Pick 1–5 cards. The poker hand name shows at the top. Press **Play Hand**. An accepted commitment spends one hand and locks those cards. |
| 4 | **Build** | Place every committed card on the tower. Cards stay frozen. You can undo this hand's latest placement, but cannot change the committed poker hand. |
| 5 | **Score** | Points count up and go into the **Pot** (on the tower). |
| 6 | **Shake** | The tower is let go. It wobbles, settles, and cards may break. |
| 7 | **Resolve** | Collapse loses the Pot. A surviving final hand automatically Banks; otherwise choose Bank or Ride. |

**Poker score** = (base chips + scoring-card chips) × multiplier. Keep the existing evaluator and scoring table. Score the committed set once; kickers that the evaluator excludes do not add chips. Two Pair needs at least 4 cards; Straight, Flush, Full House, and Straight Flush need 5.

**Commit safety:** the game must confirm that all chosen cards can fit through a legal placement sequence, including slots opened by earlier placements. An impossible commitment spends nothing and lets you reselect. This is not a limit based only on currently visible slots.

**Card lifecycle:** unused cards stay in hand between hands. Played cards remain spent until the next blind, even after Bank or collapse. Bank does not refresh the deck, swaps, or remaining hands.

---

## 6. Bank or Ride

### 6.1 The choice
| | **Bank** 🏦 | **Ride** 🎢 |
|---|---|---|
| Pot | Becomes Safe Points | Stays on the tower |
| Tower | Cleared; next hand starts fresh | Stays; next hand builds higher |
| Good | Can't lose it anymore | Taller tower = bigger points |
| Bad | Lose your tall tower and its bonus | Tower falls = lose the Pot |

### 6.2 Tall tower bonus
- Every standing level gives the Pot **+10% ⚙**, recalculated after each shake.
- Example: raw Pot 200 with 3 standing levels → height value **260** before any run reward multiplier.
- **It never compounds into stored Pot chips.** Ride carries the raw Pot after break penalties; the next shake recalculates the height bonus.
- **Prototype assumption:** count contiguous tiers from the table, each containing at least one complete supported left/right tent. Connecting floors enable upper tiers but add no level. A lone standing base card can prevent collapse while giving zero height bonus.

### 6.3 When things break
| What happens | You lose |
|---|---|
| A card breaks | **10% ⚙ of the remaining raw Pot** per unique broken card; two breaks multiply it by 0.9 × 0.9 |
| **Tower falls** (no standing base cards left) | **The whole Pot** 💥 |

Safe Points are never touched. A falling tower **does not** end the game or crack the Glass.

**Prototype assumption for fallen cards:** at shake end, cards outside the standing/support rules are removed as breaks, including cards that fell without breaking on impact. Each card counts once. This keeps the current prototype's forced-break behavior but resolves it before Bank/Ride. Their old slots reopen only where surviving support permits a new placement.

The shake has a minimum observation time, a quiet period, and a maximum duration. After it resolves, survivors are frozen and its payout is fixed. See the Master Plan for thresholds; the game cannot wait indefinitely for physics.

### 6.4 Exact payout

1. Add the committed poker score to raw Pot.
2. Resolve the shake. Collapse makes Pot zero; otherwise apply all unique break penalties and round down once: `raw Pot = floor(raw Pot × (1 − break rate)^broken cards)`.
3. Quote `Bank payout = floor(raw Pot × (1 + height rate × standing levels) × current run reward multiplier)`. Round once at the end of this product.
4. Bank adds the same payout to both Blind Score and Run Total, zeros Pot, and clears the tower. Ride stores raw Pot only.

The post-shake Pot display says **Bank now** and shows the actual payout; a breakdown explains raw chips, height, and reward. During the shake the value is unsettled.

**Rounding example:** Pot 200 + hand 100, then two 10% breaks → raw Pot 243. Two levels and reward ×1.2 → `floor(243 × 1.2 × 1.2) = 349` banked. Do not round the intermediate height value before applying the reward.

### 6.5 Four-hand example

Illustration with reward ×1, no breaks before the final collapse, and the stated standing levels:

| Hand | What happens | Raw Pot after choice | Blind Score |
|---|---|---|---|
| 1 | Pair scores 52; one level survives; **Ride** | 52 | 0 |
| 2 | Flush adds 260; two levels survive; **Bank** for `floor(312 × 1.2)` | 0 | **374** ✅ |
| 3 | New tower; Pair scores 52; **Ride** | 52 | 374 |
| 4 | Flush adds 260, then **tower collapses** | 0 | **374** |

The fourth hand ends the blind against 374 safe points. No extra life is lost for the collapse itself.

### 6.6 When the tower is full

**Prototype assumption:** disable Ride when no legal next placement exists and show **“Tower full — Bank to continue.”** Bank works normally. A full tower never ends the run.

---

## 7. Winning and Losing a Blind

### 7.1 Playing the blind
- You get **4 hands ⚙**. The deck is fully reshuffled and the tower starts empty.
- Clear the previous hand and restore all 52 cards at blind start. Pot and **Blind Score** reset to zero; **Run Total**, rewards, and Glass carry over.
- Only this blind's **Blind Score** counts toward its target.

### 7.2 The last hand
- Accepting the last **Play Hand** spends the last remaining hand; placement and the shake still have to finish.
- If the tower survives, apply break penalties and automatically Bank the remaining Pot, then finish the blind. There is no Bank/Ride prompt or unused-hand bonus.
- If it collapses, lose the Pot and finish against the already safe Blind Score.

### 7.3 Reaching the target
- The blind **does not end** as soon as Blind Score reaches the target.
- After a Bank, if the target is met and hands remain, offer **Continue / Leave Table** before drawing again.
- **Continue** plays the next hand. **Leave Table** ends the blind and pays `floor(target × 0.10 × unused hands)` ⚙ once.
- **Prototype assumption:** this bonus goes directly to both safe totals without height/reward multiplication. Example: target 300 and two unused hands gives 60 bonus points.

### 7.4 Win ✅
At the end, if **Blind Score ≥ target**:
- You get the blind's **Reward** once. Future Banks use the increased multiplier (e.g. current multiplier ×1.2). It stacks for the whole run; it does not multiply banks or leave bonuses already paid.
- Return to the next Blind Menu, or clear the run on a final Boss win. A Boss win also repairs one Glass crack, up to maximum lives.

### 7.5 Fail ❌
At the end, if **Blind Score < target**:
- No reward.
- **The Glass cracks** (−1 life).
- Banked points remain in Run Total. If Glass survives, advance to the next blind; the final Boss uses the retry rule below. If Glass shatters, end the run.

### 7.6 Skip
- **Small** and **Big** blinds can be skipped. Skipping gives no score and no reward.
- Instead, it **repairs 1 Glass crack ⚙**.
- Repair is capped at maximum lives. Skip advances exactly one blind; you cannot repeatedly skip the same blind to repair.
- The **Boss** blind can't be skipped.

### 7.7 Boss Blind
It is a plain blind with the highest target and the best reward. It has **no twist**: the rules are the same as any other blind. It can't be skipped, and a win repairs one Glass crack.

Boss twists (Tremor, Heavy Card, Glass Pot, Short Fuse) are cut from the prototype.

---

## 8. Winning and Losing the Run

### 8.1 The Glass (your lives)
- It has **3 lives ⚙**. Each failed blind cracks it once.
- **3rd crack → the Glass shatters → run over.**
- Beating a Boss blind **repairs 1 crack ⚙**.

### 8.2 Run end
| Result | How |
|---|---|
| **Run Cleared** 🏆 | Beat the Boss of the final Ante (4 Antes ⚙) |
| **Shattered** 💔 | The Glass breaks |

**Prototype assumption:** if the final Boss fails while lives remain, retry that same Boss with the same target and fewer lives. Reset its hand budget, deck, tower, Pot, and Blind Score; preserve Run Total and existing rewards. Winning clears the run; a failure that reaches zero lives shatters Glass. There is no free repair on retry.

**Final Score = all your Safe Points from the whole run.** Try to beat your best.

### 8.3 Targets ⚙
- Ante 1: **300 / 450 / 600**
- Each next Ante multiplies targets by about **×1.5**. Your Rewards help you keep up.
- Calculate `floor(base target × growth^(Ante − 1))` from the Ante 1 target; round down once. A final-Boss retry uses the same target.

---

## 9. Full Flow

```
Main Menu → Play → Intro → Blind Menu
  Skip eligible blind → capped repair → next blind
  Select → reset blind → Draw → Swap → Commit → Build → Score → Shake

Resolve shake:
  Collapse → lose Pot → hands remain? Draw : Finish Blind
  Survive, no hands left → penalties → Auto Bank → Finish Blind
  Survive, hands remain → penalties → Bank / Ride
    Ride (if buildable) → Draw
    Bank → secure points + clear tower
      Target not met → Draw
      Target met → Continue (Draw) / Leave Table (bonus, Finish Blind)

Finish Blind:
  Win → reward; Boss also repairs → next blind / final Boss clears run
  Fail → one crack
    Glass shattered → Run Over
    Alive → next blind / retry final Boss
```

---

## 10. Still to Decide
1. Every ⚙ number needs playtesting.
2. Use 4 Antes for the prototype; decide whether endless should be a separate mode.
3. Test the new assumptions: completed-tent height, fallen-card break penalties, forced Bank on full tower, leave-bonus accounting, and final-Boss retry.
4. Should the Bank / Ride buttons show the risk, e.g. a "wobble meter"?
5. Test reward growth against target growth. Keep multiplicative rewards for the baseline; example factors are not proven balanced. Leave Table trades further scoring opportunities for a guaranteed bonus and saved time. Continuing cannot undo a secured blind win; only additional unbanked points are at risk.

## 11. What Changed From the Old Design
| Old | New |
|---|---|
| 10 turns, then the game ends | Blinds, Antes, and 3 lives (Glass) |
| Full tower or empty hand = game over | Removed. Shattered Glass causes a loss; beating the final Boss clears the run. |
| Points go straight to your total | Points sit on the tower until you Bank |
| Tower falling costs nothing | Tower falling loses your Pot |
| Reaching the target ends the level | Keep playing, or Leave Table for a bonus |
