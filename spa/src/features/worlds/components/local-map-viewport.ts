import { getViewportForBounds, type Viewport } from '@xyflow/react';

interface MapRoomExtent {
  x: number;
  y: number;
  width: number;
  height: number;
  isCurrent: boolean;
  hasCorpse: boolean;
}

export function initialMapViewport(
  rooms: MapRoomExtent[],
  canvas: { width: number; height: number },
): Viewport {
  const left = Math.min(...rooms.map((room) => room.x));
  const top = Math.min(...rooms.map((room) => room.y));
  const right = Math.max(...rooms.map((room) => room.x + room.width));
  const bottom = Math.max(...rooms.map((room) => room.y + room.height));
  const bounds = { x: left, y: top, width: right - left, height: bottom - top };
  const fitted = getViewportForBounds(bounds, canvas.width, canvas.height, 0.2, 1.1, 0.12);
  if (fitted.zoom >= 0.9) return fitted;
  const focus = rooms.find((room) => room.isCurrent) ?? rooms.find((room) => room.hasCorpse);
  if (!focus) return getViewportForBounds(bounds, canvas.width, canvas.height, 0.9, 1.1, 0.12);
  return {
    x: canvas.width / 2 - (focus.x + focus.width / 2) * 0.9,
    y: canvas.height / 2 - (focus.y + focus.height / 2) * 0.9,
    zoom: 0.9,
  };
}
