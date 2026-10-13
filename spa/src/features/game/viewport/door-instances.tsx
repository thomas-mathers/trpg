import { useLayoutEffect, useMemo, useRef } from 'react';
import { Object3D, type InstancedMesh } from 'three';

import type {
  FootprintWire,
  LocationBoundarySnapshot,
  NearbyExitSnapshot,
  PlacementWire,
} from '@/api/signalr-client/TRPG.GameSessions.Responses';

import { hasDoor } from './connector-visibility';
import {
  doorFittingsGeometry,
  doorFittingsMaterial,
  doorTimberGeometry,
  doorTimberMaterial,
} from './door-connector';
import { headingToYaw } from './layout-math';

export function DoorBatch({ placements }: { placements: PlacementWire[] }) {
  const timber = useRef<InstancedMesh>(null);
  const fittings = useRef<InstancedMesh>(null);
  useLayoutEffect(() => {
    const instance = new Object3D();
    placements.forEach(({ x, y, angle }, index) => {
      instance.position.set(x, 0, y);
      instance.rotation.set(0, headingToYaw(angle) + Math.PI, 0);
      instance.updateMatrix();
      for (const mesh of [timber.current, fittings.current])
        mesh?.setMatrixAt(index, instance.matrix);
    });
    for (const mesh of [timber.current, fittings.current]) {
      if (mesh) mesh.instanceMatrix.needsUpdate = true;
    }
  }, [placements]);

  if (placements.length === 0) return null;
  return (
    <>
      <instancedMesh
        ref={timber}
        args={[doorTimberGeometry, doorTimberMaterial, placements.length]}
        receiveShadow
        frustumCulled={false}
        dispose={null}
      />
      <instancedMesh
        ref={fittings}
        args={[doorFittingsGeometry, doorFittingsMaterial, placements.length]}
        receiveShadow
        frustumCulled={false}
        dispose={null}
      />
    </>
  );
}

export function DoorInstances({
  connectors,
  boundary,
  size,
}: {
  connectors: NearbyExitSnapshot[];
  boundary?: LocationBoundarySnapshot;
  size: FootprintWire;
}) {
  const placements = useMemo(
    () =>
      connectors
        .filter((connector) => !connector.stairs && hasDoor(connector, boundary, size))
        .map(({ placement }) => placement),
    [connectors, boundary, size],
  );
  return <DoorBatch placements={placements} />;
}
