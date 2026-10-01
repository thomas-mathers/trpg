import { Grid, useGLTF } from '@react-three/drei';
import { Suspense, useMemo } from 'react';

import type {
  BuildingLayoutWire,
  ConnectorLayoutWire,
  CreatureLayoutWire,
  CreatureStatusSnapshot,
  FootprintWire,
  PlacementWire,
  PropLayoutWire,
  PropModel,
} from '@/api/signalr-client/TRPG.GameSessions.Responses';

import { connectorYaw } from './connector-placement';
import { DoorConnector } from './door-connector';
import { EntityLabel } from './entity-label';
import { headingToYaw, type Obstacle, toScenePosition, WALL_HEIGHT } from './layout-math';
import {
  BUILDING_MODEL_URLS,
  BUILDING_STYLES,
  type BoxStyle,
  PROP_MODEL_URLS,
  PROP_STYLES,
} from './model-styles';
import type { ViewportSeat } from './seat-interaction';
import { SeatMesh } from './seat-mesh';
import { SeatedBody } from './seated-body';
import { StandingBody } from './standing-body';

const CREATURE_HEIGHT = 1.7;

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
  const fallback = propModel?.startsWith('Seat') ? (
    <SeatMesh footprint={footprint} style={style} model={propModel} />
  ) : (
    <BoxMesh footprint={footprint} style={style} />
  );

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
  props: PropLayoutWire[];
  buildings: BuildingLayoutWire[];
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
      {buildings.map(({ id, type, placement, footprint }) => (
        <Box
          key={id}
          placement={placement}
          footprint={footprint}
          style={BUILDING_STYLES[type]}
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
}: {
  creatures: CreatureLayoutWire[];
  playerId: string;
  names: EntityNames;
  statuses: Pick<CreatureStatusSnapshot, 'id' | 'posture'>[];
  playerSeat?: ViewportSeat;
}) {
  return (
    <>
      {creatures.map(({ id, placement }) => {
        const seated = statuses.find((creature) => creature.id === id)?.posture === 'Sitting';
        if (id === playerId && !seated) return null;
        const pose = id === playerId && playerSeat ? playerSeat.placement : placement;
        return (
          <group
            key={id}
            position={toScenePosition(pose.x, pose.y)}
            rotation={[0, headingToYaw(pose.angle), 0]}
          >
            {seated ? (
              <SeatedBody
                color={id === playerId ? '#4f8091' : '#b9503f'}
                perspective={id === playerId ? 'first-person' : 'third-person'}
              />
            ) : (
              <StandingBody color="#b9503f" />
            )}
            {id !== playerId && (
              <group position={[0, seated ? 1.85 : CREATURE_HEIGHT + 0.4, 0]}>
                <EntityLabel text={names.get(id)} />
              </group>
            )}
          </group>
        );
      })}
    </>
  );
}

export function Connectors({
  connectors,
  size,
  buildings,
}: {
  connectors: ConnectorLayoutWire[];
  size: FootprintWire;
  buildings: BuildingLayoutWire[];
}) {
  return (
    <>
      {connectors.map((connector) => (
        <group
          key={connector.connectorId}
          position={toScenePosition(connector.exitX, connector.exitY)}
          rotation={[0, connectorYaw(connector, size, buildings), 0]}
        >
          <DoorConnector />
        </group>
      ))}
    </>
  );
}
