import { useEffect, useMemo, useRef } from 'react';
import { DoubleSide, FrontSide, type MeshStandardMaterial } from 'three';

import type { NearbyPropSnapshot } from '@/api/signalr-client/TRPG.GameSessions.Responses';

import { FurniturePartMesh } from './furniture-mesh';
import { useLampGlow } from './lamp-glow';
import { headingToYaw } from './layout-math';
import { buildPropBatches, type PropBatch } from './prop-batch-geometry';
import { woodMaterial } from './wood-material';

function LampBatchMaterial({ emissive }: { emissive: string }) {
  const material = useRef<MeshStandardMaterial>(null);
  useLampGlow(material);
  return <meshStandardMaterial ref={material} vertexColors roughness={1} emissive={emissive} />;
}

function BatchMaterial({ batch }: { batch: PropBatch }) {
  if (batch.finish === 'lamp') return <LampBatchMaterial emissive={batch.emissive} />;
  if (batch.finish === 'wood') return <primitive object={woodMaterial()} attach="material" />;
  return (
    <meshStandardMaterial
      vertexColors
      side={batch.doubleSided ? DoubleSide : FrontSide}
      metalness={batch.finish === 'metal' ? 0.4 : 0}
      roughness={batch.finish === 'metal' ? 0.55 : 1}
      emissive={batch.emissive}
      emissiveIntensity={batch.finish === 'glow' ? 1.2 : 0}
    />
  );
}

function BatchMesh({ batch, shadows }: { batch: PropBatch; shadows: boolean }) {
  return (
    <mesh geometry={batch.geometry} castShadow={shadows} receiveShadow matrixAutoUpdate={false}>
      <BatchMaterial batch={batch} />
    </mesh>
  );
}

export function PropBatches({ props, shadows }: { props: NearbyPropSnapshot[]; shadows: boolean }) {
  const { batches, moving } = useMemo(() => buildPropBatches(props), [props]);
  useEffect(() => () => batches.forEach(({ geometry }) => geometry.dispose()), [batches]);
  return (
    <>
      {batches.map((batch) => (
        <BatchMesh key={batch.key} batch={batch} shadows={shadows} />
      ))}
      {moving.map(({ key, placement, part }) => (
        <group
          key={key}
          position={[placement.x, 0, placement.y]}
          rotation={[0, headingToYaw(placement.angle), 0]}
        >
          <FurniturePartMesh {...part} shadows={shadows} />
        </group>
      ))}
    </>
  );
}
