import { BodySegment, type Limb, type Point } from './body-segment';
import type { CreatureAppearance } from './creature-appearance';
import { CreatureFeature } from './creature-feature';
import type { Gear } from './creature-gear';
import { LowerGear, UpperGear } from './creature-gear-parts';
import { Part } from './creature-part';
import { creatureFootGeometry, creatureHeadGeometry } from './creature-resources';

export type BodyPose = { torso: Limb; head: Point; arms: Limb[]; legs: Limb[]; foot: Point };

function BodyLegs({ pose, color, boots }: { pose: BodyPose; color: string; boots?: string }) {
  return (
    <group>
      {pose.legs.map((limb, index) => (
        <BodySegment key={index} {...limb} color={color} />
      ))}
      <Part color={color} position={pose.foot} geometry={creatureFootGeometry} />
      {boots && <LowerGear pose={pose} color={boots} />}
    </group>
  );
}

function BodyArms({ pose, color }: { pose: BodyPose; color: string }) {
  return (
    <>
      {pose.arms.map((limb, index) => (
        <BodySegment key={index} {...limb} color={color} />
      ))}
    </>
  );
}

export function CreatureBody({
  pose,
  appearance,
  gear,
  perspective = 'third-person',
  upperBodyYaw = 0,
}: {
  upperBodyYaw?: number;
  pose: BodyPose;
  appearance: CreatureAppearance;
  gear: Gear;
  perspective?: 'first-person' | 'third-person';
}) {
  const { skin, garment, height } = appearance;
  const thirdPerson = perspective === 'third-person';
  return (
    <group scale={height}>
      <group rotation={[0, upperBodyYaw, 0]}>
        <BodySegment {...pose.torso} color={garment} />
        {thirdPerson && (
          <>
            <Part color={skin} position={pose.head} geometry={creatureHeadGeometry} />
            <CreatureFeature head={pose.head} appearance={appearance} />
          </>
        )}
        <BodyArms pose={pose} color={skin} />
        <group scale={[-1, 1, 1]}>
          <BodyArms pose={pose} color={skin} />
        </group>
        <UpperGear pose={pose} gear={gear} showHead={thirdPerson} />
      </group>
      <BodyLegs pose={pose} color={garment} boots={gear.armor.Boots} />
      <group scale={[-1, 1, 1]}>
        <BodyLegs pose={pose} color={garment} boots={gear.armor.Boots} />
      </group>
    </group>
  );
}
