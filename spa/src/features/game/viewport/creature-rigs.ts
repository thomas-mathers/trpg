import type { CreatureAppearance } from './creature-appearance';
import { creatureBodyGeometry } from './creature-body-geometry';
import type { Gear } from './creature-gear';
import { createCreatureRig } from './creature-rig';

export function bystanderRig(appearance: CreatureAppearance, gear: Gear) {
  return createCreatureRig(creatureBodyGeometry(appearance, gear));
}

export function viewerRig(appearance: CreatureAppearance, gear: Gear) {
  return createCreatureRig(
    creatureBodyGeometry(appearance, gear, 'body'),
    creatureBodyGeometry(appearance, gear, 'head'),
  );
}
