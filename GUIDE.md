# Project Juice — Foundation Guide (for you, the level designer)

This is the clean foundation. You build the levels. Code is kept simple: prefab + component + Inspector.

## 1. What changed (and what was kept)

Kept (reused, not rewritten):
- `Scripts/Player/WormPhysics.cs` — mass/gravity/bounce/landing/launch/carry. Momentum is preserved on switch (no velocity reset).
- `Scripts/Player/WormLauncher.cs` — slingshot aim + preview. Still grounded-only launch.
- `Scripts/Surfaces/*`, `Scripts/Mechanics/*` (BreakableObject, CiliaCarpet, CarrierChain, Piston, RollerJaws), `Scripts/Core/*`, `ScriptableObjects/*`, `Prefabs/*`, `Scenes/MechanicsTest.unity`, `Scenes/TestArena.unity`.

Changed:
- `Scripts/Player/WormController.cs` — **material switch now works airborne**. Removed `if (!IsGrounded) return false`. Added death state: `IsDead`, `Die(reason)`, `Revived` event, `Kill()` = die + instant respawn at last landing (old behaviour), `RespawnAtLastLanding()` clears death, `ResetRun()` clears death. `CanLaunch` and `SetMaterial` both block while dead.
- `Scripts/Player/WormVisual.cs` — rewritten to play your real sprites (Idle / Transition / Death) with fallback to the old coloured ball if arrays are empty. Gameplay never reads colour; it reads `WormMaterial { Squishy, Heavy }`.
- New: `Scripts/Gameplay/Tube.cs`, `Hazard.cs`, `SimpleRotator.cs`, `Scripts/Environment/BackgroundLayer.cs`.
- New editor tools: `Scripts/Editor/WormVisualSetup.cs` (Project Juice > Wire Worm Sprites), `Scripts/Editor/LevelTemplateBuilder.cs` (Create Level Template Structure, Create Tube Prefab).
- New folders: `Prefabs/Gameplay|Environment|UI`, `Scenes/Test|Levels|Menus`, `Scripts/Gameplay|Environment|UI|Systems`, `Art/Environment|Background|UI`, `Animations/`.

Found during inspection:
- Player art exists as PNG strips only, no `.anim`/`.controller`: `Art/player/Idle` (3+3), `Art/player/Death` (4+4), `Art/player/Transition Bouncy - Rocky` (7 frames, single folder — reverse is used for the other direction). There is NO separate `Transition Rocky - Bouncy` folder.
- `Art/pieces` = 4 big sheets (`FG_Blobs_1/3`, `FG_Props_1/2`) that must be sliced in Sprite Editor; no individual chain/gear files. Blobs = decoration, Props = saws/gears/pipes/chains.
- `Art/background` = 4 industrial images. `Art/level.png` (8.5 MB) is your level reference image. `Audio/` and `Materials/` are empty.
- `Scenes/Level1.unity` is EMPTY (only Main Camera + Directional Light). Your Level 1 design lives in `level.png`, not in the scene. I did NOT build Level 1 for you.

## 2. What controls what

- Player prefab: `Prefabs/Player/Worm.prefab` = Rigidbody2D + CircleCollider2D + GroundDetector + WormPhysics + WormLauncher + LaunchCounter + WormController + child Visual (WormVisual).
- Physics numbers: `ScriptableObjects/Worm/Worm_Squishy.asset` + `Worm_Heavy.asset` (mass, gravityScale, radius, launch speeds, restitution per surface, breakSpeedKeep). Tune there, nowhere else.
- Surface numbers: `ScriptableObjects/Surfaces/Surface_*.asset` (friction, restitutionMultiplier, allowsLanding, landingGrip).
- Bouncy = low mass (0.3), high restitution, elastic. Rocky = high mass (2.5), low restitution, breaks brittles by `mass*speed >= threshold`.
- Breakable: `BreakableObject.breakThreshold` (default 20; Heavy hits 15–65, Squishy 2–9).
- Carry: `CiliaCarpet` / `CarrierChain` implement `ISurfaceCarrier`.
- Camera: `CameraFollow2D`. Test shortcuts: `DebugTools` (R respawn, T reset area, [ ] / 1..0 jump).

## 3. Important assets

- Player: `Art/player/Idle|Death|Transition Bouncy - Rocky/`
- Pieces: `Art/pieces/FG_Props_*.png` (gameplay), `FG_Blobs_*.png` (decoration)
- Backgrounds: `Art/background/`
- Reference: `Art/level.png`
- Worm profiles: `ScriptableObjects/Worm/`, surfaces: `ScriptableObjects/Surfaces/`

## 4. How the player works / material switching / animations / physics tuning

- State: `WormController.Material` (`Squishy`/`Heavy`). Visuals only reflect it.
- Switch input: Space or right-click (when not aiming). Calls `ChangeMaterial()` → `WormPhysics.SetMaterial()` → `MaterialChanged` → `WormVisual` plays transition. Works grounded AND airborne, preserves `Rigidbody2D.velocity`.
- Launch: still requires `CanLaunch` (grounded + settled). No mid-air launch.
- Anim: `WormVisual` Inspector arrays — `bouncyIdle`, `rockyIdle`, `bouncyToRocky` (7), `bouncyDeath`, `rockyDeath`. `rockyToBouncy` = reversed `bouncyToRocky`. Idle loops at `idleFps`, transition one-shot at `transitionFps` then Idle, death one-shot at `deathFps` then frozen. Squash/pop/roll still applied on top.
- Wire it: Unity menu `Project Juice > Wire Worm Sprites` (assigns all 7-frame/4-frame/3-frame strips to scene visuals + Worm prefab). Check the arrays in the Inspector afterwards.
- Tune physics: edit the two `Worm_*` assets only.
- Death: any script calls `worm.Die("reason")` → `Died` → Death anim, input/launch/switch blocked. Call `worm.RespawnAtLastLanding()` to revive. `Kill()` = Die + instant teleport (used by RollerJaws/fall-out so old tests keep passing).

## 5. Backgrounds

- Where: `Level/Background` (world sprites, NOT UI Canvas). UI lives in separate `UI` Canvas.
- Add: drag a sprite from `Art/background` under `Background`, add component `BackgroundLayer`, set `Order in Layer = -10` (gameplay stays ≥ 0), position/scale in Scene view.
- Change: select another sprite in the SpriteRenderer, or replace the GameObject.
- Multiple layers/parallax later: add several `BackgroundLayer` objects at different orders/depths; drive them from one parallax script (not included — keep it manual for now).
- Create Sorting Layer `Background` once (Edit > Project Settings > Tags and Layers) if you want names instead of numbers.
- Camera: keep `Main Camera` orthographic (≈ 7), with `CameraFollow2D`.

## 6. New level (exact steps)

1. Unity: File > New Scene (or duplicate `Scenes/TestArena.unity`).
2. Menu `Project Juice > Create Level Template Structure` → gives `Level/{Background,Gameplay,Environment,Player,Foreground}` + `UI` Canvas + 2D camera fix.
3. Background: see §5.
4. Environment (visual only, no collision): drag `FG_Blobs` slices under `Environment`, NO collider.
5. Gameplay: drag surfaces/mechanisms under `Gameplay`. Each needs its component: `Surface` (+ optional `BreakableObject`), `CiliaCarpet`, `CarrierChain`, `Piston`, `RollerJaws`, or new `Tube`/`Hazard`/`SimpleRotator`.
6. Player: drag `Prefabs/Player/Worm.prefab` under `Level/Player`. Set `Start Material` in Inspector.
7. Camera: select Main Camera, set orthographic size, drag worm into `CameraFollow2D.target` (auto-finds worm if empty), press Play → camera snaps via `DebugTools`.
8. Boundaries: surround play area with `Surface_Metal` walls/floor (or any `Surface_*` prefab scaled).
9. Test: Play. Space/right-click switches material even mid-air. Left-drag aims when settled.
10. Save to `Assets/Scenes/Levels/Level_X.unity`.

Menus/Test separation: real levels → `Scenes/Levels/`, experiments → `Scenes/Test/`, menus → `Scenes/Menus/`. `MechanicsTest` stays as the dev testbed, never as a real level.

## 7. Mechanical object from your art (example: gear)

1. In Project window select `FG_Props_1.png` → Sprite Editor → Slice (or Single sprite) → Apply. Drag the gear sprite into the scene under `Level/Gameplay` or `Level/Environment`.
2. Decide role in Inspector:
   - Pure decoration → nothing else (ensure NO Collider2D).
   - Solid obstacle → Add Component `BoxCollider2D` (or Polygon) + `Surface` (type Metal). Size collider to the artwork.
   - Deadly gear → above + `Hazard` + `SimpleRotator` (degreesPerSecond e.g. 90).
   - Rotating visual only → `SimpleRotator` only.
   - Breakable → `Surface` (Brittle) + `BreakableObject` (set threshold).
3. Position/scale, Playtest. Same sprite can be reused with different roles elsewhere — visual and behaviour are separate.

## 8. Tubes: Solid vs Traversable (your main request)

- Menu `Project Juice > Create Tube Prefab` (once) → `Prefabs/Gameplay/Tube.prefab`.
- Drag Tube into `Level/Gameplay`. In Inspector `Tube Type`:
  - `Solid` = Blocker active → worm collides, cannot enter (wall/obstacle).
  - `Traversable` = Blocker off, only `Walls/WallLeft + WallRight` collide → worm enters through open ends, hits only walls.
- Match colliders to YOUR tube art: expand `Walls`, move/resize `WallLeft/WallRight`, set `size`/`wallThickness` to taste. Never leave a box filling the passage on a Traversable tube.
- Rule: decorative tube → no `Tube` at all (or tube under `Environment` with no colliders). Same artwork, different role, no duplicate code.
- Other assets use the same idea: `Hazard` for deadly, plain collider+`Surface` for solid, nothing for decorative, `BreakableObject` for breakable.

## 9. UI later (do not build now)

- Each screen = new scene in `Scenes/Menus/` (MainMenu, Pause, LevelSelect, Death, Victory) OR prefabs in `Prefabs/UI/` loaded additively. Keep world (`Level`) and `UI` Canvas separate.
- HUD (launch counter etc.): put Text/Counter under `UI` Canvas, read `WormController.Counter/LaunchCount` via a tiny `HudCounter.cs` you will write when needed.
- Pause/death/victory: listen to `WormController.Died/Launched` and show/hide Canvas groups. No manager needed yet.

## 10. Do NOT modify / safe to modify

- Do NOT modify: `WormPhysics` bounce/landing math, `WormLauncher` preview math, `GroundDetector` probe, `GameBootstrap`/`GameSettings` unless you know why. Do not infer material from sprite colour.
- Safe: `Worm_Squishy/Heavy` + `Surface_*` assets, all prefab scales/positions, `Tube.tubeType/size`, `Hazard.killCooldown`, `SimpleRotator.degreesPerSecond`, `BackgroundLayer.orderInLayer`, `Piston/Cilia/Carrier/RollerJaws` Inspector numbers, new levels/scenes, slicing `FG_*` sheets.

## 11. Level 1 workflow (continue yourself)

`Level1.unity` is empty — `level.png` is the reference. To continue Level 1 yourself: open `Level1.unity`, run `Create Level Template Structure`, add worm instance, block out passages with `Surface_*` + `Tube` (Traversable for open tubes, Solid for closed), add hazards with `Hazard`, background under `Background`, playtest switch-in-air (Space mid-flight). I left composition to you on purpose.

## 12. Floating fix (what caused it, what changed)

Cause (measured, not guessed): your worm PNGs are 800x800 with the animal covering only ~31% of the canvas. `WormVisual` scales the whole 8-unit texture down to the collider diameter, so with `visualSizeMultiplier 1.8` the visible animal rendered at ~56% of the collision circle — about 0.6 units of daylight under it at rest. Pivot offsets were negligible (~0.07). Physics/grounding were already exact.
Changed: prefab `Worm > visualSizeMultiplier` 1.8 → 3.1 (art edge ≈ collider edge for both materials), `transitionFps` 12 → 10, `deathFps` 8 → 18 (fast death), and marker `Start_A` lifted out of the floor (y -0.14 → 1.0, rest height is ~0.91). Launch/landing math untouched.
Rule: art size = `WormVisual.visualSizeMultiplier`, physics size = `Worm_Squishy/Heavy.radius`. Never scale the Worm root (it scales the collider too). If you redraw art with less padding, lower the multiplier until the sprite edge kisses the green collider circle in Scene view (Gizmos on).

## 13. BreakableChain

Structure (auto-built, like RollerJaws): `BreakableChain > Upper` (static body, hangs from wherever you put the root) + `Lower` (dynamic body on one `HingeJoint2D` at the break point). Link sprites are visual only — no segment spam. Before break the Lower swings as a pendulum; on break the joint is destroyed and Lower falls, collides and settles with real physics.
Break paths (all Inspector): joint `hingeBreakForce/hingeBreakTorque` (Infinity = off), `breakOnWormImpact + wormImpactThreshold` (uses worm's real mass × speed: Heavy breaks ~15+, Squishy must be fast), or call `Break()` from anything. `IsBroken`, `BreakPointWorld`, `onBreak` event for sound/shake later. `Restore()` re-hangs it; test reset (T / area jump) calls it automatically.
New chain: duplicate `Prefabs/Gameplay/BreakableChain.prefab`, position the root at the anchor, set `upperLength/lowerLength/width`, assign `linkSprite` once you slice `FG_Props_2` (empty = grey squares), tune threshold, play.

## 14. PulleyRig

Structure: `PulleyRig (PulleySystem) > Wheel + Rope (PulleyRope) + PlatformA + PlatformB (PulleyPlatform)`. Movement is controlled/kinematic (MovePosition, like Piston) — stable and tunable, not a rope simulation. One `travel` scalar drives both: A.y = rest−travel, B.y = rest+travel.
Weight: each side = `baseWeight` + rider's `WormPhysics.Mass` × `playerWeightMultiplier` (Squishy 0.3, Heavy 2.5 — read live, never duplicated). Target velocity = load difference × `moveSpeed`, capped by `maxSpeed`, smoothed by `damping`, clamped to ±`maxTravel`; empty + `autoReturn` eases back to `restOffset`; `counterBias` permanently loads side B. `travel>0` = A down / B up.
Standing: a rider counts only while grounded with `Ground.Carrier == platform` — flying past never triggers. Platforms carry a `Surface` (default Metal, allows landing) so launching from them works normally. Rope is a 3-point `LineRenderer` (A top → wheel → B top) redrawn every frame; wheel spins with travel (gizmo shows travel rails when selected).
New pulley: duplicate the prefab, place the rig, move A/B to start spots (captured at Play), tune `maxTravel/moveSpeed/damping/counterBias`, play. Different levels = different Inspector values, no code.

## 15. BreakEffect (reusable break VFX)

`Scripts/Gameplay/BreakEffect.cs`: directional `SimpleDebris` burst, auto-cleaned, zero gameplay influence. Configure: `effectEnabled, offset, effectScale, shardCount, color, speed/size/life ranges`. `BreakableObject.breakVfx` fires it at the exact hit point (empty = old shards only); `BreakableChain.breakEffect` fires at the snap point; `PlaySelf()` takes no arguments so you can wire it into any `onBroken` UnityEvent. Order is always gameplay → physical change → event → VFX. New destructible later: add the component, tune colour/count, assign or wire — no code.
Test: Area_H chain snaps with VFX + a `BrittleSeal` tube with orange burst; Area_I pulley with both materials. Run `Project Juice > Build Chain+Pulley Prefabs` then `> Add Chain+Pulley Test Areas` (appends Area_H at (0,-100), Area_I at (40,-100), skips if present).
