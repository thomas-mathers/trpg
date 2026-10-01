import { useMemo } from 'react';
import { Quaternion, Vector3 } from 'three';

type Point = [number, number, number];
type Limb = { from: Point; to: Point; radius: number };
export type BodyPose = { torso: Limb; head: Point; arms: Limb[]; legs: Limb[]; foot: Point };

function BodySegment({ from, to, radius, color }: Limb & { color: string }) {
  const { position, rotation, length } = useMemo(() => {
    const start = new Vector3(...from);
    const end = new Vector3(...to);
    const direction = end.clone().sub(start);
    return {
      position: start.add(end).multiplyScalar(0.5),
      rotation: new Quaternion().setFromUnitVectors(
        new Vector3(0, 1, 0),
        direction.clone().normalize(),
      ),
      length: direction.length(),
    };
  }, [from, to]);
  return (
    <mesh position={position} quaternion={rotation}>
      <capsuleGeometry args={[radius, length, 6, 12]} />
      <meshStandardMaterial color={color} roughness={0.85} />
    </mesh>
  );
}

function BodySide({ pose, color }: { pose: BodyPose; color: string }) {
  return (
    <group>
      {[...pose.arms, ...pose.legs].map((limb, index) => (
        <BodySegment key={index} {...limb} color={color} />
      ))}
      <mesh position={pose.foot}>
        <boxGeometry args={[0.19, 0.14, 0.3]} />
        <meshStandardMaterial color={color} roughness={0.85} />
      </mesh>
    </group>
  );
}

export function CreatureBody({
  pose,
  color,
  perspective = 'third-person',
}: {
  pose: BodyPose;
  color: string;
  perspective?: 'first-person' | 'third-person';
}) {
  return (
    <group>
      <BodySegment {...pose.torso} color={color} />
      {perspective === 'third-person' && (
        <mesh position={pose.head}>
          <sphereGeometry args={[0.18, 16, 12]} />
          <meshStandardMaterial color={color} roughness={0.85} />
        </mesh>
      )}
      <BodySide pose={pose} color={color} />
      <group scale={[-1, 1, 1]}>
        <BodySide pose={pose} color={color} />
      </group>
    </group>
  );
}
