import { CreatureBody, type BodyPose } from './creature-body';

const pose: BodyPose = {
  torso: { from: [0, 0.99, 0], to: [0, 1.23, 0], radius: 0.21 },
  head: [0, 1.63, 0],
  arms: [
    { from: [0.28, 1.28, 0], to: [0.32, 0.99, 0], radius: 0.075 },
    { from: [0.32, 0.99, 0], to: [0.32, 0.7, -0.04], radius: 0.07 },
  ],
  legs: [
    { from: [0.15, 0.81, 0], to: [0.15, 0.49, 0], radius: 0.11 },
    { from: [0.15, 0.46, 0], to: [0.15, 0.16, 0], radius: 0.09 },
  ],
  foot: [0.15, 0.075, -0.08],
};

export function StandingBody({ color }: { color: string }) {
  return <CreatureBody pose={pose} color={color} />;
}
