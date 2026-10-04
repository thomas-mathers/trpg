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
    <mesh castShadow receiveShadow position={position} quaternion={rotation}>
      <capsuleGeometry args={[radius, length, 6, 12]} />
      <meshStandardMaterial color={color} roughness={0.85} />
    </mesh>
  );
}

function BodyLegs({ pose, color }: { pose: BodyPose; color: string }) {
  return (
    <group>
      {pose.legs.map((limb, index) => (
        <BodySegment key={index} {...limb} color={color} />
      ))}
      <mesh castShadow receiveShadow position={pose.foot}>
        <boxGeometry args={[0.19, 0.14, 0.3]} />
        <meshStandardMaterial color={color} roughness={0.85} />
      </mesh>
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
  color,
  perspective = 'third-person',
  upperBodyYaw = 0,
}: {
  upperBodyYaw?: number;
  pose: BodyPose;
  color: string;
  perspective?: 'first-person' | 'third-person';
}) {
  return (
    <group>
      <group rotation={[0, upperBodyYaw, 0]}>
        <BodySegment {...pose.torso} color={color} />
        {perspective === 'third-person' && (
          <mesh castShadow receiveShadow position={pose.head}>
            <sphereGeometry args={[0.18, 16, 12]} />
            <meshStandardMaterial color={color} roughness={0.85} />
          </mesh>
        )}
        <BodyArms pose={pose} color={color} />
        <group scale={[-1, 1, 1]}>
          <BodyArms pose={pose} color={color} />
        </group>
      </group>
      <BodyLegs pose={pose} color={color} />
      <group scale={[-1, 1, 1]}>
        <BodyLegs pose={pose} color={color} />
      </group>
    </group>
  );
}
