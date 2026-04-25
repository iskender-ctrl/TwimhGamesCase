# Unity Match Puzzle Case Solution

Unity version target: **6000.3.11f1**

This repository contains a clean, modular, interview-focused implementation of a grid-based match puzzle core gameplay loop.

## Scope

- Dynamic grid board (`NxM`, default `6x6`)
- Optional shaped level layouts via ScriptableObject playable-mask
- Random tile generation with configurable fruit tile catalog (sprite-based)
- Sprite rendering auto-fits each tile to board cell size (safe for 200x200 assets and mixed PPU values)
- Layered board visuals: background panel + per-cell square slots + fruit sprites
- Special tiles can use sprite icons (Color/Bomb/Lightning) via `BoardConfig -> Visual`, replacing fruit sprite on those tiles
- Object pooling for tile views
- Adjacent swap validation with illegal move rollback
- Match detection (`3+` horizontal/vertical)
- Special tile creation rules:
  - `4` line match -> **Lightning** special
  - `5+` line match -> **Color** special
  - horizontal+vertical intersection (`T/L/+`) -> **Bomb** special
- Special trigger behavior:
  - **Lightning**: clears full row + column
  - **Bomb**: clears 3x3 area
  - **Color**: when swapped, clears all tiles of the swapped fruit kind
- Special usage:
  - specials no longer require a match to activate
  - swapping a special with any adjacent tile triggers it directly
  - direct special + special swaps use stronger combo patterns instead of just firing both normally
  - new special creation is limited to the player-initiated first match; cascades and shuffle cleanup do not spawn extra specials
- Chain reactions between specials
- If no valid move is available, board auto-shuffles until at least one valid move exists
- If player stays idle for a while, system shows suggested move hint
  - hint prioritizes moves that create specials over plain matches
- DOTween-integrated visual flow (when DOTween define is active):
  - hint preview: grow/shrink + swap-preview + return
  - match clear: fast shrink-out before pooled release
- Full deterministic resolve loop:
  - match
  - clear
  - trigger specials
  - drop
  - refill
  - auto-cascade until stable

## Run

1. Open project in Unity `6000.3.11f1`.
2. Open `Assets/Scenes/SampleScene.unity`.
3. Press Play.

`PuzzleGameBootstrap` auto-creates `PuzzleGameInstaller` if not present in scene, so the prototype is playable without manual scene setup.

## Shaped Level Setup (ScriptableObject)

1. Create `Create > TwimhGames > Puzzle > Level Layouts`.
2. In `LevelLayout` inspector:
   - use the `Levels` section to add, duplicate, remove, and reorder level entries
   - click one level from the list to make it the selected runtime level
   - edit that level with `Name`, `Width`, `Height`, and the clickable `Layout Grid`
3. Characters:
   - playable: `1`, `X`, `O`, `#`
   - blocked: `0`, `.`, `-`, `_`, empty
4. Assign that asset to `BoardConfig -> Level Layout`.
5. If `Level Layout` is assigned, board size and playable mask come from the selected level entry; otherwise `BoardConfig` width/height is used.

## Editor Quality-of-Life

- `LevelLayoutSO` custom inspector includes:
  - multi-level list management: `Add Level`, `Duplicate`, `Remove`, `Move Up`, `Move Down`
  - click-to-select active runtime level
  - click-to-toggle grid
  - quick presets: `Fill`, `Clear`, `Invert`, `Border`, `Cross`, `Diamond`
  - validation hints (playable count, disconnected groups)
- one-click bootstrap menu:
  - `TwimhGames > Puzzle > Create Default Case Assets`
  - auto-creates and links `BoardConfig`, `TileCatalog`, `LevelLayout`
  - auto-populates `TileCatalog` with fruit sprites from `Assets/Simasart/Hungry Bat/Art/Fruits`

Hint timing can be tuned in `BoardConfig -> Timings`:
- `HintDelay`
- `HintBlinkInterval`
- `ClearAnimationDuration` (match clear shrink animation length)
- `NoMoveShuffleDelay` (wait time before auto-shuffle when no valid moves remain)

Board layout can be tuned in `BoardConfig`:
- `Slot Size`: size of the square under the fruit
- `Cell Gap`: empty space between neighboring squares
- `Fruit Size`: size of the fruit sprite itself
- `Bomb Area -> Mode`: `3x3`, `5x5`, or `Custom`
- `Bomb Area -> Custom Width/Height`: custom bomb grid size when `Mode = Custom`

If DOTween is installed and either `DOTWEEN` or `DOTWEEN_ENABLED` define is active, hint uses smooth grow/swap-return preview animation and clears use shrink-out tween before release.

## Folder Structure

- `Assets/_Project/Scripts/Core`
- `Assets/_Project/Scripts/Grid`
- `Assets/_Project/Scripts/Tiles`
- `Assets/_Project/Scripts/Input`
- `Assets/_Project/Scripts/StateMachine`
- `Assets/_Project/Scripts/Match`
- `Assets/_Project/Scripts/Pooling`
- `Assets/_Project/Scripts/Events`
- `Assets/_Project/Scripts/Config`
- `Assets/_Project/Scripts/Visual`
- `Assets/_Project/ScriptableObjects`
- `Assets/_Project/Prefabs`
- `Assets/_Project/Scenes`
- `Assets/_Project/Art/Placeholders`

## Architecture Choices

- **Board orchestration**
  - `BoardManager` owns board model + view mapping and high-level board operations (spawn, swap, clear, drop, refill).
- **Model/View separation**
  - `TileModel` is pure gameplay data.
  - `TileView` is visual + collider representation.
- **Single-responsibility services**
  - `MatchFinder`: only finds matches.
  - `SpecialTileResolver`: expands special clear sets, direct special swaps, and special chain reactions.
  - `SwapController`: adjacency + swap legality + rollback handling.
  - `BoardResolver`: resolve loop orchestration.
- **State machine**
  - `GameStateMachine` explicitly gates input and gameplay flow.
- **Event-driven flow**
  - `GameEventBus` publishes state/swap/match/special/stable events to reduce direct coupling between gameplay systems and external listeners.
- **Data-driven configuration**
  - `BoardConfigSO` and `TileCatalogSO` define board rules and tile catalog.
  - Runtime defaults are created if assets are not assigned, so project remains playable out of the box.

## Patterns Used (and Why)

- **State Machine** (`GameStateMachine`)
  - Prevents invalid concurrent operations and input timing bugs.
- **Observer/Event Bus** (`GameEventBus`)
  - Keeps game loop extensible (UI, analytics, VFX hooks can subscribe without changing core logic).
- **Object Pooling** (`TilePoolManager`)
  - Avoids frequent instantiate/destroy churn during clear/refill cascades.
- **Coordinator + Service split**
  - Improves readability and testability versus one large monolithic board script.

## Known Limitations

- Indirectly detonated Color specials still use their stored base fruit kind; only direct swap activation borrows the swapped fruit kind.
- In shaped boards, gravity is vertical per-column compaction over playable slots (no side-flow/pathfinding fill).
- Input is basic click/tap + drag-swap (no advanced gesture tuning or touch UX polish).
- No gameplay UI (score/moves/goals), VFX/SFX, or production polish.
- Runtime bootstrap uses default ScriptableObject assets when present; otherwise falls back to runtime-generated defaults.

## If More Time Was Available

- Add unit/integration tests for:
  - swap legality
  - match detection edge cases
  - chain reaction determinism
  - resolve loop invariants
- Add dead-board detection + deterministic shuffle.
- Add richer color-special interaction variants (for example combining with neighboring tile color on direct swap).
- Add editor tooling to auto-create/configure default ScriptableObject assets.
- Add lightweight debug HUD subscribing to `GameEventBus`.

## Submission Notes

- Implementation prioritizes correctness and maintainability over polish.
- The core gameplay systems are intentionally separated for interview discussion:
  - swap validation and rollback
  - match finder isolation
  - special chain expansion
  - deterministic board resolve loop
  - explicit game state gating
- Placeholder visuals are simple by design to keep focus on architecture and gameplay correctness.


