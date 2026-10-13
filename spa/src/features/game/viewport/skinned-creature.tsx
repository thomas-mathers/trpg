import type { Gear } from './creature-gear';
import { CreatureHeldGear } from './creature-held-gear';
import type { CreatureRig } from './creature-rig';

export function SkinnedCreature({
  rig,
  gear,
  height,
  lift,
}: {
  rig: CreatureRig;
  gear: Gear;
  height: number;
  lift: number;
}) {
  return (
    <group position={[0, lift, 0]}>
      <group scale={height}>
        <primitive object={rig.mesh} />
        <CreatureHeldGear rig={rig} gear={gear} />
      </group>
    </group>
  );
}
