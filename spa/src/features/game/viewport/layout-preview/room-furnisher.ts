import type { Rect } from './district-generator';
import type { Interior, PreviewRoom } from './interior-generator';
import { intersects, isSolid, type ScaleProp } from './preview-population';
import { footprint, roomPlan } from './room-plans';

export function furnishRoom(room: PreviewRoom, interior: Interior, blocked: Rect[]): ScaleProp[] {
  const plan = roomPlan(room, interior).map(({ spec, x, y, rotation }) => ({
    ...spec,
    ...footprint(spec, rotation),
    rotation,
    x: room.x + x,
    y: room.y + y,
  }));
  const fits = (rect: ScaleProp) =>
    rect.x >= room.x &&
    rect.y >= room.y &&
    rect.x + rect.width <= room.x + room.width &&
    rect.y + rect.depth <= room.y + room.depth;
  const free = (rect: ScaleProp, others: Rect[]) =>
    !others.some((other) => other.width > 0 && other.depth > 0 && intersects(rect, other, 0.12));
  const rugs = plan.filter((rect) => !isSolid(rect) && fits(rect) && free(rect, blocked));
  const placed: ScaleProp[] = [];
  plan
    .filter((rect) => isSolid(rect) && fits(rect))
    .forEach((rect) => {
      if (free(rect, [...blocked, ...placed])) placed.push(rect);
    });
  return [...rugs, ...placed];
}
