import * as THREE from '../lib/three/three.module.js';
import { OrbitControls } from '../lib/three/OrbitControls.js';

let scene, camera, renderer, controls, partMesh, stockMesh, activeCanvasId = 'mazatrol-viewport';

function getCanvas(id) {
  const el = document.getElementById(id);
  if (!el) throw new Error(`Canvas #${id} not found`);
  return el;
}

function clearMeshes() {
  [partMesh, stockMesh].forEach(m => {
    if (m) {
      scene.remove(m);
      m.geometry?.dispose();
      m.material?.dispose();
    }
  });
  partMesh = stockMesh = null;
}

function buildLatheGeometry(profile, segments = 64) {
  const points = profile.map(p => new THREE.Vector2(p.radius, p.axialZ));
  if (points.length < 2) return null;
  return new THREE.LatheGeometry(points, segments);
}

function buildStockGeometry(dto, segments = 64) {
  const r = dto.stockOd / 2;
  const len = dto.stockLength + dto.workface;
  const points = [
    new THREE.Vector2(r, 0),
    new THREE.Vector2(r, len)
  ];
  return new THREE.LatheGeometry(points, segments);
}

export function init(canvasId) {
  activeCanvasId = canvasId;
  const canvas = getCanvas(canvasId);
  const parent = canvas.parentElement;
  const w = parent.clientWidth || 800;
  const h = parent.clientHeight || 400;

  scene = new THREE.Scene();
  scene.background = new THREE.Color(0x1a1a2e);

  camera = new THREE.PerspectiveCamera(45, w / h, 0.1, 5000);
  camera.position.set(200, 120, 200);

  renderer = new THREE.WebGLRenderer({ canvas, antialias: true, preserveDrawingBuffer: true });
  renderer.setSize(w, h);
  renderer.setPixelRatio(window.devicePixelRatio);
  renderer.shadowMap.enabled = true;

  const ambient = new THREE.AmbientLight(0xffffff, 0.45);
  scene.add(ambient);
  const dir = new THREE.DirectionalLight(0xffffff, 0.85);
  dir.position.set(100, 200, 100);
  dir.castShadow = true;
  scene.add(dir);

  controls = new OrbitControls(camera, renderer.domElement);
  controls.enableDamping = true;

  const grid = new THREE.GridHelper(400, 20, 0x444466, 0x333355);
  grid.rotation.x = Math.PI / 2;
  scene.add(grid);

  animate();
  window.addEventListener('resize', () => resize(canvasId));
}

function animate() {
  requestAnimationFrame(animate);
  controls?.update();
  renderer?.render(scene, camera);
}

export function resize(canvasId) {
  if (!renderer || !camera) return;
  const id = canvasId || activeCanvasId;
  const canvas = getCanvas(id);
  const parent = canvas.parentElement;
  const w = parent.clientWidth || 800;
  const h = parent.clientHeight || 400;
  camera.aspect = w / h;
  camera.updateProjectionMatrix();
  renderer.setSize(w, h);
}

export function updateSimulation(dto) {
  clearMeshes();

  const stockGeo = buildStockGeometry(dto);
  if (stockGeo) {
    stockMesh = new THREE.Mesh(
      stockGeo,
      new THREE.MeshStandardMaterial({ color: 0x334466, transparent: true, opacity: 0.25, wireframe: false })
    );
    stockMesh.rotation.x = -Math.PI / 2;
    scene.add(stockMesh);
  }

  const partGeo = buildLatheGeometry(dto.profile);
  if (partGeo) {
    partMesh = new THREE.Mesh(
      partGeo,
      new THREE.MeshStandardMaterial({ color: 0xb8c4d8, metalness: 0.3, roughness: 0.45 })
    );
    partMesh.rotation.x = -Math.PI / 2;
    partMesh.castShadow = true;
    scene.add(partMesh);
  }

  fitCamera();
}

function fitCamera() {
  if (!partMesh && !stockMesh) return;
  const box = new THREE.Box3();
  if (partMesh) box.expandByObject(partMesh);
  if (stockMesh) box.expandByObject(stockMesh);
  const center = box.getCenter(new THREE.Vector3());
  const size = box.getSize(new THREE.Vector3());
  const maxDim = Math.max(size.x, size.y, size.z, 50);
  camera.position.set(center.x + maxDim, center.y + maxDim * 0.6, center.z + maxDim);
  controls.target.copy(center);
  controls.update();
}

export function setWireframe(enabled) {
  if (partMesh?.material) partMesh.material.wireframe = enabled;
}

export function viewIso() {
  camera.position.set(200, 150, 200);
  controls.target.set(0, 0, 0);
  controls.update();
}

export function viewFront() { camera.position.set(0, 0, 300); controls.target.set(0, 0, 0); controls.update(); }
export function viewSide() { camera.position.set(300, 0, 0); controls.target.set(0, 0, 0); controls.update(); }
export function viewTop() { camera.position.set(0, 300, 0); controls.target.set(0, 0, 0); controls.update(); }

export function screenshot() {
  if (!renderer) return '';
  renderer.render(scene, camera);
  return renderer.domElement.toDataURL('image/png');
}

export function exportStl() {
  if (!partMesh) return;
  // Minimal ASCII STL from lathe mesh
  const geo = partMesh.geometry;
  const pos = geo.attributes.position;
  let stl = 'solid mazatrol\n';
  for (let i = 0; i < pos.count; i += 3) {
    const ax = pos.getX(i), ay = pos.getY(i), az = pos.getZ(i);
    stl += `facet normal 0 0 0\n outer loop\n`;
    for (let j = 0; j < 3; j++) {
      stl += `  vertex ${pos.getX(i+j)} ${pos.getY(i+j)} ${pos.getZ(i+j)}\n`;
    }
    stl += ` endloop\nendfacet\n`;
  }
  stl += 'endsolid mazatrol\n';
  window.mazatrolFiles?.downloadText('part.stl', stl, 'model/stl');
}
