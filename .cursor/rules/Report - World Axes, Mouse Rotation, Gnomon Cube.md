# 3D Viewer — Three.js Z-Up Orbit & Gnomon: Agent Reference

> **Scope:** This document covers the canonical solutions to six recurring issues when building a Z-up (NX CAM convention) 3D viewer with Three.js: gnomon world-axis tracking, XY-floor alignment, cube-face snap, orbit pole lock, orbit pole jump, and snap screen orientation. Read this before touching the orbit controller or gnomon code.

---

## Coordinate Convention

This viewer uses **Z-up**: Z points to sky, XY is the floor plane. This matches Siemens NX CAM. Three.js defaults to Y-up — every axis decision below is made with Z-up in mind and must stay consistent.

---

## Issue 1 — World Axes Not Following Mouse Rotation

### Root Cause

The gnomon had two separate groups: one for the cube (which rotated) and one for the world axes (kept at identity). The gnomon's internal camera was fixed. When the main camera orbited, the gnomon cube responded but the axis lines stayed visually frozen.

### Wrong Approach

A single `gnomonOrientGroup` rotated by `camera.quaternion.invert()`. Intent was to counter-rotate the axes so they appeared world-aligned. Result: axes appeared fixed in screen space instead.

### Correct Solution

**Move the gnomon camera — never rotate the gnomon scene.**

Keep the gnomon group (cube + axes) at world identity. Each frame, mirror the main camera's viewing direction into the gnomon camera:

```js
const dir = new THREE.Vector3()
  .subVectors(camera.position, target)
  .normalize();

gnomonCamera.position.copy(dir.clone().multiplyScalar(gnomonCamDist));
gnomonCamera.up.copy(camera.up);
gnomonCamera.lookAt(gnomonTarget);
```

The gnomon elements never move. The gnomon camera moves. Because the gnomon scene contains world-aligned axes and the gnomon camera always mirrors the main camera's viewpoint, the axes rotate exactly with mouse input — matching Siemens NX behaviour.

---

## Issue 2 — World Axes Not Aligned to XY Floor Plane

### Root Cause

Three.js defaults to Y-up. If axis geometry was constructed with Y as the vertical axis, and the camera set up with Y-up defaults, the axes appeared skewed relative to the part geometry and floor grid.

### Correct Solution

Two things must be consistent:

1. **Gnomon axis geometry** must explicitly point:
   - X along `(1, 0, 0)`
   - Y along `(0, 1, 0)`
   - Z along `(0, 0, 1)`

2. **Camera up vector** must be initialised and maintained so world Z is treated as "up." Read it from the orbit quaternion's local-Y (see Issue 5).

> **Key insight:** Build gnomon axes in world coordinates, not camera-local coordinates. Screen-aligned sprites or local vectors will not match the main scene's grid and part.

---

## Issue 3 — Gnomon Cube Not Aligned to World Axes

### Root Cause

The gnomon cube's face normals did not match the axis convention. In Three.js `BoxGeometry`, face material order is:

| Index | Face |
|-------|------|
| 0 | +X |
| 1 | −X |
| 2 | +Y |
| 3 | −Y |
| 4 | +Z |
| 5 | −Z |

Snap-view directions were programmed with wrong indices, so clicking the "top" face did not snap to the +Z view. An accidental rotation was also baked into the cube transform from an earlier fix attempt.

### Correct Solution

Define an explicit lookup table mapping face material index → world normal:

```js
const GNOMON_LOCAL_FACE_NORMALS = [
  new THREE.Vector3( 1,  0,  0),  // 0: +X face
  new THREE.Vector3(-1,  0,  0),  // 1: −X face
  new THREE.Vector3( 0,  1,  0),  // 2: +Y face
  new THREE.Vector3( 0, -1,  0),  // 3: −Y face
  new THREE.Vector3( 0,  0,  1),  // 4: +Z face (top)
  new THREE.Vector3( 0,  0, -1),  // 5: −Z face (bottom)
];
```

Keep the gnomon group transform at **identity** (no baked-in rotation or scale). Use a plain `BoxGeometry`. Raycasting uses `intersection.face.materialIndex` to look up the snap direction.

---

## Issue 4 — Mouse Rotation Locks Near the Top View

### Root Cause (Original Spherical Coordinates)

```js
spherical.phi = Math.max(0.05, Math.min(Math.PI - 0.05, spherical.phi));
```

This clamp stopped `phi` from reaching the top/bottom pole. Once the user dragged close to the top, further drag was swallowed. The camera literally stopped responding.

### Intermediate Fix (Still Had Lock)

After replacing spherical coordinates with a quaternion orbit, the lock partially remained because horizontal drag was wired to rotate around **world Z**:

```js
const rotH = new THREE.Quaternion().setFromAxisAngle(
  new THREE.Vector3(0, 0, 1), -dx * 0.005
);
```

**Why this re-introduces a lock at the pole:** When the camera is at the top pole, its position vector is `(0, 0, radius)` — it sits on the Z-axis. Rotating around world Z only spins the camera around that axis; the camera position does not change, so the view does not change. Left/right drag produces no movement.

### Correct Solution — Screen-Space Orbit

Replace the fixed world-Z axis with the camera's own up and right vectors, read from the orbit quaternion each frame:

```js
const cameraUp    = new THREE.Vector3(0, 1, 0).applyQuaternion(orbitQuat);
const cameraRight = new THREE.Vector3(1, 0, 0).applyQuaternion(orbitQuat);

const rotH = new THREE.Quaternion().setFromAxisAngle(cameraUp,    -dx * 0.005);
const rotV = new THREE.Quaternion().setFromAxisAngle(cameraRight, -dy * 0.005);

orbitQuat.premultiply(rotH).premultiply(rotV);
```

**Why this eliminates the lock:**

- At equatorial views — `cameraUp ≈ projected world-Z`. Behaviour is identical to a Z-up turntable.
- At the top pole — `cameraUp` is a horizontal vector (e.g. `(-1, 0, 0)`). Dragging horizontally rotates the camera around this horizontal axis, which moves the camera position. The camera escapes the pole in any drag direction.

> **Rule:** Drag axes must always be the camera's own up and right — they are well-defined at every point on the sphere.

---

## Issue 5 — Mouse Rotation Jumps Near the Top View (X and Y Axis Swap)

### Root Cause

This was the most subtle bug. It required fixing both the orbit quaternion construction and the camera up-vector derivation.

#### Step 1 — Wrong Quaternion Construction

The initial orbit quaternion was built as `qZ(θ) · qY(φ)`. The local-Y axis of this quaternion — `(0,1,0).applyQuaternion(orbitQuat)` — does **not** equal the correct Z-up camera up vector. The `updateCamera` function was forced to re-derive the up vector each frame via projection:

```js
// worldZ projected onto the view plane
up = new THREE.Vector3(0, 0, 1)
  .sub(dir.clone().multiplyScalar(dotZ))
  .normalize();
```

#### Step 2 — Why the Projection Formula Causes the Jump

Near the top pole, `dir ≈ (ε_x, ε_y, 1)`. The projection gives:

```
up ≈ (0,0,1) - 1·(ε_x, ε_y, 1) = (−ε_x, −ε_y, 0), normalised
```

The direction of this vector is entirely determined by the tiny horizontal components `ε_x`, `ε_y`. When the camera crosses over the top and the direction changes from `(ε, 0, 1)` to `(−ε, 0, 1)` — a smooth quaternion rotation — the projected up flips from `(−1, 0, 0)` to `(+1, 0, 0)`. That is a **180° instantaneous flip**. Three.js `lookAt` sees this and re-orients the camera matrix, making X and Y axes on the gnomon appear to swap.

### Correct Solution — Fix the Quaternion Construction

Use `qZ(θ + π/2) · qX(φ)` instead of `qZ(θ) · qY(φ)`. This quaternion's three local axes are:

| Local axis | Maps to world |
|------------|---------------|
| `(1, 0, 0)` | `(−sin θ, cos θ, 0)` — camera right, purely horizontal |
| `(0, 1, 0)` | `(−cos φ cos θ, −cos φ sin θ, sin φ)` — the exact projected-worldZ camera up |
| `(0, 0, 1)` | `(sin φ cos θ, sin φ sin θ, cos φ)` — the camera offset direction |

Because the orbit quaternion's local-Y is **analytically equal** to the projected-worldZ up vector, `updateCamera` can read it directly:

```js
camera.up.copy(
  new THREE.Vector3(0, 1, 0).applyQuaternion(orbitQuat)
);
```

This vector is smooth and continuous — no singularity, no conditional branch, no flip at the pole.

#### Verification of Mathematical Consistency

- Horizontal drag `premultiply(qZ(Δθ))` advances to `qZ(θ + Δθ + π/2) · qX(φ)` — theta increments cleanly.
- Vertical drag `premultiply(qAxis(cross(worldZ, dir), Δφ))` is equivalent to advancing to `qZ(θ + π/2) · qX(φ + Δφ)` — phi increments cleanly.
- Both operations preserve the invariant: **local-Y = correct camera up**.

---

## Issue 6 — Gnomon Top/Bottom Face Snap Has Wrong Screen Orientation (X and Y Swapped)

### Root Cause

`snapViewToDirection` derived theta from `Math.atan2(dir.y, dir.x)`. For the top face `(0, 0, 1)` and bottom face `(0, 0, −1)`, both `dir.x` and `dir.y` are exactly zero. `Math.atan2(0, 0)` returns `0` in JavaScript, so `theta = 0` was used.

With `setOrbitFromAngles(0, 0)`, the orbit quaternion became `qZ(π/2)`, encoding `camera.up = (−1, 0, 0)` (world −X). Three.js `lookAt` with this up vector placed world-X pointing down and world-Y pointing right — the opposite of NX convention.

### Correct Solution

`atan2(0, 0)` is mathematically undefined at the poles. A specific theta must be chosen that produces the desired screen orientation. For NX top view (X right, Y up):

```
setOrbitFromAngles(−π/2, 0)
= qZ(−π/2 + π/2) · qX(0)
= qZ(0) · identity
= identity
```

With `orbitQuat = identity`:
- `camera.up = (0, 1, 0)` = world Y
- Three.js `lookAt` → camera-right = world X, camera-up = world Y → **X right, Y up ✓**

**Implementation — guard clause in `snapViewToDirection`:**

```js
const theta = (Math.abs(dir.x) < 1e-6 && Math.abs(dir.y) < 1e-6)
  ? -Math.PI / 2
  : Math.atan2(dir.y, dir.x);
```

---

## Summary — Rules for Future Agents

| # | Rule |
|---|------|
| 1 | **Gnomon architecture:** Always move the gnomon camera to mirror the main camera. Never rotate the gnomon scene group. |
| 2 | **Z-up orbit quaternion:** Use `qZ(θ + π/2) · qX(φ)`, not `qZ(θ) · qY(φ)`. The `+π/2` offset encodes Z-up so the quaternion's local-Y equals the correct camera up at all elevations. |
| 3 | **Camera up derivation:** Never compute camera up via the projection formula `worldZ − (worldZ·dir)·dir`. It is singular at the poles. Read it from the orbit quaternion's local-Y: `(0,1,0).applyQuaternion(orbitQuat)`. Requires rule 2. |
| 4 | **No phi clamping:** Do not clamp phi away from 0 or π. If a clamp is needed to prevent exact-pole singularity, use `1e-6`, not `0.05`. Any large clamp creates a stuck region. |
| 5 | **Rotation axes:** For screen-space orbit, use the camera's own up/right vectors as rotation axes, not world Z. This eliminates pole singularity for horizontal drag. |
| 6 | **Pole snap theta:** Whenever a snap direction is parallel to world Z, `atan2(dir.y, dir.x)` is undefined and must not be used. Substitute `theta = −π/2` to produce NX convention: X-right, Y-up for top view. |

---

## Quick Reference — Correct Render Loop Snippet

```js
// --- Orbit input handling ---
const cameraUp    = new THREE.Vector3(0, 1, 0).applyQuaternion(orbitQuat);
const cameraRight = new THREE.Vector3(1, 0, 0).applyQuaternion(orbitQuat);
const rotH = new THREE.Quaternion().setFromAxisAngle(cameraUp,    -dx * 0.005);
const rotV = new THREE.Quaternion().setFromAxisAngle(cameraRight, -dy * 0.005);
orbitQuat.premultiply(rotH).premultiply(rotV);

// --- Camera update ---
const offset = new THREE.Vector3(0, 0, radius).applyQuaternion(orbitQuat);
camera.position.copy(target).add(offset);
camera.up.copy(new THREE.Vector3(0, 1, 0).applyQuaternion(orbitQuat));
camera.lookAt(target);

// --- Gnomon camera sync ---
const dir = new THREE.Vector3().subVectors(camera.position, target).normalize();
gnomonCamera.position.copy(dir.clone().multiplyScalar(gnomonCamDist));
gnomonCamera.up.copy(camera.up);
gnomonCamera.lookAt(gnomonTarget);
```

## Quick Reference — Snap to Axis-Aligned View

```js
function snapViewToDirection(dir) {
  const phi = Math.asin(Math.max(-1, Math.min(1, dir.z)));

  // Guard: atan2(0,0) is undefined at the poles
  const theta = (Math.abs(dir.x) < 1e-6 && Math.abs(dir.y) < 1e-6)
    ? -Math.PI / 2
    : Math.atan2(dir.y, dir.x);

  setOrbitFromAngles(theta, phi); // rebuilds orbitQuat as qZ(theta + π/2)·qX(phi)
}
```
