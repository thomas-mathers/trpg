import type { CreatureAppearance } from './creature-appearance';
import { CreatureBody, type BodyPose } from './creature-body';
import type { Gear } from './creature-gear';

const pose: BodyPose = {
  torso: { from: [0, 0.72, 0], to: [0, 0.96, 0], radius: 0.21 },
  head: [0, 1.36, 0],
  arms: [
    { from: [0.28, 1.01, 0], to: [0.32, 0.72, -0.04], radius: 0.075 },
    { from: [0.32, 0.72, -0.04], to: [0.23, 0.8, -0.36], radius: 0.07 },
  ],
  legs: [
    { from: [0.15, 0.61, -0.04], to: [0.15, 0.61, -0.43], radius: 0.11 },
    { from: [0.15, 0.56, -0.43], to: [0.15, 0.16, -0.43], radius: 0.09 },
  ],
  foot: [0.15, 0.075, -0.51],
};

const SEAT_TOP = 0.5;

export function SeatedBody({
  appearance,
  gear,
  perspective,
  upperBodyYaw,
}: {
  upperBodyYaw?: number;
  appearance: CreatureAppearance;
  gear: Gear;
  perspective?: 'first-person' | 'third-person';
}) {
  const seatLift = Math.max(0, SEAT_TOP * (1 - appearance.height));
  return (
    <group position={[0, seatLift, 0]}>
      <CreatureBody
        pose={pose}
        appearance={appearance}
        gear={gear}
        perspective={perspective}
        upperBodyYaw={upperBodyYaw}
      />
    </group>
  );
}
