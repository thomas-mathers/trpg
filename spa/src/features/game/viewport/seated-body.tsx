import { CreatureBody, type BodyPose } from './creature-body';

const pose: BodyPose = {
  torso: { from: [0, 0.72, 0], to: [0, 0.96, 0], radius: 0.21 },
  head: [0, 1.36, 0],
  arms: [
    { from: [0.28, 1.01, 0], to: [0.32, 0.72, -0.04], radius: 0.075 },
    { from: [0.32, 0.72, -0.04], to: [0.23, 0.65, -0.36], radius: 0.07 },
  ],
  legs: [
    { from: [0.15, 0.53, -0.04], to: [0.15, 0.53, -0.43], radius: 0.11 },
    { from: [0.15, 0.48, -0.43], to: [0.15, 0.16, -0.43], radius: 0.09 },
  ],
  foot: [0.15, 0.075, -0.51],
};

export function SeatedBody({
  color,
  perspective,
  upperBodyYaw,
}: {
  upperBodyYaw?: number;
  color: string;
  perspective?: 'first-person' | 'third-person';
}) {
  return (
    <CreatureBody pose={pose} color={color} perspective={perspective} upperBodyYaw={upperBodyYaw} />
  );
}
