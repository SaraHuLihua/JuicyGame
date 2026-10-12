# Chain, Pulley rope, Rotating passage, Camera — how they work and how to edit them

Everything here is driven from the Inspector. No code changes are needed for normal level design.
All four systems reuse what was already in the project:

| System | Built on (existing) | New |
|---|---|---|
| Chain | `BreakableChain` (rewritten into a real link chain), `BreakableObject` (impact rule), `BreakEffect` (VFX), `Surface` | `ChainSounds` |
| Pulley | `PulleySystem`, `PulleyPlatform`, `PulleyRope` (rewritten), worm mass from `WormPhysics` | — |
| Belt drive | the pulley wheel art (`FG_Props_1_52/54/55`), `Surface`, the worm's carrier path (`ISurfaceCarrier`, like `CarrierChain`) | `BeltDrive` |
| Rotating passage | the `FG_Props_1_1` object in `MechanicsTest`, `Surface`, worm physics | `RotatingChamber` |
| Camera | `CameraFollow2D` (rewritten, same component) | `CameraZone` (optional) |
| Fan | the `Fan` object in `MechanicsTest` (`FG_Props_1_10` grille, `FG_Props_1_41` body), worm mass from `WormPhysics` | `FanAirflow`, `Fan.prefab` |
| Stepping gear | `FG_Props_1_45` in `MechanicsTest` (its own PolygonCollider2D), `WormController.Die` (like `Hazard`) | `GearStepper` |
| Trampoline | the `Trampoline` object in `MechanicsTest` (`FG_Props_1_3` head, `FG_Props_1_42` rod, `FG_Props_1_41` base), the worm's own launch (`WormLauncher`) | `Trampoline`, `Trampoline.prefab` |
| Push wheel | `FG_Props_1_55 (1)` in Area F | `PushableWheel` |
| Bounce pads | the `FG_Props_1_3` / `FG_Props_1_6` art of Area G | `BouncePad`, `BouncePad.prefab` |
| Checkpoint | `WormController` respawn | `Checkpoint`, `Checkpoint.prefab`, `Art/Checkpoint` sprites |

> Written without access to the Unity editor. The C# compiles against the Unity API, the prefab and
> scene YAML was written by hand, and nothing has been tested in Play mode yet. The checklist at the end
> lists what to look at first.

---

## 1. Chain

### Where it is
- Prefab: **`Assets/Prefabs/Gameplay/Chain.prefab`** (this is the old `BreakableChain.prefab`, renamed and rebuilt; same GUID).
- Scripts: `Scripts/Gameplay/BreakableChain.cs`, `Scripts/Gameplay/ChainSounds.cs`.
- Art: the rings from **`Assets/Art/pieces/FG_Props_2.png`**:
  - face-on rings `FG_Props_2_11`, `FG_Props_2_12`, `FG_Props_2_14`
  - side-on links `FG_Props_2_8`, `FG_Props_2_9`, `FG_Props_2_10`

  The chain alternates ring / side-on link / ring..., which is how the ready-drawn chain `FG_Props_2_18` is built, so it reads as one continuous chain.
- Test area: menu **Project Juice > Add Chain+Pulley Test Areas** adds `Area_H` to `MechanicsTest` with a short, a long and an angled chain plus a wall to hit.

### How it works
- The chain hangs from the **Chain object's position** and runs along its **local down** direction.
- At Play it builds `Link Count` small links under the object. Each link has its own Rigidbody2D, a CapsuleCollider2D and a HingeJoint2D to the link above it. The top link is hinged to the world (or to a body you choose).
- Neighbouring capsules overlap slightly, so walls cannot slip between links, and the links collide individually (the chain is not one big collider). Links ignore each other by default, which keeps the chain calm and cheap.
- Link colliders use Continuous collision, so a falling chain does not tunnel through thin walls.
- In Edit mode you see a non-saved preview of the links, plus a red **X** gizmo on the break point.

### Workflow
1. Drag `Chain.prefab` into the scene (under `Level/Gameplay` in a real level).
2. Move it to the hanging point (the top of the chain).
3. Rotate it (Z rotation) for an angled start.
4. Set **Link Count** for the length.
5. Set **Break At Link** for the break point.
6. Press Play.

Duplicate it (Ctrl/Cmd+D) as often as you like. Every copy is independent.

### Changing the length
- **Link Count** = number of links. **Link Length** = distance between two joints (0.45 by default).
  Total length = Link Count × Link Length (12 × 0.45 = 5.4 units).
- Don't stretch the chain with the Scale tool. Scaling physics bodies distorts them, so at Play a Y scale is converted into more or fewer links (scale Y 2 doubles the link count) and an X scale into thicker links, then the scale is reset to 1. Prefer typing Link Count.
- Don't put a chain under a *scaled* parent, because the link shapes would be distorted (a warning tells you).

### Angled or stretched chains (both ends fixed)
- **Top**: `PinnedInPlace` (default) hangs from the object position. `AttachedToBody` hangs from **Top Body** (for example a moving platform's Rigidbody2D). `Free` means nothing holds it.
- **Bottom**: `Free` (default) is a normal hanging chain. `PinnedInPlace` fixes the last link where it spawned, so a rotated chain spans between two points. `AttachedToBody` fixes it to **Bottom Body**.
- **Bottom Slack** (pinned bottom only): the span is this much shorter than the chain, so it hangs in a soft curve instead of a rigid bar. Keep it at 0.04–0.1.

### Fixing the ends to the walls
1. Move the `Chain` object onto the first wall, exactly where the first ring should be bolted. That point is the top pin.
2. Create an empty object (right click > Create Empty) and move it onto the other wall. Call it `ChainAnchor_R`, for example.
3. On `BreakableChain`, set **Bottom** = `PinnedInPlace` and drag that empty object into **Bottom Anchor**.
4. The chain now aims at the anchor, and **Link Count is worked out from the distance** (span ÷ (1 − Bottom Slack) ÷ Link Length), so you don't need to rotate or scale anything. Raise **Bottom Slack** for a deeper U, lower it for a tighter chain. The Edit-mode preview follows when you move either end.
5. Each pinned end gets a bolted **wall plate** (Header *Wall plates*): **Show Wall Plates**, **Wall Plate Sprite** (empty = a round plate with a bolt; drag in any bracket art you like), **Wall Plate Size**, **Wall Plate Color**. The plates are visual only.

Without a Bottom Anchor the old behaviour stays: the far end is pinned where the straight chain ends, pulled in by Bottom Slack.

### Changing the break point
- **Break At Link**: the joint above this link breaks. With 12 links, 6 breaks in the middle, 1 just under the top link and 11 above the last link.
- **Breakable** off gives an unbreakable chain.
- What breaks it:
  0. **Standing on it heavy**: **Break Under Mass** (1.5) on `BreakableChain`. When the worm stands on the chain for **Break Under Mass Delay** (0.25 s) and its mass is at least this value, the chain snaps. Rocky weighs 2.5, so it breaks the chain; Bouncy weighs 0.3, so it can walk on it.
     **Break Under Mass is also the lightest worm whose hits count** (it sets `BreakableObject` > **Minimum Mass**). Before, Bouncy could break chains too: its fast hits (up to 0.3 × 31 = 9) are above half the threshold, so they "cracked" the chain, and two or three cracks add up to a break. Now Bouncy's hits never crack or break it. Set it to 0 to go back to "any hit hard enough".
  1. **Worm impact**, through the project's existing `BreakableObject` on the Chain object, with the same rule as brittle seals: impact force = worm mass × speed into the chain. **Break Threshold** is 15 by default, which Rocky reaches easily (15–65) and Bouncy practically never (2–9). **Crack Fraction / Accumulate Damage** work as usual, so two medium hits can snap it. A hit anywhere on the chain counts. With **Break Where Hit** on (default) it snaps at the link that was hit or stood on; off, it always snaps at Break At Link.
  2. **Joint Break Force** on `BreakableChain`: the break joint also snaps on its own when overloaded. Infinity (the default) turns this off. It's hard to tune because the chain's own weight already pulls on that joint (a lot on a long, tight chain), so use Break Under Mass for "Rocky is too heavy for it".
  3. **Code or UnityEvents**: call `BreakableChain.Break()`, for example from a button, a trigger or another object's `onBroken`.
- Order on a break: the valid break event, then the joint is removed (lower part free), then the `BreakEffect` plays at the break point, then `onBreak` fires and the snap sound plays, then gravity takes over and the lower part falls, swings, hits walls and clinks.
- **Harder hit, more damage** (Header *Break strength*). A hit exactly at Break Threshold snaps one joint. Every **Force Per Extra Break** (18) above it snaps one more, up to **Max Breaks** (5), spaced **Break Spacing** (3) links apart above and below the first one. So a short Rocky drop (force about 25) snaps it once, and a fall from high up (40 u/s, force 100) breaks it into five pieces. The links near the hit are also kicked away from the worm by **Impact Push** × force (within **Push Radius** links) and spun, and the break VFX grows with **Effect Growth** and plays at every snap. Standing on it (Break Under Mass) is always a single snap. Set Force Per Extra Break to 0 for the old single snap.
- **T** (test reset) re-hangs every chain, all pieces included.

### Changing the physics
On `BreakableChain` > **Physics**:
- **Link Mass** (0.15): lighter links swing more when the worm hits them. The worm weighs 0.3 (Bouncy) or 2.5 (Rocky).
- **Gravity Scale** (0.8): fall speed. The project gravity is high (37.5).
- **Linear Damping** (0.25): air drag. Higher settles sooner.
- **Angular Damping** (1.5): kills wiggle and jitter. Raise it if the chain looks nervous.
- **Surface Type** (Metal): how the worm bounces off or lands on the chain.
- **Link Layer**: the physics layer of the links (-1 uses the Chain object's layer). Links collide with whatever that layer collides with in *Project Settings > Physics 2D > Layer Collision Matrix*. Everything in the project is currently on `Default`, so chains hit all level geometry.
- **Collider Matches Art** (new, on by default): the collision thickness follows the drawn rings (Art Scale included), times **Art Collider Fill** (0.85). This fixes the worm **floating** above the chain. In `MechanicsTest` the chain had Link Width 1.02 and Scale 4.48, so each link was 4.6 units thick while the rings are drawn about 1.8 units tall, and the worm rested on an invisible capsule well above the art. Lower Art Collider Fill if the worm still looks a little high, raise it if it sinks into the rings.
- **Link Width**: collision thickness, used only when Collider Matches Art is off or the chain has no art.

### Many chains without lag (Header *Performance*)
Every link is a Rigidbody2D with a joint. The `Chain` in `MechanicsTest` has Link Count 28 and Scale 4.48, and the Y scale multiplies the link count, so it builds **125 links**. Six or seven copies make about 800 bodies swinging at 120 physics steps per second, which is why duplicating it lagged. The fixes:
- **Sleep When Still** (on): once a chain has stopped moving for **Sleep After** (1 s), with every link slower than **Sleep Speed**, the whole chain goes to sleep and costs almost nothing. It wakes up by itself when the worm (or anything moving) touches it, and goes back to sleep when it's still again. You can unhide all your chains.
- **Continuous Collision** (off): continuous collision on every link was the most expensive part. Now only the part that falls after a break switches to continuous collision, so it still can't fall through floors.
- Links now ignore only the neighbours their capsules can actually reach, instead of every pair. That was 7,750 calls per chain when the level loaded.
- If a chain builds more than 60 links, the Console says so. The simplest extra saving is fewer links: for the same length, use a smaller **Link Count** (Count × Scale Y is what gets built).

### Changing the look
- **Ring Sprites / Bar Sprites**: swap or add variants. Leave Bar Sprites empty to get rings on every link.
- **Art Ring Pitch Pixels** (190): the ring-to-ring distance in the art, which keeps the art matched to Link Length. **Art Scale** makes the art bigger or smaller without touching physics.
- **Tint**, **Sorting Order**.

### Changing the chain sound
`ChainSounds` (on the same object):
- **Impact Clips**: metallic clinks for links hitting walls, the floor or the worm, picked at random.
- **Break Clips**: the snap sound.
- While both are empty, a synthesised metal clink and snap are used, so the chain is never silent. Untick **Use Generated Sounds When Empty** for silence.
- No audio files exist in the project yet. Drop `.wav`/`.ogg` files into `Assets/Audio/` and drag them into these arrays.
- Spam control:
  - **Min Impact Speed** (2): softer contacts are silent.
  - **Full Volume Speed** (12): the speed that plays at full volume. Volume scales with impact speed.
  - **Min Interval** (0.07 s): the minimum gap between two clinks of this chain.
  - **Max Per Second** (8): a hard cap.
  - **Per Link Cooldown** (0.25 s): the same link cannot clink twice quickly.
- **Pitch Range**, **Impact Volume**, **Break Volume**, **Spatial Blend** (0 = plain 2D).

Sound never affects physics. It only listens to the chain's events.

### Assigning a different break VFX
- **Break Effect** field on `BreakableChain`. The prefab points to the `BreakEffect` component on the same object (dark ink shards).
- Edit that component (count, colour, speed, size, life, Effect Scale), or drag in a `BreakEffect` from another object.
- Leave it empty for no VFX.

---

## 2. Pulley / rope

### Where it is
- Prefab: **`Assets/Prefabs/Gameplay/PulleyRig.prefab`**: `PulleyRig (PulleySystem)` > `Wheel`, `Rope (PulleyRope + LineRenderer)`, `PlatformA`, `PlatformB`.
- The wheel now uses the spoked pulley wheel from **`FG_Props_1_40`**.
- Test area: `Area_I` from the same menu as the chain.

### How the rope works
- `PulleyRope` draws a `LineRenderer` from **Platform A**, up to the wheel, **around the top of the wheel** (it leaves and meets the rim at the real tangent points), and down to **Platform B**.
- It is visual only, not a simulated rope, so it can never destabilise the rig.
- It is redrawn every rendered frame **after** the platforms' Rigidbody2D interpolation. Previously it was updated in FixedUpdate, which made it lag and snap against the smoothly drawn platforms. The ends are glued to the platforms and can never visually detach.
- When a platform goes down, its side gets longer and the other side shorter, because the ends follow the platforms and the wheel stays fixed.
- It also shows in Edit mode, so you can see it while placing things.

### Changing the rope's appearance
On `Rope > PulleyRope`:
- **Width**, **Color**, **Sorting Order**.
- **Rope Material**: assign a material with a rope texture and tick **Tile Texture** to repeat it along the rope. Empty uses a plain coloured line.
- **Attach Point A / B**: where the rope ties onto each platform, in the platform's local space. (0, 0.5) is the top centre.
- **Wrap Radius**: the rope radius around the wheel. 0 uses `PulleySystem.wheelRadius`.
- **Points Per Side / Points Over Wheel**: smoothness of the curve.

### Elasticity / smoothing
On `PulleyRope` > **Elasticity**. These are visual only, and the ends never move off the platforms.
- **Sag** (0.04): a constant soft bow in the middle of each side. 0 is perfectly straight.
- **Sway Amount** (0.035): how much platform starts, stops and landings make the rope sway.
- **Sway Stiffness** (90) / **Sway Damping** (9): a quick small wobble that settles fast. Raise the damping (12+) for almost no wobble, or lower it for more life.
- **Max Sway** (0.25): the limit on how far the rope can bow.

### Changing the platform movement
On `PulleyRig > PulleySystem`:
- **Max Travel**: how far each platform can move up or down from its start.
- **Move Speed**: platform speed per unit of weight difference.
- **Max Speed**: a hard cap.
- **Damping**: how quickly the speed follows the target. Higher is snappier, lower is floatier.
- **End Ease** (0.6, new): the platforms slow down over this distance before the travel limit instead of stopping dead. This used to be a hard stop.
- **Auto Return / Rest Offset**: with nobody on it, the rig eases back to this position.
- **Sink Per Weight** (new, 0 = off): 0 is the original counterweight, where a rider keeps the platform moving until Max Travel. Above 0 it works like a scale: the platform sinks this many units per unit of extra load and stops there (Bouncy sinks 0.3 × this, Rocky 2.5 × this).

#### Why the platform only comes back up a little after a Bouncy jump
The rig only returns **while nobody stands on it**, so it only has the time you spend in the air. With the values in `MechanicsTest` (Max Travel 16 and 10.7, Max Speed 6), a platform that has sunk far needs about 2 to 3 seconds to come all the way back, while a Bouncy jump keeps you in the air for roughly 0.5 to 2 seconds depending on power. It rises part of the way, then you land and it stops (or sinks again). These new fields, under *Rest behaviour*, control the return:
- **Return Strength** (4): how hard it's pulled back. Higher gets back sooner, and it never bounces past the rest position.
- **Return Speed** (0 = Max Speed): top speed while returning. With a big Max Travel this is usually the real limit, so raise it (for example 12–15) if you want it back within one jump.
- **Return Amount** (1): how much of the way back it goes each time you leave it. 1 is all the way, 0.5 is halfway, 0 is stays where it was left, like a real balanced pulley. This is the value to tune if you want a less "magic" return.
- You can also lower **Max Travel**, so it can't sink so far in the first place, or use **Sink Per Weight**.
- **Counter Bias**: permanent extra weight on side B.
- **Wheel Radius**: used for the wheel spin and the rope wrap.
- Move `PlatformA` / `PlatformB` in the Scene view to set where they start. The positions are captured at Play.

### How player weight affects it
- It uses the existing Bouncy/Rocky system and nothing else. Each side's load is the platform's **Base Weight** plus the riding worm's real `WormPhysics.Mass` × **Player Weight Multiplier**.
  - Bouncy (Squishy) has mass **0.3** and Rocky (Heavy) has mass **2.5**, so Rocky pulls about 8× harder and the platform sinks much faster. Switching material while standing (Space) changes the speed immediately.
- New, **Landing Kick** (0.35): landing on a platform gives it a dip proportional to the impact force (worm mass × landing speed, taken from the worm's own impact event). A Rocky landing yanks the rig down and Bouncy barely moves it. Set 0 to turn it off.
- The masses live in `ScriptableObjects/Worm/Worm_Squishy.asset` and `Worm_Heavy.asset`. Change them there, not on the pulley.

---

## 3. Rotating passage — `FG_Props_1_1`

### Where it is configured
- Scene **`MechanicsTest`** > `World` > `Area_B Break` > **`FG_Props_1_1`** (the hairy C-shaped ring in the cave).
- Components: `SpriteRenderer` (art), `PolygonCollider2D` (walls, generated), `Rigidbody2D` (**Kinematic**), `Surface` (Lint), **`RotatingChamber`**.
- What was wrong before: the ring was turned by a legacy **Animation** clip (`Animations/New Animation.anim`) on an object with no Rigidbody2D. That teleports the collider every frame, so the worm got pushed into it, snagged or blocked. The auto-traced collider also followed every hair spike and had a stray second piece. The Animation component is removed from this object (the clip file is untouched), and the collider is now a clean arc.

### How rotation works
- `RotatingChamber` turns the kinematic Rigidbody2D with `MoveRotation` in FixedUpdate, with smooth acceleration and braking.
- Because it is a real moving body, the worm inside is moved by contacts and friction: it rides a little up the wall and settles. It is never teleported, and it cannot pass through the walls.
- **Mode**:
  - **PlayerTriggered** (used here):
    1. The chamber waits with the opening facing **Entry Direction**.
    2. When the worm has been inside for **Enter Delay** seconds, it turns (in **Direction**) until the opening faces **Exit Direction**.
    3. The worm falls out.
    4. After the worm has left plus **Return Delay**, it turns back and waits again.
    5. **Give Up After**: if the worm stays inside at the exit, the chamber returns anyway.
  - **Continuous**: it turns forever at Rotation Speed.
  - **StopAtAngles**: it turns and pauses for **Pause At Stop** at each of the **Stop Angles**.
- All directions are world angles of the opening: **0 = right, 90 = up, 180 = left, -90 = down**. Select the object to see the gizmos: cyan is where the opening points now, green is the entry, orange is the exit (yellow for the stops).

### Inspector settings (`RotatingChamber`)
- **Rotation Speed** (deg/s), **Direction** (Clockwise / CounterClockwise), **Acceleration** (lower = softer starts and stops, which keeps the worm stable).
- **Use Start Direction + Start Direction**: the starting rotation for Continuous and StopAtAngles. PlayerTriggered always starts at Entry Direction.
- **Start Automatically**: for Continuous and StopAtAngles. Off means you call `StartRotating()` from a UnityEvent or trigger. `StopRotating()` stops it smoothly.
- **Stop Angles + Pause At Stop**: stopping at certain positions.
- Events: **On Player Entered**, **On Start Turning**, **On Opening At Exit**, **On Player Left**. Hook sounds or a camera zone here.

### How the player enters
- The worm flies or rolls in through the opening. Here **Entry Direction = -30**, which faces the opening down-right towards the corridor the worm arrives from in `Area_B`.
- "Inside" means the worm's centre is within the inner wall, which needs no trigger collider or layer setup. Quick fly-throughs shorter than **Enter Delay** (0.4 s) don't start it.

### How the opening works and how to adjust the exit / fall position
- The opening is a real gap in the generated collider, matched to the art. **Opening Local Angle** = 180 (the art opens to its left) and **Opening Width** = 54°.
- **Exit Direction** (-100) is where the opening points when the worm should drop out. -90 is straight down. A few degrees past -90 *in the turning direction* works best, because the worm rides up the wall while the chamber turns. Move it to aim the drop at a different spot below.
- **Wall Friction** (0.25) decides how far up the wall the worm is carried while turning (about 25° at 0.25, inside the 54° opening). Higher friction carries the worm further and can leave it beside the opening instead of over it.
- Shape fields (**Ring Center, Inner Radius, Wall Thickness**) are in the sprite's own units and were measured from the art. Right-click the component > **Rebuild Collider** after changing them in the editor. **Generate Collider** off keeps a collider you drew yourself.
- To reuse it elsewhere, drag the configured `FG_Props_1_1` from the Hierarchy into `Prefabs/Gameplay` to make a prefab, or add `RotatingChamber` to any ring sprite and adjust the shape fields.

---

## 4. Camera

### Exactly where to adjust zoom
1. Open the level scene (for example `MechanicsTest`).
2. In the **Hierarchy**, select **`Main Camera`**.
3. In the **Inspector**, open **Camera Follow 2D** (not the Camera component's own Size; the script sets that for you).
4. Change **Orthographic Size**:
   - It is half the visible height in world units. 14 means 28 units tall, and the width follows the screen aspect.
   - **Bigger = wider view** (more level, smaller worm). **Smaller = closer**.
   - It is now **14** in MechanicsTest (it was 9.9). Try 12–18.
   - It updates the Game view immediately, even outside Play mode.

### Exactly where to adjust follow
Same component, under **Follow**:
- **Follow Speed** (6, horizontal) / **Vertical Follow Speed** (4.5): how fast the camera catches up. Higher is snappier, lower is floatier. The camera uses SmoothDamp in LateUpdate after the worm's interpolation, so it stays smooth at any value without jitter or snapping.
- **Dead Zone** (0.4, 0.6): the worm can move this far from the aim point before the camera reacts, so tiny bounces don't shake the view. Set it to (0, 0) for a tight follow.
- **Look Ahead** (0.12 s) / **Max Look Ahead** (3): while flying, the view leans in the direction of travel so you see where you are going. Set 0 to turn it off.

### Exactly where to adjust offsets
- **Offset** (x, y), in units added to the worm's position. (0, 2) puts the worm slightly below the centre so you see more above. (3, 2) shows more to the right, and (0, -2) shows more below.

### Bounds
- **Use Bounds** plus **Bounds Min / Max**, or drag any Collider2D covering the level into **Bounds Collider**. The camera then never shows outside it. A cyan box gizmo appears when the camera is selected.

### Closer / wider camera, and different settings per level
- **Whole level wider or closer**: change Orthographic Size on that scene's Main Camera. Each scene has its own Main Camera, so each level automatically keeps its own values.
- **Same settings in many levels**: on the Camera Follow 2D component, click the **Preset** icon (slider icon, top right of the component) > *Save current to...*. In another level, apply that preset to its Main Camera.
- **Part of a level wider or closer** (for example zoom out over a big drop):
  1. Create an empty GameObject.
  2. Add a **BoxCollider2D** and **Camera Zone** (`Scripts/Core/CameraZone.cs`).
  3. Size the box over the area. It becomes a trigger automatically.
  4. Set the zone's Orthographic Size (and optionally its Offset).

  While the worm is inside, the camera blends to those values (**Zoom Smooth Time** on the camera). When it leaves, the camera blends back. If zones overlap, the highest **Priority** wins.
- No Cinemachine is installed in the project, so none was added. This component does the job without the extra package.

---

## 5. Quality pass: what was found and fixed
- **Chain: worm floating above it**: the link colliders were much thicker than the drawn rings (see *Collider Matches Art*).
- **Chain: Rocky standing on it didn't break it**: only impacts counted. Added *Break Under Mass*.
- **Rope visually lagging and disconnecting**: it was updated in FixedUpdate while the platforms are interpolated. It now updates in LateUpdate from the interpolated positions. The wheel spin is computed the same way (smooth, no drift).
- **Pulley platforms snapping** at the travel limit (hard stop): they now ease out (`End Ease`).
- **Rotating chamber trapping the worm**: the collider was teleported by an Animation and traced every hair spike, with a stray second piece. It is now a kinematic body with a clean collider and an opening.
- **Worm on rotating or moving bodies**: the "settled" check used the ground body's linear velocity only, so a worm resting in a turning chamber looked like it was sliding. It now uses the velocity of the exact ground point, including spin (`GroundDetector.Point` + `GetPointVelocity`). Nothing changes on static ground.
- **Old chain** was two big bodies with one hinge, and the lower half was one giant collider. It is now a link chain with per-link collision, Continuous detection, self-collision off and damping.
- **Unnecessary per-step work**: `PulleyPlatform` re-configured its Rigidbody2D every physics step. Now that happens once. The old chain polled for a broken joint every step; it now uses `OnJointBreak2D`.
- **Camera jitter, lag and snapping**: it now uses SmoothDamp in LateUpdate with a dead zone and look-ahead, and the zoom is owned by one Inspector field.
- `BreakableObject` gained two switches (**Disable Colliders On Break**, **Hide Sprite On Break**, both on by default so existing brittles behave exactly as before) and a `Broken` event, so composite objects like the chain can use the same break rule.

## 6. First things to check in Play mode (not verified here)
1. `Area_B`: launch into the ring from the corridor on the right. It should turn clockwise and drop you into the shaft below. If the worm misses the opening, adjust **Entry Direction**. If it doesn't fall out, adjust **Exit Direction** or lower **Wall Friction**.
2. Run **Project Juice > Add Chain+Pulley Test Areas**, then jump to `H` and `I` with `[ ]` or the digit keys:
   - Hit the chains as Rocky (they should snap, with VFX and a snap sound, and the lower part should clink against the wall).
   - Stand on the pulley as Bouncy, then switch to Rocky.
3. If a chain jitters, raise **Angular Damping** a little or lower **Link Count**. If it stretches when Rocky hits it, raise **Link Mass** slightly.
4. Camera: try Orthographic Size 12–18 and keep what feels right.

---

## 7. Belt drive (wheels + belt, all turning)

### Where it is
- Prefab: **`Assets/Prefabs/Gameplay/BeltDrive.prefab`**: `BeltDrive (BeltDrive + Surface)` > `WheelSmall`, `WheelLarge`, `WheelMedium`, `Belt (LineRenderer + EdgeCollider2D)`.
- Script: `Scripts/Gameplay/BeltDrive.cs`.
- Art: the pulley wheels from **`FG_Props_1.png`**: `FG_Props_1_52` (small), `FG_Props_1_54` (medium), `FG_Props_1_55` (large). The belt is drawn by a LineRenderer, like the pulley rope.

### How it works
- The belt is a closed loop around the wheels. It is rebuilt from the wheels' positions and radii (straight runs on the real tangent lines, arcs around each wheel), also in Edit mode. Move a wheel in the Scene view and the belt and its collider follow.
- By default the belt and every wheel turn **clockwise**. Each wheel spins at the speed that matches the belt at its rim, so the small wheel spins fastest and nothing looks like it slips.
- The belt is solid (EdgeCollider2D, Metal surface). The worm can land on it and is **carried like a conveyor**: on the top run it moves in the belt's direction, and it inherits the belt's speed when it launches. Grip is the same rule as `CarrierChain` (Bouncy picks up speed faster, Rocky lags a little more when boarding). At the end of the top run the worm rides over the wheel and drops off.
- Small light marks ("cleats") travel along the belt in Play mode so you can see it moving.

### Workflow
1. Drag `BeltDrive.prefab` into the scene.
2. Move the root to place the whole machine. Move `WheelSmall` / `WheelMedium` / `WheelLarge` to change the shape.
3. Set **Speed** and **Direction** on the root's `BeltDrive`.
4. Press Play.

Don't scale the root or the wheels with the Scale tool to resize a wheel: change its **Radius** in the `Pulleys` list and scale only the wheel sprite to match (the sprite scale is 2 × Radius ÷ sprite size in units: 2.00 for the small art, 3.48 for the medium, 4.83 for the large).

### Inspector settings (`BeltDrive`)
- **Pulleys**: the wheels, **in order around the loop** (either way round works). For each: **Wheel** (the transform), **Radius** (where the belt sits on the rim) and **Outside**.
  - Outside off (default): the wheel is inside the loop and turns the same way as the belt.
  - Outside on: an idler that presses on the belt from outside and turns the other way.
  - Add a fourth wheel by duplicating one, then adding it to the list in the right spot.
  - Wheels that overlap have no straight run between them, and the belt disappears until you move them apart.
- **Direction** (Clockwise / CounterClockwise), **Speed** (units/s, also the rim speed of every wheel), **Running**, **Spin Up Time** (soft start and stop).
- **Carries Player**: off makes the belt a plain solid surface. **Squishy Grip / Heavy Grip**: how quickly the worm is brought up to belt speed (keep Heavy above about 42 so it beats Metal friction).
- **Thickness** (also the collision thickness), **Color**, **Belt Material** (optional tiling texture), **Sorting Order** (wheels are 5, belt 6, cleats 7), **Points Per Wheel**.
- **Show Cleats / Cleat Spacing / Cleat Size / Cleat Color**.
- For UnityEvents (buttons, triggers): `StartBelt()`, `StopBelt()`, `ToggleBelt()`, `ReverseDirection()`, `SetSpeed(float)`.
- Select the object to see the gizmos: a cyan circle per wheel radius (orange for Outside idlers) and green arrows showing which way the belt runs.

### First things to check in Play mode (not verified here)
1. All three wheels and the belt turn clockwise, and the cleats move with the belt.
2. Land on the top run as Bouncy and as Rocky: you should be carried right and dropped off past the large wheel. If Rocky doesn't move, raise **Heavy Grip**.
3. If the belt sits a little off the wheel art, adjust that wheel's **Radius**.

---

## 8. Fan (blows upward)

### Where it is
- Prefab: **`Assets/Prefabs/Gameplay/Fan.prefab`**: `Fan (FanAirflow + BoxCollider2D + Surface)` > `Housing` > `FG_Props_1_10` ×3 (grille), `FG_Props_1_41` (body).
- The `Fan` object in `MechanicsTest` is now an instance of this prefab, in the same place.
- Script: `Scripts/Gameplay/FanAirflow.cs`.

### How it works
- Above the grille there is an invisible **air column**. While the worm is inside it, the fan pushes it with an upward **force**, the same force whatever the material. Like everything else in the project, the worm's real mass decides the result (there is no "if Squishy"):
  - **Bouncy/Squishy** (mass 0.3) gets 8× more acceleration than Rocky. It rises, and because the force fades towards the top of the column, it stops at the height where the air exactly carries its weight and **floats** there, bobbing gently.
  - **Rocky/Heavy** (mass 2.5, falls 1.6× faster) is barely slowed and **stays down**.
  - Switching material mid-air works: switch to Rocky to drop out of the air, and back to Bouncy to be picked up again.
- The body of the fan is solid (BoxCollider2D, Metal), so you can stand on it as Rocky.
- Visuals (never touch physics): soft air streaks rise from the grille, wave and fade out at the top, and the housing vibrates a little while the fan runs.
- Select the Fan to see the column (cyan box) and **the height where Bouncy will float (green line)**.

### Launching while floating
**Allow Launch While Floating** (on): once the worm floats (slower than **Float Max Speed**, 4 u/s, for **Float Settle Time**, 0.2 s) it can aim and launch as if it stood on the ground. The launch starts from its floating speed. Right after a launch it has to slow down and float again before it can launch once more, so it can't climb forever by chaining jumps. Floating isn't a landing: the respawn point doesn't move.

### Inspector settings (`FanAirflow`)
- **Air column**: **Outlet Center** (middle of the grille, local), **Column Width**, **Column Height**. The air blows along the object's local up, so rotating the Fan makes a sideways fan.
- **Lift**:
  - **Running**, plus `StartFan()`, `StopFan()`, `ToggleFan()` for UnityEvents and buttons.
  - **Lift Force** (20): higher makes Bouncy float higher. Bouncy needs more than about 8 to lift off, and Rocky would need more than about 150, so anything in between keeps the "Bouncy floats, Rocky stays" rule.
  - **Top Strength** (0): the share of the force left at the top. 0 makes Bouncy float inside the column. 1 means no fading, so Bouncy shoots out of the top, which is good for a launch fan.
  - **Air Drag** (1.2): calms the bobbing. **Centering** (0.6): keeps a floating worm from drifting out of the side. **Flutter / Flutter Speed**: the gentle bobbing. **Spin Up Time**: soft start and stop.
- **Air VFX**: **Show Air**, **Streak Count**, **Streak Speed**, **Streak Length / Width** (min, max), **Air Color** (alpha = transparency), **Wobble**, **Streak Sprite** (optional), **Sorting Order**.
- **Housing shake**: **Shake Target** (the `Housing` child; never the object that holds the collider), **Shake Amount**.
- **Events**: **On Started**, **On Stopped** (for a sound, for example).

## 9. Stepping gear — `FG_Props_1_45`

### Where it is
- Scene **`MechanicsTest`** > **`FG_Props_1_45`** (the 10-tooth gear at x 54, y −68). It now has a kinematic `Rigidbody2D` and `GearStepper`. Its own PolygonCollider2D is kept.
- Script: `Scripts/Gameplay/GearStepper.cs`. Add it to any other object to make it click the same way (it adds the Rigidbody2D itself).

### How it works
It turns **clockwise in clicks**: a quick turn of one tooth, a small overshoot that settles back (the mechanical "clunk"), then a pause. It's turned like the rotating chamber, by physics (MoveRotation on a kinematic body), so a worm standing on a tooth is carried along instead of glitching through.

### Inspector settings (`GearStepper`)
- **Direction** (Clockwise).
- **Step Angle** (36): degrees per click. 360 ÷ number of teeth gives one tooth per click (10 teeth = 36). Use 18 for half a tooth.
- **Step Duration** (0.15 s): how fast each click turns. **Pause Between Steps** (0.6 s): the wait between clicks.
- **Overshoot** (0.08) / **Settle Time** (0.1 s): the clunk. 0 gives a dry, exact stop.
- **Start Automatically**, **Start Delay**, and `StartTurning()`, `StopTurning()`, `ToggleTurning()` for UnityEvents.
- **Click Sound** / **Click Volume** (optional), and **On Step**, fired on every click (sound, particles, or to drive another mechanism).
- **Crushing**: **Crushes Player** (on). While the gear turns, a worm squeezed between the gear and anything else (wall, floor, another mechanism) dies, the same death as touching a `Hazard`. **Crush Opposition** (0.4) sets how face to face the two pushes must be: 1 = exactly opposite, lower catches more angled squeezes. **Crush Time** (0.04 s) ignores a tooth just brushing past. **On Crushed Player** is an event, for a sound for example. Just resting in a gap between two teeth, or being pushed along the floor, doesn't kill.

## 10. Trampoline (spring piston)

### Where it is
- Prefab: **`Assets/Prefabs/Gameplay/Trampoline.prefab`**: `Trampoline (Trampoline)` > `FG_Props_1_41` (base) and `FG_Props_1_3` (head) > `FG_Props_1_42` (rod, moves with the head).
- The `Trampoline` object in `MechanicsTest` (`Area_D tubi` > `tubo2`) is an instance of this prefab.
- Script: `Scripts/Gameplay/Trampoline.cs`.

### How it works
1. Land or stand on the head as **Rocky**. Its mass (2.5) is at least **Trigger Mass** (1.5), so the head sinks (**Compress Depth**, **Compress Time**) and shakes a little (**Charge Shake**). Bouncy (0.3) just stands on it.
2. While it's charged you **launch exactly as usual**: click, drag to aim and set the power, release. The launch is multiplied by **Power Multiplier** (2.2), so it's the normal throw with a huge boost. The aim preview shows the boosted arc. Rocky's strongest normal launch is 26.6 u/s; ×2.2 is about 58 u/s, roughly 28 units high.
3. **Max Launch Speed** (70) is the ceiling for the boosted speed (the worm's normal limit is 43.75; it's raised for that flight only). **Max Steer Angle** (90) keeps the launch within that many degrees of straight up; 90 allows anywhere in the upper half.
4. If you don't launch within **Auto Fire After** (1.5 s), it throws you by itself at **Auto Launch Speed** (45 u/s), along your aim if you're holding one, otherwise straight up. 0 = it waits for you.
5. **The head never rises more than Max Rise (0.25) above its rest position**, so the rod stays inside the base. **Fire Overshoot** (the "pop") is limited to that. **Fire Time / Return Time / Cooldown** control the spring after the throw.
- To make it stronger, raise **Power Multiplier** and, if the throw stops getting faster, **Max Launch Speed**.
- **On Charge**, **On Fire**: events for sounds or effects. `Trigger()` charges it from another script or a UnityEvent.
- Select it to see the steering limits (orange).

## 11. Push wheel — `FG_Props_1_55 (1)` (Area F)

### Where it is
- The `FG_Props_1_55 (1)` object in `MechanicsTest` (`World` > `Area F`) has a **`PushableWheel`** component. Script: `Scripts/Gameplay/PushableWheel.cs`.
- Its Rigidbody2D and CircleCollider2D are created at Play (Radius 2.38, the drawn wheel), so the object in the scene looks unchanged.

### How it works
1. The wheel stays parked where you put it, even on the slope, and is solid.
2. Push it from the side as **Rocky** (mass at least **Min Pusher Mass**, 1.5). Bouncy can't move it. It starts rolling (**Start Spin**) and from then on it's a real heavy body (**Mass** 6, **Gravity Scale**, **Rolling Resistance**, **Friction**): it rolls down the slope and drops into the hole on the right.
3. When it has come to rest at least **Min Drop** (2 units) lower than where it started, for **Settle Time**, it locks in place (Static). It's now a bridge: walk over it and carry on.
- **On Pushed** and **On Locked**: events for sound/effects. `Release(1)` frees it from a script (1 = to the right).
- **T** puts it back.
- The hole is about 13 units wide and the wheel 11.8, so its top ends about 1.5 units under the far edge. If it ends too low, raise the wheel's scale slightly (and Radius with it).

## 12. Bounce pads (zigzag to the exit, Area G)

### Where it is
- Prefab: **`Assets/Prefabs/Gameplay/BouncePad.prefab`**: the `FG_Props_1_3` cap (**`BouncePad`** script, trigger BoxCollider2D) > `Spring (FG_Props_1_6)`.
- In `MechanicsTest` > `Area G super bounce`, the 6 caps and 6 springs were replaced by **`BouncePad 1 (floor)` … `BouncePad 6 (to exit)`** and an empty **`Bounce Exit`** where the worm lands.
- Script: `Scripts/Gameplay/BouncePad.cs`.

### How it works
- When the worm touches a pad it's thrown on an exact arc into the next pad (**Next**), and the last pad throws it to **Target** (`Bounce Exit`). No aiming: it's a ride. The arc is computed from the worm's gravity at that moment, so Bouncy and Rocky follow the same path.
- **Arc Height**: how far above the higher end the arc goes. **Landing Offset**: the point in front of the cap the previous pad aims at.
- **Lock Input During Ride** (on): no aiming or switching until the worm lands after the last pad.
- Juice: the cap squashes and wobbles and the spring stretches (**Squash**, **Wobble Time**), plus **Bounce Sound** and **On Bounce**.
- **The positions were computed from the shaft's colliders**: every arc keeps at least 1.9 units from the walls (the worm's radius is 0.375), alternating right and left, up to the floor on the right of the shaft top (x 207, y 3.7), under the gears and away from the saw. The pads are at 1.8 world scale (they were 2.66), otherwise they don't fit the narrow parts of the shaft.
- To move one, select it and drag it. The green gizmo shows each arc; keep it clear of the walls. To change the exit, move `Bounce Exit`. To add a pad, duplicate one and fix the Next links.

## 13. Checkpoint

### Where it is
- Prefab: **`Assets/Prefabs/Gameplay/Checkpoint.prefab`** (**`Checkpoint`** script, trigger BoxCollider2D, SpriteRenderer). Art: `Assets/Art/Checkpoint/Checkpoint_Off.png` (dark flag hanging) and `Checkpoint_On.png` (yellow flag raised, glowing). Both are 256×512 at 128 pixels per unit (2×4 units), pivot at the bottom.
- One is placed in `MechanicsTest` (`World` > `Checkpoint`) after the wheel's hole, before the bounce shaft (x 174, y -67.4). Drag the prefab into the scene to add more.

### How it works
- Touch it with any material: the flag goes up and lights (**Pop Scale**, **Pop Time**, optional **Activate Effect**, **Activate Sound**, **On Activated**). From then on **every death sends the worm back here**, instead of the last landing. Touching another checkpoint moves the respawn there and turns the old one off.
- **Auto Respawn After** (1 s): after a death (hazard, gear...) the worm comes back by itself. R still works straight away. 0 = only R.
- **Spawn Point** (optional child) or **Spawn Height** (0.6) set where the worm reappears.
- **T** (area reset) clears the checkpoint.

## 14. First things to check in Play mode (not verified here)
1. Fan: walk into the air as Bouncy. You should rise and float near the green line. As Rocky you should stay on the ground. If Bouncy floats too high or too low, change **Lift Force**. If the air streaks are too strong or too faint, change the **Air Color** alpha.
2. Gear: it clicks clockwise one tooth at a time. If a click looks like more or less than one tooth, adjust **Step Angle**.
3. Chain: the worm should sit on the rings, not above them. Stand on it as Rocky and it should snap after about a quarter of a second. Bouncy should not break it.
4. Pulley: sink a platform, then jump as Bouncy. With **Return Speed** 12–15 it should get back during the jump.
5. Fan: float as Bouncy, then click and drag. You should be able to launch from mid-air.
6. Gear: let a tooth push you against the wall on the right (as in the screenshot). You should die. Standing in a gap between teeth should not kill you.
7. Chains: unhide all of them. The game should stay smooth; the chains still react when you hit them. Bouncy should never break them, Rocky should.
8. Trampoline: land as Rocky, wait for it to sink, then launch as usual. You should fly about twice as high in the direction you aimed, and the rod should never come out of the base.
9. Chains: fall on one as Rocky from low and from high. Low should snap it once; high should break it into several pieces.
10. Wheel: push it as Rocky. It should roll into the hole and stop there; you should be able to walk across. If it rolls away by itself at the start or doesn't reach the hole, tell me.
11. Bounce pads: go down to the bottom of the Area G shaft. You should be thrown zigzag pad to pad up to the floor on the right at the top. If a hop hits a wall, move that pad slightly or lower its **Arc Height**.
12. Checkpoint: touch it, then die on a hazard. You should come back at the flag after 1 second.
