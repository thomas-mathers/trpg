import { Grid, useGLTF } from '@react-three/drei';
import { Suspense, useMemo } from 'react';

import type {
  BuildingLayoutWire,
  ConnectorLayoutWire,
  CreatureLayoutWire,
  FootprintWire,
  PlacementWire,
  PropLayoutWire,
} from '@/api/signalr-client/TRPG.GameSessions.Responses';

import { EntityLabel } from './entity-label';
import { headingToYaw, type Obstacle, toScenePosition, WALL_HEIGHT } from './layout-math';
import {
  BUILDING_MODEL_URLS,
  BUILDING_STYLES,
  type BoxStyle,
  PROP_MODEL_URLS,
  PROP_STYLES,
} from './model-styles';

const CREATURE_HEIGHT = 1.7;
const CREATURE_RADIUS = 0.35;
const CONNECTOR_HEIGHT = 3;

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
}: {
  label?: string;
  placement: PlacementWire;
  footprint: FootprintWire;
  style: BoxStyle;
  modelUrl?: string;
}) {
  const { height } = style;
  const fallback = <BoxMesh footprint={footprint} style={style} />;

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
}: {
  creatures: CreatureLayoutWire[];
  playerId: string;
  names: EntityNames;
}) {
  return (
    <>
      {creatures
        .filter((creature) => creature.id !== playerId)
        .map(({ id, placement }) => (
          <group
            key={id}
            position={toScenePosition(placement.x, placement.y, CREATURE_HEIGHT / 2)}
            rotation={[0, headingToYaw(placement.angle), 0]}
          >
            <mesh>
              <capsuleGeometry args={[CREATURE_RADIUS, CREATURE_HEIGHT - CREATURE_RADIUS * 2]} />
              <meshStandardMaterial color="#b9503f" />
            </mesh>
            <group position={[0, CREATURE_HEIGHT / 2 + 0.4, 0]}>
              <EntityLabel text={names.get(id)} />
            </group>
          </group>
        ))}
    </>
  );
}

export function Connectors({ connectors }: { connectors: ConnectorLayoutWire[] }) {
  return (
    <>
      {connectors.map(({ connectorId, exitX, exitY }) => (
        <group key={connectorId} position={toScenePosition(exitX, exitY, CONNECTOR_HEIGHT / 2)}>
          <mesh>
            <cylinderGeometry args={[0.5, 0.5, CONNECTOR_HEIGHT, 16, 1, true]} />
            <meshStandardMaterial
              color="#e7c15a"
              emissive="#e7c15a"
              emissiveIntensity={0.6}
              transparent
              opacity={0.45}
            />
          </mesh>
        </group>
      ))}
    </>
  );
}
