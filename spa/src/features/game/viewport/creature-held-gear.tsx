import { createPortal } from '@react-three/fiber';
import { Fragment } from 'react';

import type { Gear, Hand, WeaponStyle } from './creature-gear';
import { Part } from './creature-part';
import { shieldGeometry, unitBoxGeometry } from './creature-resources';
import { boneOf, JOINTS, type BoneName, type CreatureRig, type Point } from './creature-rig';

const HANDLE = '#5a4330';
const SHIELD = '#7a5a3a';
const SHIELD_ROLL = Math.PI / 2 - 0.4;
const SHIELD_STANDOFF = -0.1;
const HAND_IN_FOREARM: Point = [
  0,
  JOINTS.handR[1] - JOINTS.forearmR[1],
  JOINTS.handR[2] - JOINTS.forearmR[2],
];

function handBone(hand: Hand): BoneName {
  return boneOf('hand', hand === 'right' ? 'R' : 'L');
}

function Weapon({ grip, head: [width, height, depth], headColor }: WeaponStyle) {
  return (
    <group rotation={[0, Math.PI / 2, 0]}>
      <Part
        color={HANDLE}
        geometry={unitBoxGeometry}
        position={[0, grip / 2 - 0.1, 0]}
        scale={[0.04, grip, 0.04]}
      />
      <Part
        color={headColor}
        geometry={unitBoxGeometry}
        position={[0, grip - 0.1 + height / 2, 0]}
        scale={[width, height, depth]}
      />
    </group>
  );
}

function Shield() {
  return (
    <group position={HAND_IN_FOREARM}>
      <group rotation={[0, SHIELD_ROLL, 0]}>
        <Part
          color={SHIELD}
          geometry={shieldGeometry}
          position={[0, 0, SHIELD_STANDOFF]}
          rotation={[Math.PI / 2, 0, 0]}
        />
      </group>
    </group>
  );
}

export function CreatureHeldGear({ rig, gear }: { rig: CreatureRig; gear: Gear }) {
  return (
    <>
      {gear.weapons.map(({ hand, style }) => (
        <Fragment key={`weapon-${hand}-${rig.mesh.uuid}`}>
          {createPortal(<Weapon {...style} />, rig.bones[handBone(hand)])}
        </Fragment>
      ))}
      {gear.shields.map((hand) => (
        <Fragment key={`shield-${hand}-${rig.mesh.uuid}`}>
          {createPortal(<Shield />, rig.bones[boneOf('forearm', hand === 'right' ? 'R' : 'L')])}
        </Fragment>
      ))}
    </>
  );
}
