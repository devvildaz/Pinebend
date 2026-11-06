# Tracks, spawning and maps

## Recipe format and compatibility

Track files use the `.pbrtrack` extension and Unity JSON serialization. Fields: `version` (integer), `title` (string), `seed` (integer), `style` (numeric `CircuitStyle` enum), `pinebend` (boolean for the built-in layout). They store a recipe, not baked geometry, crater state, cars or race progress. The same generator implementation must be used by all participants.

Example version-3 Crossroads recipe:

```json
{
  "version": 3,
  "title": "Example crossroads",
  "seed": 104729,
  "style": 5,
  "pinebend": false
}
```

| Version | Behavior |
|---|---|
| 1 | Original built-in Pinebend when `pinebend=true`; otherwise original radial seeded generator |
| 2 | Previous layout families through `LegacyStyle`; existing bridge/overpass recipes still load |
| 3 | Current generator; new Crossroads uses a level intersection |

`CircuitStyle` numeric order: Surprise=0, Speedway=1, Switchbacks=2, Canyon=3, Overpass=4, Intersection=5. Menu labels are Mix, Speed, Hairpins, Canyon and Crossroads. New `TrackRecipe.Generate` creates version 3 recipes. Do not change old seed behavior without introducing a new version and keeping earlier implementations available.

`TrackLibrary` writes to `Application.persistentDataPath/Tracks`. Current company/product settings are `DefaultCompany` / `My project`, so Windows ordinarily uses `%USERPROFILE%/AppData/LocalLow/DefaultCompany/My project/Tracks`. Query `Application.persistentDataPath` for the actual location rather than hardcoding it. Renaming company/product changes the default save location. The menu also remembers `PeerAddress`, `PeerPort` and `PeerHost` through PlayerPrefs.

Import requires an existing `.pbrtrack` file up to 8192 bytes, a supported version and a valid nonempty title of at most 48 characters. Saving writes a temporary file and atomically moves/replaces it. The filename includes title and seed. Uploading/sharing means transferring this file; there is no online track workshop or cloud library. Multiplayer sends the selected recipe automatically in START.

## Geometry and deformation

Generators output 128 ordered points forming a closed circuit. `RaceSession.ApplyLayout` moves the existing route and related scenery. `RallyRoad` builds the driving mesh/collider at runtime, with 16 subdivisions across a 12 m road and six longitudinal rows per route interval. The road has two jump profiles starting around route stations 8 and 78, with nominal heights 2.6 m and 1.7 m. Jump geometry is layered on the route; bots plan from route markers rather than the complete deformed contact mesh.

Version-3 intersections clip the overlapping road, shoulders and curbs and replace the crossing with a shared **36 × 36 m** surface. Approaches are level and jump height fades near the junction. Progress tracking penalizes switching to a distant marker branch at crossings. Four ordered, forward-only gates still determine lap validity. Test shortcuts and branch ambiguity manually after changes.

Explosions deform the rendered mesh and its MeshCollider together. A crater uses a 3.2 m radial bowl and subtracts up to 0.42 m per accepted hit; repeated blasts accumulate rather than clamping to a fixed depth. Scorched triangles receive a second material. Most outer road-edge vertices remain fixed; intersections have special edge handling. A hit still needs the crater raycast to reach the surface, and version-2+ edits filter vertices by local deck height. Thus accumulation has no explicit depth cap, but is bounded in practice by geometry/raycast/recovery behavior. Deep-hole behavior needs stress testing.

Crater edits are not saved to `.pbrtrack`. Multiplayer replays ordered crater events against the generated mesh. Rebuilding the collider on each crater may become costly under heavy combat; profile before introducing pooling or spatial subdivision.

## Initial grid

`StartingGrid.Place` orders the solo player behind bots; multiplayer uses the same ordinal name order on every machine. It plans every slot before placing any car. Rows are seven metres apart by route arc distance, with ±2.6 m lane offsets. A conservative radius and hull overlap box reject obstacles and overlapping reservations. `Physics.SyncTransforms` brackets placement.

`RallyRoad.TrySpawn` probes the actual current road collider under the center and four chassis corners. All probes must succeed, normals must have Y ≥0.88, height spread must be ≤1.15 m, and the lowest contact must not be more than 0.65 m below the requested route point. The car root is placed 0.90 m above the highest contact. Version-2+ casts start at the requested deck height +4 m to avoid selecting a distant upper bridge surface.

If a complete safe starting grid cannot be reserved, the race stays held and offers track selection. Do not silently fall back to unsafe positions. Existing grid checks use a synthetic ground probe: they prove reservations, not runtime WheelCollider settling or every scene obstacle.

## Recovery

`TestCar` tracks the last safe on-road segment. Recovery searches backward from it, trying center and ±3.5 m lanes using the road probe, and compares clearance to other active cars. It waits and retries after 0.25 s if no sufficiently clear candidate exists (squared clearance below 16). It aims for squared clearance above 25 before stopping the search. Rejoin placement remains behind unpassed gates to discourage shortcut advancement.

Do not assume recovery can use every crater floor or steep incline: its conservative probe intentionally rejects them. Larger replacement physics rigs need updated footprint checks, collision clearance, spawn height and grid separation. Cosmetic model changes alone do not change these assumptions.

## Maps

`CircuitMap` draws road triangles, start line and racer markers into a single 320 × 320 texture using one bottom-left-origin coordinate transform. This avoids mismatched GUI/world coordinate spaces. Runtime geometry comes from `RallyRoad.CreateMap`, including the intersection patch; menu preview uses recipe edges.

The corner map centers on the local car, spans approximately 95 m and rotates to **car-forward**, using `car.transform.forward`. It does not rotate with rear camera view. The M overview stays world-north-up and fits full track bounds; the HUD scales its displayed area to the viewport. Both road and all markers must continue using the same origin, axes and scale.

This is a 2D XZ map; it does not distinguish bridge altitude or visualize crater depth. The texture is a route footprint, not a realtime satellite renderer. The menu reserves a bounded preview area and stacks/scrolls at narrow widths. `MenuPreviewCheck` is an editor-only preview utility for this layout.
