import type { NearbyPropSnapshot } from '@/api/signalr-client/TRPG.GameSessions.Responses';

import { varyColor } from './color-variation';
import { furnitureParts, isFurnitureModel, type FurniturePart } from './furniture-parts';
import { PROP_MODEL_URLS, PROP_STYLES } from './model-styles';
import { seatParts } from './seat-parts';
import { withWoodFinish } from './wood-parts';

export function canBatchProp({ model, description }: NearbyPropSnapshot) {
  return !PROP_MODEL_URLS[model] && !(model === 'Sign' && description);
}

export function propParts({ id, model, footprint }: NearbyPropSnapshot): FurniturePart[] {
  const style = PROP_STYLES[model];
  const size = { ...footprint, ...style, color: varyColor(style.color, id, 0.5), id };
  const finished = (parts: FurniturePart[]) => withWoodFinish(parts, model, size.color);
  if (model.startsWith('Seat')) return finished(seatParts(model, size));
  if (isFurnitureModel(model)) return finished(furnitureParts(model, size));
  return [
    {
      shape: 'box',
      position: [0, style.height / 2, 0],
      size: [footprint.width, style.height, footprint.depth],
      color: size.color,
    },
  ];
}
