import { useEffect, useMemo } from 'react';

import type { BuildingType, FootprintWire } from '@/api/signalr-client/TRPG.GameSessions.Responses';

import { buildingFacade, facadePalette } from './building-facade';
import { buildingWallColor, roofShape } from './building-roof';
import { roofGeometry } from './building-roof-geometry';
import { createRoofMaterial } from './building-roof-material';
import { createWallMaterial } from './building-wall-material';
import { stringHash } from './color-variation';
import type { BoxStyle } from './model-styles';

const STOREY_HEIGHT = 3;
const SEED_RANGE = 1000;

function BuildingRoof({
  id,
  type,
  footprint,
  wallHeight,
  plaster,
  timber,
  shadows,
}: {
  id: string;
  type: BuildingType;
  footprint: FootprintWire;
  wallHeight: number;
  plaster: string;
  timber: string;
  shadows: boolean;
}) {
  const roof = roofShape(id, type, footprint);
  const geometry = useMemo(
    () => (roof ? roofGeometry(roof) : undefined),
    [roof?.span, roof?.length, roof?.rise, roof?.chimney?.x, roof?.chimney?.z],
  );
  const material = useMemo(
    () =>
      roof
        ? createRoofMaterial({
            span: roof.span,
            length: roof.length,
            rise: roof.rise,
            tile: roof.color,
            plaster,
            timber,
          })
        : undefined,
    [roof?.span, roof?.length, roof?.rise, roof?.color, plaster, timber],
  );
  useEffect(() => () => geometry?.dispose(), [geometry]);
  useEffect(() => () => material?.dispose(), [material]);
  if (!roof || !geometry || !material) return null;
  return (
    <mesh
      geometry={geometry}
      material={material}
      position={[0, wallHeight / 2, 0]}
      rotation={[0, roof.yaw, 0]}
      castShadow={shadows}
      receiveShadow={shadows}
    />
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
  const { width, depth } = footprint;
  const { plinth } = buildingFacade(type);
  const palette = useMemo(
    () => facadePalette(type, buildingWallColor(color, id), id),
    [type, color, id],
  );
  const wallMaterial = useMemo(
    () =>
      createWallMaterial({
        width,
        height,
        depth,
        storeys: Math.max(1, Math.round(height / STOREY_HEIGHT)),
        plinth: Math.min(plinth, height + 1),
        seed: stringHash(id) % SEED_RANGE,
        ...palette,
      }),
    [width, height, depth, plinth, id, palette],
  );
  useEffect(() => () => wallMaterial.dispose(), [wallMaterial]);

  return (
    <>
      <mesh material={wallMaterial} castShadow={shadows} receiveShadow={shadows}>
        <boxGeometry args={[width, height, depth]} />
      </mesh>
      <BuildingRoof
        id={id}
        type={type}
        footprint={footprint}
        wallHeight={height}
        plaster={palette.plaster}
        timber={palette.timber}
        shadows={shadows}
      />
    </>
  );
}
