import { Euler, Matrix4, Quaternion, Vector3, type BufferGeometry } from 'three';

import type { CreatureAppearance } from './creature-appearance';
import type { Gear } from './creature-gear';
import { creatureCapsuleGeometry, creatureFootGeometry, handGeometry } from './creature-resources';
import {
  JOINTS,
  SIDES,
  boneOf,
  mirrored,
  type BoneName,
  type Point,
  type Side,
} from './creature-rig';

export interface BodyPart {
  bone: BoneName;
  geometry: BufferGeometry;
  matrix: Matrix4;
  color: string;
}

interface Placement {
  position: Point;
  rotation?: Point;
  scale?: number | Point;
}

const UP = new Vector3(0, 1, 0);
const TORSO_FROM: Point = [0, 0.99, 0];
const TORSO_TO: Point = [0, 1.23, 0];
const TORSO_RADIUS = 0.21;
const FOOT_CENTER: Point = [0.15, 0.075, -0.08];

export function solidPart(
  bone: BoneName,
  geometry: BufferGeometry,
  color: string,
  { position, rotation = [0, 0, 0], scale = 1 }: Placement,
): BodyPart {
  const matrix = new Matrix4().compose(
    new Vector3(...position),
    new Quaternion().setFromEuler(new Euler(...rotation)),
    new Vector3(...(typeof scale === 'number' ? [scale, scale, scale] : scale)),
  );
  return { bone, geometry, matrix, color };
}

function segmentPart(
  bone: BoneName,
  from: Point,
  to: Point,
  radius: number,
  color: string,
): BodyPart {
  const start = new Vector3(...from);
  const direction = new Vector3(...to).sub(start);
  const geometry = creatureCapsuleGeometry(radius, direction.length());
  const matrix = new Matrix4().compose(
    start.clone().addScaledVector(direction, 0.5),
    new Quaternion().setFromUnitVectors(UP, direction.clone().normalize()),
    new Vector3(1, 1, 1),
  );
  return { bone, geometry, matrix, color };
}

function torsoParts(garment: string, chest: string | undefined): BodyPart[] {
  const torso = segmentPart('spine', TORSO_FROM, TORSO_TO, TORSO_RADIUS, garment);
  return chest
    ? [torso, segmentPart('spine', TORSO_FROM, TORSO_TO, TORSO_RADIUS + 0.03, chest)]
    : [torso];
}

function armParts(side: Side, skin: string, gloves: string | undefined) {
  const [shoulder, elbow, wrist] = [
    JOINTS[boneOf('upperArm', side)],
    JOINTS[boneOf('forearm', side)],
    JOINTS[boneOf('hand', side)],
  ];
  return [
    segmentPart(boneOf('upperArm', side), shoulder, elbow, 0.075, skin),
    segmentPart(boneOf('forearm', side), elbow, wrist, 0.07, skin),
    solidPart(boneOf('hand', side), handGeometry, gloves ?? skin, { position: wrist }),
  ];
}

function legParts(side: Side, sign: 1 | -1, garment: string, boots: string | undefined) {
  const thigh = mirrored([0.15, 0.81, 0], sign);
  const knee = mirrored([0.15, 0.49, 0], sign);
  const shinTop = mirrored([0.15, 0.46, 0], sign);
  const ankle = JOINTS[boneOf('foot', side)];
  const foot = mirrored(FOOT_CENTER, sign);
  const parts = [
    segmentPart(boneOf('thigh', side), thigh, knee, 0.11, garment),
    segmentPart(boneOf('shin', side), shinTop, ankle, 0.09, garment),
    solidPart(boneOf('foot', side), creatureFootGeometry, garment, { position: foot }),
  ];
  return boots
    ? [
        ...parts,
        segmentPart(boneOf('shin', side), shinTop, ankle, 0.11, boots),
        solidPart(boneOf('foot', side), creatureFootGeometry, boots, {
          position: foot,
          scale: 1.08,
        }),
      ]
    : parts;
}

export function bodyParts({ skin, garment }: CreatureAppearance, { armor }: Gear): BodyPart[] {
  return [
    ...torsoParts(garment, armor.Chest),
    ...SIDES.flatMap(({ side, sign }) => [
      ...armParts(side, skin, armor.Gloves),
      ...legParts(side, sign, garment, armor.Boots),
    ]),
  ];
}
