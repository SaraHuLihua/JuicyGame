# Project Juice – Unity mechanics prototype

Core mechanics rebuilt in Unity (2D) from the web/Electron prototype. No levels, no final art.

## Setup (2 minutes)
1. Create a new Unity project: **2D template**, Unity 2022.3 LTS or Unity 6 (the code works with both, and with the old Input Manager or the new Input System).
2. Close Unity, copy this `Assets` folder into the project (merge), reopen.
3. Wait for compilation. The first time, the project builds everything by itself (or use the menu **Project Juice > Build All**):
   ScriptableObject profiles, prefabs, `Assets/Scenes/MechanicsTest.unity` and `TestArena.unity`.
4. Open `MechanicsTest` and press Play.

Guides: `GUIDE.md` (foundation) and `GUIDE_Chain_Pulley_Chamber_Camera.md` (chain, pulley rope, rotating passage, camera).

## Controls
| Input | Action |
|---|---|
| LMB press + drag + release | aim and launch (slingshot: launch opposite to the pull) |
| RMB / Space | switch Squishy <-> Heavy (only while grounded) |
| RMB / Esc while aiming | cancel the aim |
| Shift while aiming | snap angle (5 deg) |
| R | respawn at last landing |
| T | reset current test area |
| [ ] or 1-9, 0 | previous/next area, jump to area |

## Structure
```
Scripts/Core        GameSettings, GameBootstrap, GameInput, LaunchCounter, CameraFollow2D, DebugHUD, DebugTools, TestAreaMarker, PlaceholderSprites
Scripts/Player      WormController (entry point), WormPhysics, WormLauncher, GroundDetector, WormVisual, WormMaterial(Profile), ImpactInfo
Scripts/Surfaces    SurfaceType, SurfaceProfile (ScriptableObject), Surface, ISurfaceCarrier
Scripts/Mechanics   BreakableObject, CiliaCarpet, CarrierChain, Piston, RollerJaws (+RollerHit), SimpleDebris
Scripts/Editor      MechanicsTestBuilder (menu Project Juice > Build All)
ScriptableObjects   Surfaces/*, Worm/*   <- tune everything here
```
Public API other systems use: `WormController.Instance.Material`, `.IsGrounded`, `.CanLaunch`, `.LaunchCount`, `.ChangeMaterial()`, events `Landed`, `Launched`, `Impacted`, `MaterialChanged`, `Died`.
The visual is independent: replace `WormVisual` (child object) with the real curled worm; the physics never reads colours or names.

## Test areas (MechanicsTest)
A Squishy->Lint, B Heavy->Lint, C Squishy vs Metal, D Heavy vs Metal, E Membrane, F Material switching, G Brittle (thresholds 16/30/50), H Cilia, I Piston, J Carrier Chain, K Roller Jaws.

## Tuning map (web value -> Unity)
1 Unity unit = 32 px (the web tile size).
| | Web (px) | Unity (units) |
|---|---|---|
| Gravity | 1200 | 37.5 (global), Squishy x0.7, Heavy x1.6 |
| Launch speed Squishy | 200-1000 | 6.25-31.25 |
| Launch speed Heavy | 180-850 | 5.6-26.6 |
| Max speed | 1400 | 43.75 |
| Rest speed (can launch) | 40 | 1.25 |
| Stop-bounce Squishy / Heavy | 25 / 60 | 0.78 / 1.9 |
| Landing friction (sideways speed kept) | 0.92 / 0.3 | same |
| Break speed kept (Heavy) | 0.85 | same |
| Drag for full power | 220 px | 220 px |
| Dead zone | 12 px | 12 px |
| Physics step | 120 Hz | 120 Hz |

NEW values (no equivalent in the web version, all in the profiles): masses (Squishy 0.3, Heavy 2.5), Heavy body friction, restitution on Lint/Membrane/Grease/Cilia, Piston/Chain/Cilia/Jaws numbers.

## Known limits
- Written without access to Unity: scripts were compile-checked against stubs only. Expect small tuning/API fixes on first run.
- Aim preview is a static snapshot (moving mechanisms are not predicted).
- Grease is a thin Surface placed on top of other surfaces, not a separate overlay trigger.
