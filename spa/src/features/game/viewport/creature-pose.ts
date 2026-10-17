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
  strike?: number;
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
const STRIKE_UPPER_ARM = 1.9;
const STRIKE_FOREARM = 0.6;
const STRIKE_WIND_UP_END = 0.3;
const STRIKE_FOLLOW_THROUGH_END = 0.5;
const STRIKE_OVERSHOOT = 0.4;

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

export function strikeSwing(progress: number): number {
  if (progress <= 0 || progress >= 1) return 0;
  if (progress < STRIKE_WIND_UP_END) {
    return Math.sin((progress / STRIKE_WIND_UP_END) * (Math.PI / 2));
  }
  if (progress < STRIKE_FOLLOW_THROUGH_END) {
    const t = (progress - STRIKE_WIND_UP_END) / (STRIKE_FOLLOW_THROUGH_END - STRIKE_WIND_UP_END);
    return 1 - (1 + STRIKE_OVERSHOOT) * t;
  }
  const t = (progress - STRIKE_FOLLOW_THROUGH_END) / (1 - STRIKE_FOLLOW_THROUGH_END);
  return -STRIKE_OVERSHOOT * (1 - t);
}

function withStrike(pose: LimbPitches, strike: number): LimbPitches {
  const swing = strikeSwing(strike);
  if (swing === 0) return pose;
  const pitch = { ...pose.pitch };
  pitch[boneOf('upperArm', 'R')] = (pitch[boneOf('upperArm', 'R')] ?? 0) + swing * STRIKE_UPPER_ARM;
  pitch[boneOf('forearm', 'R')] = (pitch[boneOf('forearm', 'R')] ?? 0) + swing * STRIKE_FOREARM;
  return withUprightHands({ ...pose, pitch });
}

export function limbPitches({
  stance,
  gaitPhase,
  gaitAmount,
  idleTime = 0,
  shielded = false,
  strike = 0,
}: PoseInputs): LimbPitches {
  if (stance === 'seated') return SEATED_POSE;
  const base =
    stance === 'looking'
      ? lookingPose(gaitPhase, gaitAmount, idleTime, shielded)
      : walkCycle(gaitPhase, gaitAmount);
  return withStrike(base, strike);
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
