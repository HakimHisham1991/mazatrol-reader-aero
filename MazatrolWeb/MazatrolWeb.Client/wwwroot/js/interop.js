import * as threeScene from './three-scene.js';

window.mazatrolThree = {
  init: (canvasId) => threeScene.init(canvasId),
  resize: (canvasId) => threeScene.resize(canvasId),
  updateSimulation: (dto) => threeScene.updateSimulation(normalizeDto(dto)),
  setWireframe: (enabled) => threeScene.setWireframe(enabled),
  viewIso: () => threeScene.viewIso(),
  viewFront: () => threeScene.viewFront(),
  viewSide: () => threeScene.viewSide(),
  viewTop: () => threeScene.viewTop(),
  screenshot: () => threeScene.screenshot(),
  exportStl: () => threeScene.exportStl()
};

window.mazatrolFiles = {
  downloadBytes(fileName, bytes, contentType) {
    const blob = new Blob([new Uint8Array(bytes)], { type: contentType });
    triggerDownload(fileName, blob);
  },
  downloadText(fileName, text, contentType) {
    const blob = new Blob([text], { type: contentType });
    triggerDownload(fileName, blob);
  }
};

function triggerDownload(fileName, blob) {
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = fileName;
  a.click();
  URL.revokeObjectURL(url);
}

function normalizeDto(dto) {
  return {
    stockOd: dto.stockOd ?? dto.StockOd,
    stockId: dto.stockId ?? dto.StockId,
    stockLength: dto.stockLength ?? dto.StockLength,
    workface: dto.workface ?? dto.Workface,
    profile: (dto.profile ?? dto.Profile ?? []).map(p => ({
      radius: p.radius ?? p.Radius,
      axialZ: p.axialZ ?? p.AxialZ
    }))
  };
}
