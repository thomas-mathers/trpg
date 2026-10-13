import { BoxGeometry, CylinderGeometry, LatheGeometry, SphereGeometry, Vector2 } from 'three';

import { BASIN_FLOOR_RATIO, type FurniturePart } from './furniture-parts';

function basinGeometry([outerRadius, innerRadius, height]: FurniturePart['size']) {
  const half = height / 2;
  const floor = -half + height * BASIN_FLOOR_RATIO;
  return new LatheGeometry(
    [
      new Vector2(0, -half),
      new Vector2(outerRadius, -half),
      new Vector2(outerRadius, half),
      new Vector2(innerRadius, half),
      new Vector2(innerRadius, floor),
      new Vector2(0, floor),
    ],
    16,
  );
}

export function furnitureGeometry({ shape, size }: Pick<FurniturePart, 'shape' | 'size'>) {
  const [first, second, third] = size;
  switch (shape) {
    case 'box':
      return new BoxGeometry(first, second, third);
    case 'cylinder':
      return new CylinderGeometry(first, second, third, 16);
    case 'sphere':
      return new SphereGeometry(first, 12, 8);
    case 'basin':
      return basinGeometry(size);
  }
}
