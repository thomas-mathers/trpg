import type { BoneName } from './creature-rig';

export interface LimbPitches {
  rootY: number;
  pitch: Partial<Record<BoneName, number>>;
  roll?: Partial<Record<BoneName, number>>;
}

const THIGH_SWING = 0.5;
const KNEE_FLEX = 0.8;
const ARM_SWING = 0.45;
const ELBOW_BEND = 0.25;
const ELBOW_SWING_BEND = 0.4;
const BOB = 0.02;

const STANCE_FLATTEN = 0.95;
const FLATTEN_NORM = Math.asin(STANCE_FLATTEN);

// A pure sine sweeps the planted foot fastest mid-step, so it skates against the ground.
function thighAngle(phase: number) {
  return (THIGH_SWING * Math.asin(STANCE_FLATTEN * Math.sin(phase))) / FLATTEN_NORM;
}

function legPitches(phase: number, amount: number) {
  const thigh = thighAngle(phase) * amount;
  const knee = -KNEE_FLEX * Math.max(0, Math.cos(phase)) * amount;
  return { thigh, knee, foot: -(thigh + knee) * 0.6 };
}

function armPitches(phase: number, amount: number) {
  const swing = -Math.sin(phase);
  return {
    upperArm: ARM_SWING * swing * amount,
    forearm: (ELBOW_BEND + ELBOW_SWING_BEND * Math.max(0, swing)) * amount,
  };
}

export function walkCycle(phase: number, amount: number): LimbPitches {
  const right = legPitches(phase, amount);
  const left = legPitches(phase + Math.PI, amount);
  const rightArm = armPitches(phase, amount);
  const leftArm = armPitches(phase + Math.PI, amount);
  return {
    rootY: -BOB * amount * (1 + Math.cos(2 * phase)),
    pitch: {
      thighR: right.thigh,
      shinR: right.knee,
      footR: right.foot,
      thighL: left.thigh,
      shinL: left.knee,
      footL: left.foot,
      upperArmR: rightArm.upperArm,
      forearmR: rightArm.forearm,
      upperArmL: leftArm.upperArm,
      forearmL: leftArm.forearm,
    },
  };
}
