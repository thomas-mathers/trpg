import { useMemo } from 'react';
import { ExtrudeGeometry, Shape } from 'three';

import type { BuildingType, FootprintWire } from '@/api/signalr-client/TRPG.GameSessions.Responses';

import { buildingWallColor, roofShape } from './building-roof';
import type { BoxStyle } from './model-styles';

function gableGeometry(span: number, length: number, rise: number) {
  const outline = new Shape();
  outline.moveTo(-span / 2, 0);
  outline.lineTo(span / 2, 0);
  outline.lineTo(0, rise);
  outline.closePath();
  const geometry = new ExtrudeGeometry(outline, { depth: length, bevelEnabled: false });
  geometry.translate(0, 0, -length / 2);
  return geometry;
}

function BuildingRoof({
  id,
  type,
  footprint,
  wallHeight,
  shadows,
}: {
  id: string;
  type: BuildingType;
  footprint: FootprintWire;
  wallHeight: number;
  shadows: boolean;
}) {
  const roof = roofShape(id, type, footprint);
  const geometry = useMemo(
    () => (roof ? gableGeometry(roof.span, roof.length, roof.rise) : undefined),
    [roof?.span, roof?.length, roof?.rise],
  );
  if (!roof || !geometry) return null;
  return (
    <mesh
      geometry={geometry}
      position={[0, wallHeight / 2, 0]}
      rotation={[0, roof.yaw, 0]}
      castShadow={shadows}
      receiveShadow={shadows}
    >
      <meshStandardMaterial color={roof.color} />
    </mesh>
  );
}

export function BuildingMesh({
  id,
  type,
  footprint,
  style: { color, height },
  shadows = true,
}: {
  id: string;
  type: BuildingType;
  footprint: FootprintWire;
  style: BoxStyle;
  shadows?: boolean;
}) {
  return (
    <>
      <mesh castShadow={shadows} receiveShadow={shadows}>
        <boxGeometry args={[footprint.width, height, footprint.depth]} />
        <meshStandardMaterial color={buildingWallColor(color, id)} />
      </mesh>
      <BuildingRoof
        id={id}
        type={type}
        footprint={footprint}
        wallHeight={height}
        shadows={shadows}
      />
    </>
  );
}
