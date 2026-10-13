import { Bone, Skeleton, SkinnedMesh, type BufferGeometry } from 'three';

import { creatureShadowOnlyMaterial, creatureVertexMaterial } from './creature-resources';

export type Point = [number, number, number];

type Limb = 'upperArm' | 'forearm' | 'hand' | 'thigh' | 'shin' | 'foot';
export type Side = 'R' | 'L';
export type BoneName = 'root' | 'hips' | 'spine' | 'head' | `${Limb}${Side}`;

export const SIDES: readonly { side: Side; sign: 1 | -1 }[] = [
  { side: 'R', sign: 1 },
  { side: 'L', sign: -1 },
];

const LIMB_JOINTS: readonly { limb: Limb; parent: Limb | 'hips' | 'spine'; joint: Point }[] = [
  { limb: 'upperArm', parent: 'spine', joint: [0.28, 1.28, 0] },
  { limb: 'forearm', parent: 'upperArm', joint: [0.32, 0.99, 0] },
  { limb: 'hand', parent: 'forearm', joint: [0.32, 0.7, -0.04] },
  { limb: 'thigh', parent: 'hips', joint: [0.15, 0.81, 0] },
  { limb: 'shin', parent: 'thigh', joint: [0.15, 0.475, 0] },
  { limb: 'foot', parent: 'shin', joint: [0.15, 0.16, 0] },
];

interface BoneLayout {
  name: BoneName;
  parent?: BoneName;
  joint: Point;
}

const MIDLINE: BoneLayout[] = [
  { name: 'root', joint: [0, 0, 0] },
  { name: 'hips', parent: 'root', joint: [0, 0.81, 0] },
  { name: 'spine', parent: 'hips', joint: [0, 0.99, 0] },
  { name: 'head', parent: 'spine', joint: [0, 1.63, 0] },
];

export function mirrored([x, y, z]: Point, sign: 1 | -1): Point {
  return [x * sign, y, z];
}

export function boneOf(limb: Limb, side: Side): BoneName {
  return `${limb}${side}`;
}

const LAYOUT: BoneLayout[] = [
  ...MIDLINE,
  ...SIDES.flatMap(({ side, sign }) =>
    LIMB_JOINTS.map(({ limb, parent, joint }) => ({
      name: boneOf(limb, side),
      parent: parent === 'hips' || parent === 'spine' ? parent : boneOf(parent, side),
      joint: mirrored(joint, sign),
    })),
  ),
];

export const BONE_NAMES: readonly BoneName[] = LAYOUT.map(({ name }) => name);

export const JOINTS = Object.fromEntries(LAYOUT.map(({ name, joint }) => [name, joint])) as Record<
  BoneName,
  Point
>;

export interface CreatureRig {
  mesh: SkinnedMesh;
  skeleton: Skeleton;
  bones: Record<BoneName, Bone>;
}

function createBones() {
  const bones = {} as Record<BoneName, Bone>;
  for (const { name, parent, joint } of LAYOUT) {
    const bone = new Bone();
    bone.name = name;
    if (parent) {
      const [px, py, pz] = JOINTS[parent];
      bone.position.set(joint[0] - px, joint[1] - py, joint[2] - pz);
      bones[parent].add(bone);
    }
    bones[name] = bone;
  }
  return bones;
}

function shadowOnlyMesh(geometry: BufferGeometry, skeleton: Skeleton) {
  const mesh = new SkinnedMesh(geometry, creatureShadowOnlyMaterial);
  mesh.castShadow = true;
  mesh.bind(skeleton);
  return mesh;
}

export function createCreatureRig(
  geometry: BufferGeometry,
  shadowOnlyGeometry?: BufferGeometry,
): CreatureRig {
  const bones = createBones();
  const skeleton = new Skeleton(BONE_NAMES.map((name) => bones[name]));
  const mesh = new SkinnedMesh(geometry, creatureVertexMaterial);
  mesh.castShadow = true;
  mesh.receiveShadow = true;
  mesh.add(bones.root);
  mesh.bind(skeleton);
  if (shadowOnlyGeometry) mesh.add(shadowOnlyMesh(shadowOnlyGeometry, skeleton));
  return { mesh, skeleton, bones };
}
