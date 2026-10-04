import { Grid, useGLTF } from '@react-three/drei';
import { Suspense, useMemo } from 'react';

import type {
  CreatureStatusSnapshot,
  FootprintWire,
  NearbyBuildingSnapshot,
  NearbyExitSnapshot,
  NearbyPropSnapshot,
  PlacementWire,
  PropModel,
} from '@/api/signalr-client/TRPG.GameSessions.Responses';

import { connectorYaw } from './connector-placement';
import { CreatureFigure } from './creature-figure';
import type { CreatureFocus } from './creature-focus';
import { DoorConnector } from './door-connector';
import { EntityLabel } from './entity-label';
import { FurnitureMesh } from './furniture-mesh';
import { isFurnitureModel } from './furniture-parts';
import { headingToYaw, type Obstacle, toScenePosition, WALL_HEIGHT } from './layout-math';
import {
  BUILDING_MODEL_URLS,
  buildingStyle,
  type BoxStyle,
  PROP_MODEL_URLS,
  PROP_STYLES,
} from './model-styles';
import type { ViewportSeat } from './seat-interaction';
import { SeatMesh } from './seat-mesh';

type EntityNames = ReadonlyMap<string, string>;

export function Ground({ size }: { size: FootprintWire }) {
  const { width, depth } = size;

  return (
    <>
      <mesh rotation={[-Math.PI / 2, 0, 0]} position={[width / 2, 0, depth / 2]}>
        <planeGeometry args={[width, depth]} />
        <meshStandardMaterial color="#3b4a32" />
      </mesh>
      <Grid
        position={[width / 2, 0.01, depth / 2]}
        args={[width, depth]}
        cellSize={1}
        sectionSize={5}
        cellColor="#4d5e42"
        sectionColor="#6f8460"
        fadeDistance={60}
        infiniteGrid={false}
      />
    </>
  );
}

export function Walls({ walls }: { walls: Obstacle[] }) {
  return (
    <>
      {walls.map(({ placement, footprint }) => (
        <mesh
          key={`${placement.x}:${placement.y}`}
          position={toScenePosition(placement.x, placement.y, WALL_HEIGHT / 2)}
        >
          <boxGeometry args={[footprint.width, WALL_HEIGHT, footprint.depth]} />
          <meshStandardMaterial color="#8a7b66" />
        </mesh>
      ))}
    </>
  );
}

function GltfModel({
  url,
  footprint,
  height,
}: {
  url: string;
  footprint: FootprintWire;
  height: number;
}) {
  const { scene } = useGLTF(url);
  const model = useMemo(() => scene.clone(), [scene]);

  return (
    <group scale={[footprint.width, height, footprint.depth]} position={[0, -height / 2, 0]}>
      <primitive object={model} />
    </group>
  );
}

function BoxMesh({ footprint, style }: { footprint: FootprintWire; style: BoxStyle }) {
  return (
    <mesh>
      <boxGeometry args={[footprint.width, style.height, footprint.depth]} />
      <meshStandardMaterial color={style.color} />
    </mesh>
  );
}

function propFallback(footprint: FootprintWire, style: BoxStyle, model?: PropModel) {
  if (model?.startsWith('Seat')) {
    return <SeatMesh footprint={footprint} style={style} model={model} />;
  }
  if (model && isFurnitureModel(model)) {
    return <FurnitureMesh footprint={footprint} style={style} model={model} />;
  }
  return <BoxMesh footprint={footprint} style={style} />;
}

function Box({
  label,
  placement,
  footprint,
  style,
  modelUrl,
  propModel,
}: {
  label?: string;
  placement: PlacementWire;
  footprint: FootprintWire;
  style: BoxStyle;
  modelUrl?: string;
  propModel?: PropModel;
}) {
  const { height } = style;
  const fallback = propFallback(footprint, style, propModel);

  return (
    <group
      position={toScenePosition(placement.x, placement.y, height / 2)}
      rotation={[0, headingToYaw(placement.angle), 0]}
    >
      {modelUrl ? (
        <Suspense fallback={fallback}>
          <GltfModel url={modelUrl} footprint={footprint} height={height} />
        </Suspense>
      ) : (
        fallback
      )}
      <group position={[0, height / 2 + 0.4, 0]}>
        <EntityLabel text={label} />
      </group>
    </group>
  );
}

export function Boxes({
  props,
  buildings,
  names,
}: {
  props: NearbyPropSnapshot[];
  buildings: NearbyBuildingSnapshot[];
  names: EntityNames;
}) {
  return (
    <>
      {props.map(({ id, model, placement, footprint }) => (
        <Box
          key={id}
          label={names.get(id)}
          placement={placement}
          footprint={footprint}
          style={PROP_STYLES[model]}
          propModel={model}
          modelUrl={PROP_MODEL_URLS[model]}
        />
      ))}
      {buildings.map(({ id, type, placement, footprint, floorCount }) => (
        <Box
          key={id}
          placement={placement}
          footprint={footprint}
          style={buildingStyle(type, floorCount)}
          modelUrl={BUILDING_MODEL_URLS[type]}
        />
      ))}
    </>
  );
}

export function Creatures({
  creatures,
  playerId,
  names,
  statuses,
  playerSeat,
  focus,
}: {
  creatures: CreatureStatusSnapshot[];
  playerId: string;
  names: EntityNames;
  statuses: (Pick<CreatureStatusSnapshot, 'id' | 'posture'> &
    Partial<Pick<CreatureStatusSnapshot, 'condition'>>)[];
  playerSeat?: ViewportSeat;
  focus?: CreatureFocus | null;
}) {
  return (
    <>
      {creatures.map(({ id, placement }) => {
        const status = statuses.find((creature) => creature.id === id);
        const posture = status?.posture;
        if (id === playerId && posture !== 'Sitting') return null;
        return (
          <CreatureFigure
            key={id}
            id={id}
            placement={id === playerId && playerSeat ? playerSeat.placement : placement}
            posture={posture}
            playerId={playerId}
            label={names.get(id)}
            facing={focus?.id === id && status?.condition !== 'Dead' ? focus.facing : undefined}
          />
        );
      })}
    </>
  );
}

export function Connectors({ connectors }: { connectors: NearbyExitSnapshot[] }) {
  return (
    <>
      {connectors.map((connector) => (
        <group
          key={connector.connectorId}
          position={toScenePosition(connector.placement.x, connector.placement.y)}
          rotation={[0, connectorYaw(connector), 0]}
        >
          <DoorConnector />
        </group>
      ))}
    </>
  );
}
