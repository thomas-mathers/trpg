import { BodySegment, type Point } from './body-segment';
import type { BodyPose } from './creature-body';
import type { Gear, Hand, WeaponStyle } from './creature-gear';
import { Part } from './creature-part';
import {
  creatureFootGeometry,
  handGeometry,
  helmGeometry,
  shieldGeometry,
  unitBoxGeometry,
} from './creature-resources';

const HANDLE = '#5a4330';
const SHIELD = '#7a5a3a';

function handPosition(pose: BodyPose, hand: Hand): Point {
  const [x, y, z] = pose.arms[pose.arms.length - 1].to;
  return hand === 'right' ? [x, y, z] : [-x, y, z];
}

function Weapon({ style: { grip, head, headColor }, at }: { style: WeaponStyle; at: Point }) {
  const [width, height, depth] = head;
  return (
    <group position={at}>
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

export function UpperGear({
  pose,
  gear,
  showHead,
}: {
  pose: BodyPose;
  gear: Gear;
  showHead: boolean;
}) {
  const { armor, weapons, shields } = gear;
  const [headX, headY, headZ] = pose.head;
  return (
    <>
      {armor.Chest && (
        <BodySegment {...pose.torso} radius={pose.torso.radius + 0.03} color={armor.Chest} />
      )}
      {armor.Helm && showHead && (
        <Part color={armor.Helm} geometry={helmGeometry} position={[headX, headY + 0.01, headZ]} />
      )}
      {armor.Gloves &&
        (['right', 'left'] as const).map((hand) => (
          <Part
            key={hand}
            color={armor.Gloves!}
            geometry={handGeometry}
            position={handPosition(pose, hand)}
          />
        ))}
      {weapons.map(({ hand, style }) => (
        <Weapon key={`weapon-${hand}`} style={style} at={handPosition(pose, hand)} />
      ))}
      {shields.map((hand) => {
        const [x, y, z] = handPosition(pose, hand);
        return (
          <Part
            key={`shield-${hand}`}
            color={SHIELD}
            geometry={shieldGeometry}
            position={[x, y + 0.1, z - 0.1]}
            rotation={[Math.PI / 2, 0, 0]}
          />
        );
      })}
    </>
  );
}

export function LowerGear({ pose, color }: { pose: BodyPose; color: string }) {
  const shin = pose.legs[pose.legs.length - 1];
  return (
    <>
      <BodySegment {...shin} radius={shin.radius + 0.02} color={color} />
      <Part color={color} geometry={creatureFootGeometry} position={pose.foot} scale={1.08} />
    </>
  );
}
