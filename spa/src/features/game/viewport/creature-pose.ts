import { JOINTS, SIDES, boneOf, type BoneName, type CreatureRig, type Side } from './creature-rig';
import { walkCycle, type LimbPitches } from './walk-cycle';

export type Stance = 'walking' | 'seated' | 'looking';

export interface PoseInputs {
  stance: Stance;
  gaitPhase: number;
  gaitAmount: number;
  upperYaw: number;
  idleTime?: number;
  shielded?: boolean;
}

const SEAT_DROP = 0.27;
const SEATED_FOREARM_PITCH = 1.8;
const LOOKING_UPPER_ARM_PITCH = 1.05;
const LOOKING_FOREARM_PITCH = 1.0;
const LOOKING_ARM_SPLAY = 0.3;
const LOOKING_FOREARM_ROLL = -0.35;
const BREATH_RATE = 1.6;
const BREATH_UPPER_ARM = 0.025;
const BREATH_FOREARM = 0.04;
const LOOKING_OFFHAND_UPPER_ARM_PITCH = 1.35;
const LOOKING_OFFHAND_FOREARM_PITCH = -0.2;
const LOOKING_OFFHAND_ARM_SPLAY = -0.7;
const LOOKING_OFFHAND_FOREARM_ROLL = 1.45;
const LOOKING_ARM_SWAY = 0.14;
const LOOKING_FOREARM_BOB = 0.12;
const HELD_WEAPON_TILT = -0.7;

const THIGH_LENGTH = JOINTS.thighR[1] - JOINTS.shinR[1];
const ANKLE_HEIGHT = JOINTS.footR[1];
const KNEE_HEIGHT = ANKLE_HEIGHT + (JOINTS.shinR[1] - JOINTS.footR[1]);
const SEATED_THIGH_PITCH = Math.acos((JOINTS.thighR[1] - SEAT_DROP - KNEE_HEIGHT) / THIGH_LENGTH);

function withUprightHands(pose: LimbPitches): LimbPitches {
  const pitch = { ...pose.pitch };
  for (const { side } of SIDES) {
    const arm = (pitch[boneOf('upperArm', side)] ?? 0) + (pitch[boneOf('forearm', side)] ?? 0);
    pitch[boneOf('hand', side)] = HELD_WEAPON_TILT - arm;
  }
  return { ...pose, pitch };
}

export const SEATED_POSE: LimbPitches = withUprightHands({
  rootY: -SEAT_DROP,
  pitch: Object.fromEntries(
    SIDES.flatMap(({ side }) => [
      [boneOf('thigh', side), SEATED_THIGH_PITCH],
      [boneOf('shin', side), -SEATED_THIGH_PITCH],
      [boneOf('forearm', side), SEATED_FOREARM_PITCH],
    ]),
  ),
});

type ArmPose = Pick<Required<LimbPitches>, 'pitch' | 'roll'>;

interface ArmDrive {
  sway: number;
  bob: number;
  upperBreath: number;
  forearmBreath: number;
}

function weaponArm(
  side: Side,
  sign: 1 | -1,
  { sway, bob, upperBreath, forearmBreath }: ArmDrive,
): ArmPose {
  return {
    pitch: {
      [boneOf('upperArm', side)]: LOOKING_UPPER_ARM_PITCH - sign * sway + upperBreath,
      [boneOf('forearm', side)]: LOOKING_FOREARM_PITCH + sign * bob + forearmBreath,
    },
    roll: {
      [boneOf('upperArm', side)]: sign * LOOKING_ARM_SPLAY,
      [boneOf('forearm', side)]: sign * LOOKING_FOREARM_ROLL,
    },
  };
}

function shieldArm({ sway, bob, upperBreath, forearmBreath }: ArmDrive): ArmPose {
  return {
    pitch: {
      upperArmL: LOOKING_OFFHAND_UPPER_ARM_PITCH + sway - upperBreath,
      forearmL: LOOKING_OFFHAND_FOREARM_PITCH - bob - forearmBreath,
    },
    roll: { upperArmL: LOOKING_OFFHAND_ARM_SPLAY, forearmL: LOOKING_OFFHAND_FOREARM_ROLL },
  };
}

function lookingPose(phase: number, amount: number, idleTime: number, shielded: boolean) {
  const walk = walkCycle(phase, amount);
  const breath = idleTime * BREATH_RATE;
  const drive: ArmDrive = {
    sway: LOOKING_ARM_SWAY * Math.sin(phase) * amount,
    bob: LOOKING_FOREARM_BOB * Math.sin(2 * phase) * amount,
    upperBreath: BREATH_UPPER_ARM * Math.sin(breath),
    forearmBreath: BREATH_FOREARM * Math.sin(breath + 1),
  };
  const right = weaponArm('R', 1, drive);
  const left = shielded ? shieldArm(drive) : weaponArm('L', -1, drive);
  return withUprightHands({
    ...walk,
    pitch: { ...walk.pitch, ...right.pitch, ...left.pitch },
    roll: { ...right.roll, ...left.roll },
  });
}

export function limbPitches({
  stance,
  gaitPhase,
  gaitAmount,
  idleTime = 0,
  shielded = false,
}: PoseInputs): LimbPitches {
  if (stance === 'seated') return SEATED_POSE;
  return stance === 'looking'
    ? lookingPose(gaitPhase, gaitAmount, idleTime, shielded)
    : walkCycle(gaitPhase, gaitAmount);
}

export function applyPose(rig: CreatureRig, inputs: PoseInputs) {
  const { rootY, pitch, roll = {} } = limbPitches(inputs);
  rig.bones.root.position.y = rootY;
  rig.bones.spine.rotation.y = inputs.upperYaw;
  for (const [name, bone] of Object.entries(rig.bones) as [
    BoneName,
    CreatureRig['bones'][BoneName],
  ][]) {
    if (name === 'root') continue;
    bone.rotation.x = pitch[name] ?? 0;
    bone.rotation.z = roll[name] ?? 0;
  }
}
