import { useFrame } from '@react-three/fiber';
import { useMemo, useRef } from 'react';
import { Color, MathUtils, type InstancedMesh } from 'three';

import { useSceneHour } from './scene-hour';
import { skyStateAt } from './sky-state';
import { wrapAround, type WeatherLook } from './weather-look';

const FIELD_RADIUS = 18;
const FIELD_HEIGHT = 14;
const FIELD_BELOW_CAMERA = 4;
const MATRIX_SIZE = 16;
const MAX_FRAME_SECONDS = 0.1;
const SNOW_DRIFT = 0.6;
const RAIN_COLOR = '#aebccd';
const SNOW_COLOR = '#f2f6fa';
const NIGHT_BRIGHTNESS = 0.15;

function seedParticles(count: number): Float32Array {
  const seeds = new Float32Array(count * 3);
  for (let index = 0; index < seeds.length; index++) {
    seeds[index] = Math.random();
  }
  return seeds;
}

function ParticleField({ look, color }: { look: WeatherLook; color: Color }) {
  const { particles, particleCount, fallSpeed } = look;
  const mesh = useRef<InstancedMesh>(null);
  const elapsed = useRef(0);
  const seeds = useMemo(() => seedParticles(particleCount), [particleCount]);

  useFrame(({ camera }, delta) => {
    if (!mesh.current) return;
    elapsed.current += Math.min(delta, MAX_FRAME_SECONDS);
    const fallen = fallSpeed * elapsed.current;
    const drift = particles === 'snow' ? SNOW_DRIFT : 0;
    const matrices = mesh.current.instanceMatrix.array;
    const { x: camX, y: camY, z: camZ } = camera.position;

    for (let index = 0; index < particleCount; index++) {
      const sway = Math.sin(elapsed.current * 0.7 + seeds[index * 3] * 40) * drift;
      const base = index * MATRIX_SIZE;
      matrices[base + 12] = wrapAround(
        seeds[index * 3] * 2 * FIELD_RADIUS + sway,
        camX - FIELD_RADIUS,
        2 * FIELD_RADIUS,
      );
      matrices[base + 13] = wrapAround(
        seeds[index * 3 + 1] * FIELD_HEIGHT - fallen,
        camY - FIELD_BELOW_CAMERA,
        FIELD_HEIGHT,
      );
      matrices[base + 14] = wrapAround(
        seeds[index * 3 + 2] * 2 * FIELD_RADIUS + sway,
        camZ - FIELD_RADIUS,
        2 * FIELD_RADIUS,
      );
    }
    mesh.current.instanceMatrix.needsUpdate = true;
  });

  return (
    <instancedMesh
      ref={mesh}
      args={[undefined, undefined, particleCount]}
      frustumCulled={false}
      renderOrder={1}
    >
      {particles === 'rain' ? (
        <cylinderGeometry args={[0.012, 0.012, 0.7, 3]} />
      ) : (
        <octahedronGeometry args={[0.035, 0]} />
      )}
      <meshBasicMaterial
        color={color}
        transparent
        opacity={particles === 'rain' ? 0.45 : 0.85}
        depthWrite={false}
      />
    </instancedMesh>
  );
}

export function WeatherParticles({ look }: { look: WeatherLook }) {
  const hour = useSceneHour();
  const { daylight } = useMemo(() => skyStateAt(hour), [hour]);
  const color = useMemo(
    () =>
      new Color(look.particles === 'snow' ? SNOW_COLOR : RAIN_COLOR).multiplyScalar(
        MathUtils.lerp(NIGHT_BRIGHTNESS, 1, daylight),
      ),
    [look.particles, daylight],
  );

  if (look.particles === 'none') return null;
  return (
    <ParticleField key={`${look.particles}-${look.particleCount}`} look={look} color={color} />
  );
}
