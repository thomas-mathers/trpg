import { useMemo } from 'react';
import { Quaternion, Vector3 } from 'three';

import { Part } from './creature-part';
import { creatureCapsuleGeometry } from './creature-resources';

export type Point = [number, number, number];
export type Limb = { from: Point; to: Point; radius: number };

export function BodySegment({ from, to, radius, color }: Limb & { color: string }) {
  const { position, rotation, length } = useMemo(() => {
    const start = new Vector3(...from);
    const end = new Vector3(...to);
    const direction = end.clone().sub(start);
    return {
      position: start.add(end).multiplyScalar(0.5),
      rotation: new Quaternion().setFromUnitVectors(
        new Vector3(0, 1, 0),
        direction.clone().normalize(),
      ),
      length: direction.length(),
    };
  }, [from, to]);
  return (
    <Part
      color={color}
      position={position}
      quaternion={rotation}
      geometry={creatureCapsuleGeometry(radius, length)}
    />
  );
}
