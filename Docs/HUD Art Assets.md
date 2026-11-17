# Neo Tower of Cards — HUD Art Asset List

> **For:** the HUD artist.
> **Date:** 2026-11-17
> **Look and colours:** [HUD Design.md](HUD%20Design.md) §2 (style guide) and the section named in each table. Match the existing menu art in `Assets/Images` (`main.png`, `Score Panel.png`, `ScoreSum.png`).

While you draw, the game is being built with plain placeholder shapes. **The placeholders are already in `Assets/Images/HUD/`**, one PNG per file in this list, with the final name and size. To deliver a piece, overwrite the PNG with the same name and keep its `.meta` file: that file holds the stretch borders and import settings, so nothing else has to change. A file with states is named `name_state.png` (for example `button_gold_pressed.png`, `hand_pip_lit.png`); variants are numbered (`glass_shard_1.png`).

---

## 1. Delivery rules

1. **Format:** PNG with a transparent background.
2. **Resolution:** the screen is **1920 × 1080**. Sizes below are for that screen. **Please deliver at 2× the listed size** (a 72 × 72 chip is delivered as 144 × 144) so it stays sharp on bigger screens.
3. **No text or numbers in the art.** Every label and number ("POT", "336", "Small Blind", "BANK") is live text drawn by the game on top of your art. Leave the space empty. The only exception is the small decorative **CASHIER** plate (§7), which never changes.
4. **One file per state.** If an item has states (for example normal / pressed / disabled), deliver one file for each, all the same size and lined up exactly, so swapping between them does not make the item jump.
5. **Stretchable items** (marked **Stretch** below) are cut into 9 parts by the game: the 4 corners stay fixed and the edges and centre stretch. Keep all the detail (curls, arches, rivets) inside the **border** size given, and keep the middle plain enough to stretch. Tell us the final border in pixels if you change it.
6. **Tintable items** (marked **Tint**) can be delivered as one white / light grey version; the game colours it. If you prefer, deliver the coloured versions listed instead.
7. **File names:** use the name in the first column, lower case, plus the state: `button_gold_pressed.png`. Put them in `Assets/Images/HUD/`.
8. **Fonts are handled by us:** Cinzel Bold, Cinzel Decorative Black and Pixelify Sans (all free Google Fonts). Use them in your mock-ups so sizes match. They have no →, ✓ or ◎ characters, so don't design around those; the game uses » and the `chip_icon` sprite instead.

---

## 2. Order of delivery

We build the HUD in this order, so art that arrives in this order can go in straight away:

| Batch | Contents | Sections |
|---|---|---|
| 1 | Shared pieces: panel frame, wells, buttons, pill, marquee | §3 |
| 2 | Left HUD and the hand plaque | §4, §5 |
| 3 | Right HUD and the ladder | §6 |
| 4 | Cashier drawer and deck | §7, §8 |
| 5 | Cards, full-screen panels, glows and particles | §9, §10, §11 |

---

## 3. Shared pieces (Batch 1)

Used in many places. Design §2.3, §2.4.

| Name | Size (1×) | Stretch / Tint | States | Notes |
|---|---|---|---|---|
| `panel_frame` | 340 × 760 | **Stretch**, border: top 96, sides and bottom 24 | — | Arched top (wide oval top corners, small rounded bottom corners), 2 px gold outer line, navy gap, faint gold inner line, navy gradient fill `#22203F` → `#191730`. Used for the left HUD, right HUD and every pop-up panel. Drop shadow is done by the game. |
| `corner_curl` | 48 × 48 | — | — | Gold whiplash curl with a small dot, for the **top-left** corner. The game mirrors it for the other side. |
| `well_gold` | 300 × 64 | **Stretch**, border 16 | — | Display well: very dark fill `#100E22`, inner shadow, thin gold rim. Must look sunk in, never like a button. |
| `well_silver` | 140 × 64 | **Stretch**, border 16 | — | Same with a silver rim (Ante and Blind wells). |
| `well_salmon` | 300 × 48 | **Stretch**, border 14 | — | Same with a salmon rim `#FF9C8F` (Broken strip). |
| `button_red` | 200 × 56 | **Stretch**, border 20 | normal, pressed, disabled | Raised: bright fill, light top edge, thick dark 3D edge underneath. Pressed = pushed down 3–5 px with the bottom edge mostly gone. Disabled = grey and flat. |
| `button_gold` | 200 × 56 | **Stretch**, border 20 | normal, pressed, disabled | Same shape, gold. |
| `button_navy` | 200 × 56 | **Stretch**, border 20 | normal, pressed, disabled | Same shape, navy. |
| `button_blue` | 200 × 56 | **Stretch**, border 20 | normal, pressed, disabled | Same shape, royal blue `#2F6FD6`. |
| `pill` | 64 × 40 | **Stretch**, border 18 | — | Dark fill, gold rim, fully rounded ends. Used for the instruction line, banners, the Pot tag, the deck count and tooltips. |
| `marquee_frame` | 300 × 150 | **Stretch**, border 28 | — | Gold sign frame, dark navy inside, **without bulbs**. Used for Run Score, the Pot sign and the run-end score. |
| `marquee_bulb` | 12 × 12 | — | on, off | One light bulb. The game places a ring of them on the frame and makes them chase and flash. |

---

## 4. Left HUD — run status (Batch 2)

Design §3. Sits on `panel_frame`.

| Name | Size (1×) | Stretch / Tint | States | Notes |
|---|---|---|---|---|
| `hand_pip` | 28 × 28 | — | lit, spent | Lit = glowing gold ball. Spent = dark with a gold rim. |
| `glass_star` | 72 × 72 | — | whole, cracked | Red stained-glass star with gold "lead" lines inside and a white shine. Whole = bright red `#D8322A` with a soft glow. Cracked = dark wine red `#4A1512`, no glow, faint shine. |
| `glass_crack_line` | 72 × 72 | — | — | Only the white zig-zag crack, on transparent, lined up with `glass_star`. Separate so the game can draw it across the star. |
| `glass_shard` | 16 × 16 | — | 3 variants | Small red glass pieces that fly off when a star cracks. |

The Run Score uses `marquee_frame`; Turn Score, Hands and Ante / Blind use the wells; Run Info and Options use `button_red` and `button_gold`.

---

## 5. Top HUD — hand plaque (Batch 2)

Design §4.

| Name | Size (1×) | Stretch / Tint | States | Notes |
|---|---|---|---|---|
| `plaque_hand` | 760 × 120 | **Stretch**, border: left 80, right 40, top and bottom 24 | — | Gold outer frame, dotted cream line, royal-blue fill `#3148BD` → `#1D2A78`, small gold curls in the top-left and bottom-left corners. |
| `chip_dark` | 72 × 72 | — | — | Casino chip, dark `#1C1A33`, white dashed edge. Empty middle (the number is live text). Base chips. |
| `chip_blue` | 72 × 72 | — | — | Same, blue `#2F6FD6`. Card chips. |
| `chip_red` | 72 × 72 | — | — | Same, red `#D8452F`. Mult. |

The **+**, **×** and **=** signs are text.

---

## 6. Right HUD — Pot and decision, and the ladder (Batch 3)

Design §5 and §6. Sits on `panel_frame`.

| Name | Size (1×) | Stretch / Tint | States | Notes |
|---|---|---|---|---|
| `blind_plaque` | 300 × 150 | **Stretch**, border 28; **Tint** | small, big, boss | Rounded plaque, gold rim, dotted cream inner line. Small = blue `#2F6FD6`, Big = gold `#C8902F`, Boss = red `#C8322B`. The glow is done by the game. |
| `chip_icon` | 28 × 28 | — | — | Tiny gold chip shown before the target number (and on the Pot tag). |
| `bar_frame` | 280 × 20 | **Stretch**, border 8 | — | Thin bar with a gold rim (the Safe bar). |
| `bar_fill_green` | 32 × 14 | **Stretch** | — | Gradient `#2FA66A` → `#7DFFB6`. |
| `bar_fill_gold` | 32 × 14 | **Stretch** | — | Gold, for when the target is met. |
| `bar_fill_striped` | 32 × 14 | repeats sideways | — | Pale green diagonal stripes, the "after Bank" preview. Must tile seamlessly left to right. |
| `broken_card_icon` | 28 × 36 | — | — | Small red card back split by a white diagonal crack, tilted slightly. |
| `broken_fragment` | 12 × 12 | — | 3 variants | Card fragments that burst when an icon pops in. |
| `ladder_line` | 4 × 64 | repeats up and down | — | Thin vertical gold line. Must tile seamlessly. |
| `ladder_dot` | 22 × 22 | — | lit, unlit | Lit = glowing gold ball. Unlit = navy with a gold ring. |

The Pot sign uses `marquee_frame`; the Broken strip uses `well_salmon`.

---

## 7. Cashier drawer (Batch 4)

Design §5.6. Hangs under the right HUD and slides down in the Bank / Ride choice.

| Name | Size (1×) | Stretch / Tint | States | Notes |
|---|---|---|---|---|
| `drawer_body` | 340 × 230 | — | — | Red lacquer `#B5352B` → `#7A1F19`, gold trim, deep rounded bottom, two brass rivets. Inside is a cream tray with an inner shadow. |
| `drawer_plate` | 120 × 28 | — | — | Small plate across the top with the word **CASHIER** (the one item with baked-in text). |
| `receipt_strip` | 300 × 40 | **Stretch**, border 12 | — | Paper strip between two dashed lines. The receipt text is live. |
| `key_bank` | 140 × 70 | — | normal, pressed | Chunky gold register key. Pressed = pushed down 5 px. |
| `key_ride` | 140 × 70 | — | normal, pressed, disabled | Chunky red register key. Disabled = greyed (when the tower is full). |

---

## 8. Deck (Batch 4)

Design §8. The deck itself is a 3D card stack. Its HUD pieces:

| Name | Size (1×) | Stretch / Tint | States | Notes |
|---|---|---|---|---|
| `swap_pip` | 20 × 20 | — | lit, spent | Blue `#5B9CFF` pip under the deck, one per swap left. |

The count badge and the hover tooltip use `pill`. The blue "click me" glow uses `glow_soft` (§11).

---

## 9. Cards (Batch 5)

Design §9. Cards are 3D objects, so these are textures, not HUD sprites. **Check the current card size with the developer before starting.**

| Name | Size | Stretch / Tint | States | Notes |
|---|---|---|---|---|
| `card_back` | match current cards | — | — | Red diagonal stripes, gold edge. Used on the deck and on any card face-down. |
| `card_face` | match current cards | — | — | Cream with a gold rim. Rank and suit are placed by the game; deliver suit symbols separately if you redraw them. |
| `card_ring` | card size + 12 px each side | **Stretch**; **Tint** | — | Outline drawn around a picked card. Blue `#5B9CFF` in Swap, gold `#FFCF4D` in Commit. |

---

## 10. Full-screen panels (Batch 5)

Design §10. Panels reuse `panel_frame`, the buttons and the wells. Extra pieces:

| Name | Size (1×) | Stretch / Tint | States | Notes |
|---|---|---|---|---|
| `blind_chip_big` | 140 × 140 | **Tint** | small, big, boss | Big casino chip in the blind's colour for the blind menu. Empty middle (the target number is live text). |
| `sparkle` | 32 × 32 | — | — | Small star sparkle for the "Won!" and run-end screens. `Assets/Images/Spark.png` can be reused if you're happy with it. |

---

## 11. Glows and particles (Batch 5)

| Name | Size (1×) | Stretch / Tint | States | Notes |
|---|---|---|---|---|
| `glow_soft` | 128 × 128 | **Tint** | — | White soft round glow, fully transparent at the edges. Used behind lit pips, stars, the deck, the Bank / Ride panel and banners. |
| `chip_coin` | 24 × 24 | — | — | Small gold chip that flies from the Pot to Safe and the Run Score when the player Banks. |

---

## 12. Questions for the artist

1. Are the sizes comfortable, or would you like a reference mock-up at 1920 × 1080 to draw over? (We can export one from the placeholder build.)
2. Do you prefer to deliver tintable items as white (we colour them) or fully coloured?
3. If you change a stretch border, please write the new number next to the file name.
