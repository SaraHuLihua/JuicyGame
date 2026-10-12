# Analysis of the web prototype (Project Juice v1-2)

Electron app, game in `resources/app.asar` -> `dist/assets/index-*.js` (Phaser 3, minified), levels in Tiled `.tmx/.tmj`.
Physics is **custom** (kinematic circle vs tile grid at 120 Hz), not a physics engine.

## KEEP
- Slingshot: drag opposite to the launch, 220 px for full power, 12 px dead zone, speed = lerp(min, max, power) per material, angle snap 5 deg, dotted trajectory preview with up to 3 bounces using the same physics.
- Two forms: Squishy (gravity x0.7, bounce 0.7 on walls, 0.97 on glass, landing friction 0.92) and Heavy "rocky" (gravity x1.6, no bounce, landing friction 0.3, breaks glass and keeps 85% speed).
- Landing = grounded AND speed <= 40 px/s; small bounces under a threshold become 0 so the worm settles.
- Bounce rule: normal speed x restitution(material, surface); floor impacts also scale the sideways speed.
- Global speed cap 1400 px/s, 120 Hz fixed step, aiming does not pause the world.
- Respawn at checkpoint, glass restoring on reset, camera/juice values (squash, pop on switch) as reference.

## ADAPT
- Custom circle physics -> Rigidbody2D, with bounce still computed in script (per material/surface pair) because Unity materials cannot do that.
- Glass break by form flag -> BreakableObject comparing impact force (mass x speed) to a threshold.
- Old "wall/glass/trampoline" -> Lint, Metal, Membrane, Brittle. Trampoline (Heavy launches off it) becomes Membrane (Squishy bounces strongly, never a landing).
- Old aim range "upward only" -> option, default Full (new levels need downward shots).
- Old air launches (with charges/heat/elasticity limiter) -> no mid-air relaunch: CanLaunch needs a valid landing.
- Old fans and cables -> reserve mechanics (Radiator Fan, Swing/Slack cable), not in this phase.

## DISCARD
- Heat / charges / elasticity limiters and pickups (replaced by the launch counter).
- Pixel units, tile-based collision, roll-off hack, Phaser scene/camera/particle code, Tiled loader, menus, audio engine, Electron shell.
