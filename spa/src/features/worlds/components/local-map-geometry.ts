import type { PointResponse } from '@/api/client';

export const MAP_SCALE = 12;
export const CORRIDOR_WIDTH = 4 * MAP_SCALE;
export const CORRIDOR_FLOOR_WIDTH = (17 / 6) * MAP_SCALE;
export const WALL_WIDTH = (CORRIDOR_WIDTH - CORRIDOR_FLOOR_WIDTH) / 2;
export const MAP_WALL_COLOR = 'color-mix(in oklch, var(--parchment-foreground) 78%, var(--card))';

export type HandleSide = 'top' | 'right' | 'bottom' | 'left';
export interface Doorway {
  side: HandleSide;
  offset: number;
}

export function roundedCorridorPath(points: PointResponse[]): string {
  const vertices = points.filter(
    (point, index) =>
      index === 0 || point.x !== points[index - 1].x || point.y !== points[index - 1].y,
  );
  if (vertices.length < 2) return '';
  let path = `M ${vertices[0].x} ${vertices[0].y}`;
  for (let index = 1; index < vertices.length - 1; index++) {
    const previous = vertices[index - 1];
    const corner = vertices[index];
    const next = vertices[index + 1];
    const incomingLength = Math.hypot(previous.x - corner.x, previous.y - corner.y);
    const outgoingLength = Math.hypot(next.x - corner.x, next.y - corner.y);
    const radius = Math.min(3 * MAP_SCALE, incomingLength / 2, outgoingLength / 2);
    const incoming = toward(corner, previous, radius / incomingLength);
    const outgoing = toward(corner, next, radius / outgoingLength);
    path += ` L ${incoming.x} ${incoming.y} Q ${corner.x} ${corner.y} ${outgoing.x} ${outgoing.y}`;
  }
  const end = vertices.at(-1)!;
  return `${path} L ${end.x} ${end.y}`;
}

function toward(origin: PointResponse, target: PointResponse, fraction: number): PointResponse {
  return {
    x: origin.x + (target.x - origin.x) * fraction,
    y: origin.y + (target.y - origin.y) * fraction,
  };
}

export function corridorGate(points: PointResponse[]): PointResponse & { angle: number } {
  const segments = points.slice(1).map((end, index) => ({
    start: points[index],
    end,
    length: Math.hypot(end.x - points[index].x, end.y - points[index].y),
  }));
  const segment = segments.sort((first, second) => second.length - first.length)[0];
  if (!segment || segment.length === 0)
    return { x: points[0]?.x ?? 0, y: points[0]?.y ?? 0, angle: 0 };
  return {
    ...toward(segment.start, segment.end, 0.5),
    angle:
      (Math.atan2(segment.end.y - segment.start.y, segment.end.x - segment.start.x) * 180) /
      Math.PI,
  };
}

interface RoomOutline {
  width: number;
  height: number;
  doorways: Doorway[];
  inset?: number;
}

export function roomWallPath({ width, height, doorways, inset = 0 }: RoomOutline): string {
  return (['top', 'right', 'bottom', 'left'] as const)
    .map((side) => {
      const horizontal = side === 'top' || side === 'bottom';
      const length = horizontal ? width : height;
      const position =
        side === 'bottom' ? height - inset : side === 'right' ? width - inset : inset;
      const point = (distance: number) =>
        horizontal ? `${distance} ${position}` : `${position} ${distance}`;
      const openings = doorways
        .filter((doorway) => doorway.side === side)
        .map(({ offset }) => ({
          start: Math.max(inset, offset * length - CORRIDOR_FLOOR_WIDTH / 2 - inset),
          end: Math.min(length - inset, offset * length + CORRIDOR_FLOOR_WIDTH / 2 + inset),
        }))
        .sort((first, second) => first.start - second.start);
      let cursor = inset;
      let path = '';
      for (const opening of openings) {
        if (opening.start > cursor) path += `M ${point(cursor)} L ${point(opening.start)} `;
        cursor = Math.max(cursor, opening.end);
      }
      if (cursor < length - inset) path += `M ${point(cursor)} L ${point(length - inset)}`;
      return path;
    })
    .join(' ');
}

export function doorwayJambPath({ width, height, doorways }: RoomOutline): string {
  return doorways
    .flatMap(({ side, offset }) =>
      [-1, 1].map((direction) => {
        const horizontal = side === 'top' || side === 'bottom';
        const position =
          offset * (horizontal ? width : height) + (direction * CORRIDOR_FLOOR_WIDTH) / 2;
        switch (side) {
          case 'top':
            return `M ${position} -2 L ${position} 5`;
          case 'bottom':
            return `M ${position} ${height - 5} L ${position} ${height + 2}`;
          case 'left':
            return `M -2 ${position} L 5 ${position}`;
          case 'right':
            return `M ${width - 5} ${position} L ${width + 2} ${position}`;
        }
      }),
    )
    .join(' ');
}
