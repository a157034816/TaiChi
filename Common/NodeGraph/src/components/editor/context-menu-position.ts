export interface ContextMenuPoint {
  x: number;
  y: number;
}

export interface ContextMenuSize {
  width: number;
  height: number;
}

export interface ContextMenuViewport {
  width: number;
  height: number;
}

function clamp(value: number, min: number, max: number) {
  return Math.min(Math.max(value, min), max);
}

export function resolveContextMenuPosition(
  anchor: ContextMenuPoint,
  menuSize: ContextMenuSize,
  viewport: ContextMenuViewport,
  margin: number,
): ContextMenuPoint {
  const minX = margin;
  const minY = margin;
  const maxX = Math.max(margin, viewport.width - menuSize.width - margin);
  const maxY = Math.max(margin, viewport.height - menuSize.height - margin);
  const fitsRight = anchor.x + menuSize.width + margin <= viewport.width;
  const fitsBelow = anchor.y + menuSize.height + margin <= viewport.height;
  const fitsLeft = anchor.x - menuSize.width - margin >= 0;
  const fitsAbove = anchor.y - menuSize.height - margin >= 0;

  const preferredX = fitsRight ? anchor.x : fitsLeft ? anchor.x - menuSize.width : anchor.x;
  const preferredY = fitsBelow ? anchor.y : fitsAbove ? anchor.y - menuSize.height : anchor.y;

  return {
    x: clamp(preferredX, minX, maxX),
    y: clamp(preferredY, minY, maxY),
  };
}
