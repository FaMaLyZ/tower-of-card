# Neo Tower of Cards

Game Design Document  
Version 0.1 | 28 October 2026

Neo Tower of Cards is a single-player card-building score game. Players choose cards for their poker combinations and place those same cards into a physical house of cards. At the end of each turn, the hand scores and the structure is released into physics. The next turn begins from whatever remains.

This document defines the current playable concept, its rules, presentation, and production scope. Sections 1 through 7 describe the existing design and scene configuration. Section 8 identifies implementation conflicts and recommended decisions before the rules are finalized.

## Contents

1. Overview
2. Short Description and Design Direction
3. About This Game
4. Contextual Gameplay and Systems
5. Assets and Presentation
6. Idea Sources
7. Technical Specifications and Scope
8. Design Decisions and Validation
9. Source Register

<!-- page -->

## 1 Overview

| Property | Design |
| --- | --- |
| Title | Neo Tower of Cards |
| Genre | Turn-based card strategy, physics construction, score attack |
| Mode | Single player |
| Engine | Unity 6, version 6000.5.8f1 |
| Presentation | A 3D tabletop and cards, with a 2D casino-themed interface |
| Input | Keyboard and mouse |
| Platform direction | Desktop PC; release storefront and supported operating systems remain undecided |
| Run structure | A configured maximum of 10 turns, with earlier ending checks described in Section 8 |
| Primary objective | Earn the highest cumulative poker score during a run |
| Structural objective | Keep useful supports and open placement positions available for later turns |

### 1.1 High Concept

Every card is both a scoring choice and a building piece. The player studies an eight-card hand, exchanges unwanted cards within a limited allowance, and places up to five cards into a growing tower. Those placements form the turn's poker hand, regardless of where they sit in the structure. When the turn ends, the score counts up while the tower is allowed to shake, fall, and break.

The player then has to work with the result. An intact tower offers existing supports, but also occupies a limited construction layout. Broken sections can reopen positions. If every tracked base card disappears, the remaining structure is cleared and the player can start again on the table.

### 1.2 Target Audience

The intended audience is players who enjoy recognizing card combinations, improving a score across repeated attempts, and watching physical systems react to their decisions. Familiarity with poker helps, but the game should explain combinations and scoring without requiring gambling knowledge. There is no implemented betting, money economy, or opponent.

### 1.3 Types of Engagement

**Challenge** comes from selecting a strong scoring hand while maintaining opportunities to build. **Expression** comes from the order of placement and the choice to expand, bridge, or rebuild. **Sensation** comes from the card selection response, tower movement, break effects, and score reveal. Discovery is focused on learning card weights and construction behavior rather than exploring a world.

<!-- page -->

## 2 Short Description and Design Direction

### 2.1 Short Description

Build a house of cards with the same cards you use to make poker hands. Choose a combination, decide where each card belongs, and end the turn to watch the score rise and the tower face gravity. Keep building from the surviving structure and make the most of a limited run.

### 2.2 About the Experience

Neo Tower of Cards alternates between deliberate planning and a short physical reveal. During construction, placed cards are locked in place, so the player can read the available positions and make decisions without a placement timer. Ending the turn creates a moment of uncertainty: the structure moves, cards may break, and the hand's numerical value becomes visible.

The tabletop is the entire play space. The current game does not need a character, a story campaign, or a simulated casino economy to explain its activity. Casino imagery gives the presentation a sense of spectacle; the actual decisions are about cards, limited resources, and construction.

### 2.3 Design Pillars

**Give cards two meaningful properties.** Rank and suit determine poker combinations, while rank also determines mass. Card inspection should make both readable before placement. The current assets make aces the lightest cards and kings the heaviest.

**Make the structure legible.** Legal snap positions and a placement preview should explain how to expand. The player chooses a structural role and position; card orientation is supplied by the construction system.

**Give every turn a clear payoff.** The hand name, chips, multiplier, turn score, and total score should be understandable as a sequence. The physics reveal should leave time to see what survived before control returns.

### 2.4 The Main Design Tension

A high-value card may add more weight, but the relationship is not a simple rule that better scoring always means greater structural risk. An ace contributes 11 chips and weighs 0.06, while a king contributes 10 chips and weighs 0.18. Suits do not change mass in the current set.

The current scorer does not award tower height, reward surviving cards, or subtract points for collapse. Construction affects available positions and future play rather than the value of the hand already played. Whether survival should influence scoring is a design decision, not an existing mechanic.

<!-- page -->

## 3 About This Game

### 3.1 Game View

The camera looks toward the tabletop from an elevated orbit around the tower. Holding A or D rotates the view horizontally. The camera follows the center of the tracked structure as it changes and returns smoothly toward the starting view after a complete tower reset.

The orbit lets the player inspect depth and separate snap positions that overlap from one angle. Camera pitch and distance are configured values; the current input script does not provide player-controlled zoom or pitch adjustment.

### 3.2 Core Player Activity

The player inspects rank, suit, and mass, chooses a card, and commits it to a legal position. A turn may use cards in different parts of the tower: physical adjacency is not required for the poker hand. The important scoring group is the set placed during the current turn.

| Input | Action | Availability |
| --- | --- | --- |
| Left-click a hand card | Select that card | Active construction turn |
| Left-click the table | Start a new tower with the selected card, or the first hand card if none is selected | Before a tower exists |
| Keys 1 to 8 | Select a corresponding hand card | After the first placement; the script supports up to 9 positions |
| Move pointer near a marker, then left-click | Place the selected card in that snap position | With a valid preview |
| Right-click | Cancel the placement preview | While previewing a card after tower creation |
| Left-click the discard zone | Exchange the selected card for a replacement | With an exchange remaining |
| Hold A or D | Orbit the camera | During the scene |
| End Turn button | Finish construction early and resolve the turn | Active construction turn |

### 3.3 Placement Feedback

The selected hand card rises and enlarges. Hovering a card or selecting it displays its details. After the tower exists, a preview follows the pointer and moves toward the nearest available slot. It is tinted green when snapped to a legal position and red when no position is close enough. The corresponding marker is highlighted.

The first placement follows a separate interaction: clicking the table sets the tower origin and immediately places its first left support card. The first card does not use the normal free-position preview.

<!-- page -->

### 3.4 Construction Rules

A **tent** consists of a left card and a right card leaning toward one another. A **floor** is one horizontal card bridging two complete neighboring tents. A floor unlocks a new tent above it. This produces a stepped house-of-cards structure rather than freeform stacking.

| Building action | Requirement | Result |
| --- | --- | --- |
| Start the tower | A card in hand and a click on the table layer | First left support at the chosen origin |
| Complete a tent | One support already occupies the tent | Opposite support becomes available |
| Extend the base | A neighboring base position and fewer than four occupied base tents | New left or right base support |
| Add a floor | Two adjacent tents are complete | A horizontal bridge between them |
| Build upward | A floor is present | Left and right supports for the next level |
| Reuse a broken card's position | The tracked card has broken and the slot prerequisites still hold | The position can become available again |

The scene allows four occupied base tents. An ideal complete pyramid with four base tents has 10 tents, requiring 20 leaning cards, plus 6 floors: **26 cards in total**. Filling every legal position can therefore end construction before a ten-turn run uses all 50 possible placements. Collapse, repairs, and early turn endings change that path.

### 3.5 Physics and Breaking

During construction, placed cards have their position and rotation frozen. At the end of the turn, those constraints are released and gravity and collisions act on the structure. Cards also receive one of three shake animation values. When the next active construction phase begins, surviving tracked cards freeze again where they are.

The active card prefab uses a break threshold of 1.5 meters per second. A sufficiently fast collision can break a card. The system also tracks peak movement speed and may break a card when it comes to rest after a fast fall. Breaking removes the card from tower tracking, plays the break sound, and spawns a short-lived break effect.

If no tracked base support remains, the system clears the remaining tower and restores the initial table-click interaction. It does not reset the cumulative score or replenish the deck. A fallen but unbroken card can remain tracked; its geometric slot is still treated as occupied even if its physical pose has changed.

<!-- page -->

### 3.6 Rules and Run Outcome

The gameplay scene configures a ten-turn score run. There is no score threshold that produces a victory, and no dedicated tower-height victory. The practical goal is to finish with a better cumulative score than in a previous attempt; persistent best-score storage is not implemented.

Each turn permits up to five placements. Playing the fifth card ends the turn automatically. The player may end earlier, and an empty hand can also end the turn. A shorter hand can still score a high card, pair, two pair, three of a kind, or four of a kind when applicable. Straights and flushes require exactly five cards. A turn with no placements awards no points.

The current game-over logic also requests a final result when there are no legal slots **or** the hand is empty after a tower has been started. This conflicts with ordinary between-turn empty hands; Section 8 records the required resolution. Total collapse is not itself an explicit loss condition.

### 3.7 Obstacles Along the Way

| Obstacle | Player response | Design consequence |
| --- | --- | --- |
| An awkward draw | Exchange a card, play a smaller combination, or retain useful cards | Hand management becomes part of the run |
| Unequal card weights | Inspect mass and consider placement order | Poker choice also changes the physical structure |
| An incomplete support layout | Finish tents and bridges before building higher | Growth follows readable dependencies |
| End-of-turn instability | Observe what remains and repair or rebuild | Planning alternates with physical uncertainty |
| Limited deck and occupied slots | Spend turns and exchanges deliberately | Opportunities become scarcer across the run |

There are no implemented enemies, wind events, scripted sabotage, bosses, or random environmental hazards. The challenge comes from the draw, the construction rules, and physics.

### 3.8 Core Gameplay Loop

**Draw toward eight cards → inspect and exchange → place up to five cards → end the turn → reveal the poker score and release physics → freeze survivors when building resumes → draw and repeat.**

At run end, show the final total and offer Play Again or Main Menu. Replay loads the gameplay scene again. Returning to the menu restores the menu presentation and its music.

<!-- page -->

## 4 Contextual Gameplay and Systems

### 4.1 Shell Menus

The main menu offers Play and Quit. Play loads MainGame. The final-score panel offers Play Again and Main Menu. Its score text reads the current total and fades in over one second. Opening the game-over panel stops the music and plays the end-game sound.

### 4.2 Turn and Hand Properties

| Property | Gameplay scene value | Meaning |
| --- | --- | --- |
| Maximum turns | 10 | Configured limit for a run |
| Placement allowance | 5 per turn | Automatically ends the turn when reached |
| Hand capacity | 8 | Maximum cards held at once |
| Draw request | 8 at each turn start | Draw stops at hand capacity or an empty deck |
| Retain unplayed cards | Yes | Remaining cards carry into the next turn |
| Exchanges | 3 per turn | One selected card replaced per exchange |
| Start phase | 2 seconds | Cards are dealt; construction is locked |
| End phase | 3 seconds | Scoring and physical resolution occur |
| Automatic next turn | Enabled | No Next Turn click is required |

The draw setting does not add eight cards on top of an existing hand. If three cards remain, only five can be drawn to refill the eight-card capacity. Keeping a card is therefore a way to plan a future combination, but it also reduces the number of fresh cards drawn.

### 4.3 Deck and Exchanges

The scene starts with one copy of each of the 52 standard cards: four suits and thirteen ranks, without jokers. The deck is shuffled at startup. Played cards enter the used-card discard pile while their physical instances remain in the tower. This bookkeeping does not remove the tower objects.

Automatic reshuffling of used cards is disabled in MainGame. Once drawn and played, those card identities do not return through normal dealing. A player exchange behaves differently: the selected card returns to a random position in the draw pile, and a replacement is drawn. It does not consume one of the five placements.

The replacement is drawn before the exchanged card returns, normally preventing an immediate redraw of the same card. When nothing can be drawn, the returned card may be drawn as a fallback. Exchanges refresh each turn; they do not create additional cards.

<!-- page -->

### 4.4 Poker Scoring

Only cards placed during the current turn are evaluated. Cards from earlier turns do not join a later hand, even if they are adjacent or in the same tent. Cards that break during resolution still count because their data was recorded when placed.

**Turn score = floor of [(base chips + scoring-card chip values) × base multiplier].** The turn score is then added to the run total. No active bonus system currently changes this formula.

| Hand | Required combination | Base chips | Multiplier | Cards adding chips |
| --- | --- | --- | --- | --- |
| High Card | No stronger combination | 5 | 1 | Highest-ranked card |
| Pair | Two of one rank | 10 | 2 | The pair |
| Two Pair | Two distinct pairs | 20 | 2 | Both pairs |
| Three of a Kind | Three of one rank | 30 | 3 | The three matching cards |
| Straight | Five consecutive ranks | 30 | 4 | All five |
| Flush | Five of the same suit | 35 | 4 | All five |
| Full House | A triple and a pair | 40 | 4 | All five |
| Four of a Kind | Four of one rank | 60 | 7 | The four matching cards |
| Straight Flush | Five consecutive ranks of one suit | 100 | 8 | All five |

Ranks 2 through 10 contribute their face values. Jacks, queens, and kings contribute 10 chips each. Aces contribute 11. Nonmatching kickers do not add chips to pairs, triples, or four of a kind. Aces count high when choosing the highest card; straights can use A–2–3–4–5 or 10–J–Q–K–A, but cannot wrap through the ace.

A same-suit 10–J–Q–K–A is displayed as Royal Flush. It uses the Straight Flush base values rather than a separate score tier.

### 4.5 Worked Examples

**Pair:** 8 of Hearts, 8 of Spades, 2 of Clubs, 5 of Diamonds, King of Clubs produces (10 + 8 + 8) × 2 = **52**. The three kickers add no chips, although all five cards become physical placements.

**Flush:** 2, 4, 6, 8, and 10 of Hearts produces (35 + 2 + 4 + 6 + 8 + 10) × 4 = **260**.

**Royal Flush:** 10, Jack, Queen, King, and Ace of one suit produces (100 + 10 + 10 + 10 + 10 + 11) × 8 = **1,208**.

<!-- page -->

### 4.6 Card Mass and Structural Properties

All four suits share the same mass for a given rank. The current data follows **mass = 0.05 + rank × 0.01**, with Ace assigned rank 1 and King rank 13. The tower mass multiplier is 1.

| Rank | Chip contribution | Mass | Rank | Chip contribution | Mass |
| --- | --- | --- | --- | --- | --- |
| Ace | 11 | 0.06 | Eight | 8 | 0.13 |
| Two | 2 | 0.07 | Nine | 9 | 0.14 |
| Three | 3 | 0.08 | Ten | 10 | 0.15 |
| Four | 4 | 0.09 | Jack | 10 | 0.16 |
| Five | 5 | 0.10 | Queen | 10 | 0.17 |
| Six | 6 | 0.11 | King | 10 | 0.18 |
| Seven | 7 | 0.12 | | | |

The configured card collider is 1.5 units long, 1.07 units wide, and 0.02 units thick. The gameplay scene leans supports 27 degrees from vertical, permits four base tents, and uses a 90-pixel snap radius. These are tuning values, not player-upgrade statistics.

Mass influences physics, but it does not guarantee that a particular card or combination will stand or fall. Contact geometry, support completeness, animation, collisions, and settling time also matter. The ace's combination of low mass and high chip value should be examined during balancing.

### 4.7 Difficulty Curve

Difficulty emerges from the current structure and the remaining deck, rather than from a programmed increase in gravity or a timed enemy schedule. Early turns introduce tent and floor construction with many cards still available. Later turns inherit occupied positions, physical displacement, and a smaller draw pile.

Keeping useful cards can improve later combinations, while spending them now provides immediate score and fills structural positions. Exchanges improve selection but are limited each turn. A collapse can reopen space and allow rebuilding, so it is not always a purely negative outcome under the current scoring rules.

There is no implemented difficulty selector, progression currency, perk tree, shop, or permanent upgrade system. Any future difficulty changes should first establish a consistent end condition and decide how much tower survival contributes to success.

### 4.8 Score Reveal and Game Feel

The reveal displays the hand's base chips and multiplier, adds scoring cards in placement order, then adds the completed result to the total. MainGame uses a 0.2-second delay between reveal steps. When a new turn starts during an unfinished reveal, the scorer completes it immediately before clearing the per-turn display.

Card selection uses lift and scale, previews move smoothly toward slots, and the camera follows tower growth. These responses should make planning comfortable and resolution readable. Animation variety and audio should support the result without hiding the state of the structure.

<!-- page -->

## 5 Assets and Presentation

### 5.1 UI and UX

The existing visual identity uses a dark navy background, gold and cream borders, red illuminated headings, casino chips, and block-style lettering. The main-menu image presents NEO in slot-machine reels above the Tower of Card title. This is the visual direction for the menus and score panels.

The gameplay interface contains turn progress, cards placed, a state label, card details or instructions, deck and used-card counts, the hand name, chips, multiplier, turn score, total score, and End Turn. The physical hand and tabletop should remain readable beside the ornamental score panel.

The scripts support a remaining-exchanges label, but that text reference is unassigned in MainGame. The count should be made visible before the exchange allowance is treated as a fully communicated rule. Recommended accessibility work includes readable rank labels, a text or shape cue alongside the preview colors, and adequate score contrast.

### 5.2 Art Inventory

| Asset group | Existing content | Intended use |
| --- | --- | --- |
| Card data | 52 rank-and-suit assets with mass values | Card identities and physical tuning |
| Standard card models | A full normal-card prefab set and card textures | Recognizable faces and suits |
| Rigged card models | A full rigged-card prefab set and shake model source | Animated physical cards |
| Broken card | Broken model, prefab, and break-effect content | Collapse feedback |
| Environment | Tabletop model, prefab, and textures | Play surface |
| Menu art | main.png | Main menu branding and button artwork |
| Score art | Score Panel.png and ScoreSum.png | Gameplay HUD and final results |
| Accent art | Spark.png and additional image assets | Menu sparkle and decorative treatments |
| Typography | NTBrickSans font asset | Block-style UI lettering |

### 5.3 Art Direction

Retain the existing casino framing while keeping the tower itself visually simple enough to inspect. Rank, suit, card boundaries, and valid placement positions carry gameplay information. Gold trim and sparkle belong around the interface; they should not compete with snap markers or disguise where one card ends and another begins.

The supplied menu and score images are presentation assets, not evidence of an additional slot-machine mechanic. The game contains no implemented reel-spinning or wagering system.

<!-- page -->

### 5.4 Sound

The project contains ten WAV files and a sound database covering menu music, gameplay music, interaction effects, scoring, breaking, and run ending. Their functional mapping is listed below; musical genre and final mix targets remain to be set through listening and playtesting.

| File | Intended cue | Current integration |
| --- | --- | --- |
| MainMenu_OST.wav | Menu music | Loops when MainMenu loads |
| TowerOfCard_OST.wav | Gameplay music | Loops when MainGame loads |
| SelectCard.wav | Card selection | Called when selection is set |
| UnSelectCard.wav | Deselection or exchange | Used by both actions |
| PlayCard.wav | Card placement | Called on normal snap placement; first placement lacks this call |
| DrawCard.wav | Drawing a card | Available in database and helper; normal draw path does not call it |
| CardBreak.wav | Physical card breaking | Called when a card breaks |
| CalculateScore.wav | Completed score reveal | Called when the timed reveal reaches completion |
| UI_Button.wav | Button response | Used by gameplay and scene-navigation controls |
| EndGameSFX.wav | Run-end feedback | Plays when the game-over panel opens; music stops |

AudioManager persists across scenes and separates music, gameplay SFX, and UI sources. Three mixer assets are present. Player-facing volume controls and saved audio preferences are not implemented in the reviewed scripts.

### 5.5 Presentation Flow

**Main Menu:** Show the existing title image, Play, Quit, and sparkle motion. Play leads directly to the tabletop scene.

**Construction:** Present the available cards, turn allowance, card details, legal slots, and End Turn. Keep the active card easy to distinguish from the rest of the hand.

**Resolution:** Lock placement, release the tower, and reveal hand scoring. The next active turn freezes surviving tracked cards and restores interaction.

**Final Result:** Fade in the score panel, stop gameplay music, play the end cue, and offer replay or return to the menu. The total must include the last scored hand before the result is considered final.

### 5.6 Recommended First-Time Guidance

A dedicated tutorial is not implemented. A small first-run guide should teach the table click, two-card tent, bridge, and end-of-turn physics release. It should also state that the cards played in one turn form the scoring hand. This prevents players from assuming that neighboring cards or tower levels are evaluated as separate poker hands.

<!-- page -->

## 6 Idea Sources

### 6.1 Reference Comparison

| Source | Contribution to the design | How Neo Tower of Cards uses it |
| --- | --- | --- |
| A real house of cards | Leaning pairs, bridges, support dependencies, and fragile balance | Cards form a stepped physical structure with a turn-end settling phase |
| Standard poker combinations | Recognizable rank-and-suit patterns | Each turn's placements are evaluated as one hand |
| Balatro-style scoring | Base chips, card contributions, multipliers, and staged numerical feedback | The scoring script explicitly uses this model and the listed starting hand values |
| Existing casino artwork | Gold framing, illuminated lettering, chips, and slot-machine branding | Provides presentation language for menus and score panels |

### 6.2 The Shared Idea

The central overlap is a card that has a numerical role and a physical role at the same time. Poker supplies the reason to choose particular ranks and suits. House-of-cards construction supplies the positions and the structure those choices must occupy. Turn-based resolution gives the player a predictable interval for planning and a visible interval for watching consequences.

The design combines these sources without currently adding betting, deck upgrades, special jokers, or a campaign. The score system has an extension hook for effects, but no reviewed component uses it to grant a height bonus or a special-card ability.

### 6.3 Direction for Further Development

The next design step is to decide how closely score and construction success should depend on each other. The current design rewards the played combination immediately, while the tower determines what can happen next. That relationship can remain the game's identity if space management and rebuilding create enough meaningful decisions.

If playtesting shows that players ignore structure because collapse has little cost, a survival or construction objective could be prototyped. It should be treated as a new design experiment with its own balancing work, rather than described as an existing feature. Arbitrary influence percentages are unnecessary until the team agrees they would help communicate direction.

## 7 Technical Specifications and Scope

The current project uses Unity 6000.5.8f1, Universal Render Pipeline 17.5.0, Input System 1.20.0, Unity UI, and TextMesh Pro. Gameplay is implemented in C# with ScriptableObject card data, rigidbody physics, collider-based interaction, animation controllers, and scene-based navigation.

<!-- page -->

### 7.1 System Responsibilities

| System | Responsibility |
| --- | --- |
| TurnManager | Start, active, and end phases; placement budget and configured turn limit |
| TowerBuilder | Tower origin, legal slots, placement previews, physics release, and reset |
| Card and CardData | Identity, mass, model fitting, frozen state, impact breaking, and effects |
| DeckManager and HandManager | Initial deck, dealing, held cards, selection, and used-card tracking |
| DiscardManager and DiscardZone | Limited card exchanges through the tabletop target |
| PokerHandEvaluator and ScoreManager | Combination recognition, chip contributions, reveal, and total |
| GameUI and FinalScores | HUD, markers, button state, and final-score presentation |
| OrbitCamera and SceneLoader | View movement and navigation between menu and game |
| AudioManager and SoundDatabase | Sound routing, looping music, and interaction cues |

### 7.2 Playable Scope

The existing scope contains one main gameplay table, a standard card set, construction and collapse, turn-based hand management, poker scoring, menu navigation, and final-score presentation. Additional scenes named SampleScene, Immer, and Filly exist; their presence does not establish additional designed game modes.

No release date, team assignment, production budget, target frame rate, or minimum hardware specification is established here. Those require production decisions and testing rather than assumptions from the previous GDDs. The names and credits in older projects should not be carried into this game's team roster automatically.

### 7.3 Build and Delivery Notes

MainMenu is the intended player entry point, but EditorBuildSettings currently lists SampleScene first. A release build should start at the menu. Runtime Card and CardData files also import UnityEditor.Animations; unused editor-only imports should be removed or editor dependencies isolated before validating a standalone player build.

The project should be launched through the menu when testing the complete flow, because gameplay scripts expect an AudioManager instance. The final deliverable should verify both normal startup and scene reloads rather than rely only on running MainGame in isolation.

### 7.4 Scope Boundaries

Persistent high scores, save/resume, achievements, multiplayer, controller support, a tutorial screen, settings menus, special cards, shops, and progression are outside the implemented scope. These are possible future additions, not requirements inferred from the casino theme.

<!-- page -->

## 8 Design Decisions and Validation

### 8.1 End Condition and Result Ownership

**Observed:** TowerBuilder checks for no slots or no cards in hand and starts a three-second game-over delay. The empty-hand test does not require the draw pile to be exhausted or the turn to be active. A normal five-card play from a five-card remaining hand can therefore request game over before the next deal. The calculated combined deck-and-hand count is not used to decide the outcome.

**Recommended:** Use one run-ending controller. After scoring and physical resolution, check the turn cap, remaining playable resources, and legal construction state. Treat an empty hand with drawable cards as a normal transition. Cancel interaction and automatic dealing once a final result has been requested.

### 8.2 Full Tower and Collapse

**Observed:** Four base tents permit a full 26-card pyramid, and no slots can end the run. Destroying all tracked base cards instead resets the structure and permits rebuilding. A full tower therefore has a harsher immediate outcome than a complete collapse.

**Decision:** Decide whether a full tower ends a successful attempt, starts a new tower while retaining the score, or requires a different construction objective. Decide separately whether total collapse is recoverable. The GDD should not label either event a win or loss until this is resolved.

### 8.3 Relationship Between Survival and Score

**Observed:** Placed-card data scores even if its physical card breaks. No point penalty, survival multiplier, or height bonus is applied.

**Recommended baseline:** Keep the current formula while validating the core loop, and explicitly teach that points come from played combinations. If construction has too little strategic value, test one additional objective at a time. Do not silently replace the current scorer with a survival rule in the final specification.

### 8.4 Turn Limit and Final Score Timing

**Observed:** TurnManager emits its game-over event at the end of the final turn, then sets IsGameOver after three seconds. Automatic advancement still has its own timer. TowerBuilder uses a separate flag and delay. The game-over panel appears but does not itself pause the simulation or cancel all further actions.

**Required behavior:** Complete the final hand once, lock the run against additional placements and turns, resolve the intended physics interval, then show a stable final total. Validate the turn-10 boundary and any simultaneous tower-ending condition.

<!-- page -->

### 8.5 Physical State and Legal Positions

**Observed:** Slots are calculated from logical tent and floor keys, while survivors can freeze in displaced positions. An unbroken fallen support may still unlock positions as if it were standing in its original slot.

**Decision:** Either accept this as an abstract construction rule or reconcile support validity with physical pose. Players need a consistent explanation of why a position is legal after collapse.

### 8.6 Communication and Feedback Gaps

Connect the remaining-exchanges label in MainGame. Apply the placement sound to the first card if all placement actions should share that cue. Connect DrawCard.wav to the draw path if it is intended to accompany dealing. Decide whether returning early from a scoring reveal should also play its completion cue. Confirm that current shake animations communicate a physical resolution rather than an unimplemented random hazard.

### 8.7 Acceptance Checks

| Check | Expected result for the agreed baseline |
| --- | --- |
| Start from the menu | Play opens MainGame; eight cards appear; turn 1 begins |
| Complete a tent and bridge | Only positions meeting the logical support rules appear |
| Play five cards | Turn ends once and scores only those five cards |
| End with a smaller hand | Applicable partial combinations score; no cards means no score |
| Retain three cards | Next turn draws at most five, stopping at eight held cards |
| Exchange a selected card three times | Hand size is preserved; a fourth exchange is blocked |
| Exhaust the draw pile | Used cards do not reshuffle; normal dealing stops |
| Resolve a pair of eights with three kickers | Score is 52 |
| Resolve the sample flush | Score is 260 |
| Break a played card during resolution | Its recorded scoring contribution remains |
| Lose every tracked base card | Remaining structure clears; a new table start is available if the run continues |
| Reach a full tower or empty hand | Apply the agreed run-end rule without a delayed false result |
| Finish turn 10 | Final hand is counted once; no extra playable turn begins |
| Replay and return to the menu | Score, deck, tower, panel, and music enter their intended fresh states |

These are validation targets for the next implementation pass. This GDD is based on code and asset inspection; it does not claim that a full interactive playthrough or standalone build has passed them.

<!-- page -->

## 9 Source Register

### 9.1 Design Style References

The structure follows the numbered overview, short description, game view, core activity, rules, gameplay loop, contextual systems, assets, idea sources, and technical sections used in the previous documents. The design pillars and engagement framing also draw on the one-page concept format. Their game-specific mechanics and production claims are not transferred to this project.

- Fart or Faster — https://docs.google.com/document/d/1jMnMvoEUYoT21mgTtFblSqjrFaRTDGnswIwF7pW3Zlc/edit
- Neuteteum GDD3 — https://docs.google.com/document/d/1J6pNtTBbkTmfP-xqyL_K6APkbKleTHUQlpoSZU7QE3I/edit
- Unknown Project 1 Page Document — https://docs.google.com/document/d/1P-LAkoP0b7co9ccPYvFVHfowPW7g2tj8XhKkUO9cAYs/edit

### 9.2 Project Evidence

| Evidence | Used to establish |
| --- | --- |
| Assets/Scripts, all 23 C# files | Gameplay flow, scoring, interaction, physics, effects, camera, menus, and audio |
| Assets/Scenes/MainGame.unity | Active rules, timings, hand size, deck composition, UI references, and construction settings |
| Assets/Scenes/MainMenu.unity | Main-menu controls and scene navigation |
| Assets/CardData, 52 card assets | Actual ranks, suits, model references, and mass values |
| Assets/Perfabs/Cube.prefab | Active tower-card prefab and 1.5 impact break threshold |
| Assets/Images | Existing menu, HUD, result art, and visual direction |
| Assets/Sound and SoundScript/GameSounds.asset | Ten WAV assets and sound-event mappings |
| Assets/ImportAsset and animation content | Table, normal and rigged cards, broken model, textures, and controllers |
| Packages/manifest.json and ProjectSettings | Engine version, dependencies, and current build-scene order |

### 9.3 Specification Priority

When a script default differs from the configured MainGame scene, this document uses the scene's value. Card masses come from the actual card assets, and the break threshold comes from the prefab referenced by MainGame. Comments describing optional future effects are not treated as implemented systems.

The next revision should incorporate the team's choices from Section 8 and observations from a complete playthrough. That revision can turn the current baseline into a final rules specification without confusing prototype behavior with an approved design decision.
