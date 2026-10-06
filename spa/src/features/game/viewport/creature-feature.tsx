import type { Point } from './body-segment';
import type { CreatureAppearance } from './creature-appearance';
import { Part } from './creature-part';
import { earGeometry, hornGeometry, unitBoxGeometry } from './creature-resources';

const ELDER_BEARD = '#cfcfcf';
const BEARD = '#4a3a2c';
const HORN = '#d8d0b8';

export function CreatureFeature({
  head,
  appearance: { feature, skin, elderly },
}: {
  head: Point;
  appearance: CreatureAppearance;
}) {
  const [x, y, z] = head;
  if (feature === 'beard') {
    return (
      <Part
        color={elderly ? ELDER_BEARD : BEARD}
        geometry={unitBoxGeometry}
        position={[x, y - 0.15, z - 0.1]}
        scale={[0.2, 0.2, 0.1]}
      />
    );
  }
  if (feature === 'pointed-ears') {
    return (
      <>
        <Part
          color={skin}
          geometry={earGeometry}
          position={[x + 0.2, y + 0.03, z]}
          rotation={[0, 0, -Math.PI / 2.4]}
        />
        <Part
          color={skin}
          geometry={earGeometry}
          position={[x - 0.2, y + 0.03, z]}
          rotation={[0, 0, Math.PI / 2.4]}
        />
      </>
    );
  }
  if (feature === 'horns') {
    return (
      <>
        <Part
          color={HORN}
          geometry={hornGeometry}
          position={[x + 0.11, y + 0.19, z]}
          rotation={[0, 0, -0.35]}
        />
        <Part
          color={HORN}
          geometry={hornGeometry}
          position={[x - 0.11, y + 0.19, z]}
          rotation={[0, 0, 0.35]}
        />
      </>
    );
  }
  return null;
}
