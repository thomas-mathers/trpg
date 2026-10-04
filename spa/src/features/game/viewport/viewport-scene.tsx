import { useGLTF } from '@react-three/drei';
import { Suspense, useMemo } from 'react';
import { BackSide, Mesh } from 'three';

import type {
  CreatureStatusSnapshot,
  FootprintWire,
  NearbyBuildingSnapshot,
  NearbyExitSnapshot,
  NearbyPropSnapshot,
  PlacementWire,
  PropModel,
} from '@/api/signalr-client/TRPG.GameSessions.Responses';

import { OUTDOOR_FLOOR_COLOR } from './boundary-scene';
import { connectorYaw } from './connector-placement';
import { CreatureFigure } from './creature-figure';
import type { CreatureFocus } from './creature-focus';
import { DoorConnector } from './door-connector';
import { floorGeometry } from './floor-geometry';
import { FurnitureMesh } from './furniture-mesh';
import { isFurnitureModel } from './furniture-parts';
import {
  DOOR_HEIGHT,
  headingToYaw,
  type Obstacle,
  toScenePosition,
  WALL_HEIGHT,
} from './layout-math';
import {
  BUILDING_MODEL_URLS,
  buildingStyle,
  type BoxStyle,
  PROP_MODEL_URLS,
  PROP_STYLES,
} from './model-styles';
import type { ViewportSeat } from './seat-interaction';
import { SeatMesh } from './seat-mesh';
import { SignMesh } from './sign-mesh';
import { StairConnector } from './stair-connector';

type EntityNames = ReadonlyMap<string, string>;

export const ROOM_FLOOR_COLOR = '#6e5338';

export function Ground({
  size,
  wells = [],
  color = OUTDOOR_FLOOR_COLOR,
}: {
  size: FootprintWire;
  wells?: NearbyExitSnapshot[];
  color?: string;
}) {
  const floor = useMemo(() => floorGeometry(size, wells), [size, wells]);

  return (
    <mesh rotation={[-Math.PI / 2, 0, 0]} geometry={floor} receiveShadow>
      <meshLambertMaterial color={color} />
    </mesh>
  );
}

export function Ceiling({
  size,
  openings,
}: {
  size: FootprintWire;
  openings: NearbyExitSnapshot[];
}) {
  const ceiling = useMemo(() => floorGeometry(size, openings), [size, openings]);

  return (
    <mesh rotation={[-Math.PI / 2, 0, 0]} position={[0, WALL_HEIGHT, 0]} geometry={ceiling}>
      <meshLambertMaterial color="#6b6254" side={BackSide} />
    </mesh>
  );
}

const HEADER_HEIGHT = WALL_HEIGHT - DOOR_HEIGHT;

const TOWER_HEIGHT = WALL_HEIGHT * 1.7;

export function Walls({
  walls,
  headers = [],
  towers = [],
}: {
  walls: Obstacle[];
  headers?: Obstacle[];
  towers?: Obstacle[];
}) {
  return (
    <>
      {walls.map(({ placement, footprint }) => (
        <mesh
          castShadow
          receiveShadow
          key={`${placement.x}:${placement.y}`}
          position={toScenePosition(placement.x, placement.y, WALL_HEIGHT / 2)}
          rotation={[0, headingToYaw(placement.angle), 0]}
        >
          <boxGeometry args={[footprint.width, WALL_HEIGHT, footprint.depth]} />
          <meshLambertMaterial color="#8a7b66" />
        </mesh>
      ))}
      {towers.map(({ placement, footprint }) => (
        <mesh
          castShadow
          receiveShadow
          key={`tower:${placement.x}:${placement.y}`}
          position={toScenePosition(placement.x, placement.y, TOWER_HEIGHT / 2)}
          rotation={[0, headingToYaw(placement.angle), 0]}
        >
          <boxGeometry args={[footprint.width, TOWER_HEIGHT, footprint.depth]} />
          <meshLambertMaterial color="#756853" />
        </mesh>
      ))}
      {headers.map(({ placement, footprint }) => (
        <mesh
          castShadow
          receiveShadow
          key={`header:${placement.x}:${placement.y}`}
          position={toScenePosition(placement.x, placement.y, DOOR_HEIGHT + HEADER_HEIGHT / 2)}
        >
          <boxGeometry args={[footprint.width, HEADER_HEIGHT, footprint.depth]} />
          <meshLambertMaterial color="#8a7b66" />
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
  const model = useMemo(() => {
    const clone = scene.clone();
    clone.traverse((object) => {
      if (object instanceof Mesh) {
        object.castShadow = true;
        object.receiveShadow = true;
      }
    });
    return clone;
  }, [scene]);

  return (
    <group scale={[footprint.width, height, footprint.depth]} position={[0, -height / 2, 0]}>
      <primitive object={model} />
    </group>
  );
}

function BoxMesh({ footprint, style }: { footprint: FootprintWire; style: BoxStyle }) {
  return (
    <mesh castShadow receiveShadow>
      <boxGeometry args={[footprint.width, style.height, footprint.depth]} />
      <meshStandardMaterial color={style.color} />
    </mesh>
  );
}

function propFallback(footprint: FootprintWire, style: BoxStyle, model?: PropModel, text?: string) {
  if (model === 'Sign' && text) {
    return <SignMesh footprint={footprint} style={style} text={text} />;
  }
  if (model?.startsWith('Seat')) {
    return <SeatMesh footprint={footprint} style={style} model={model} />;
  }
  if (model && isFurnitureModel(model)) {
    return <FurnitureMesh footprint={footprint} style={style} model={model} />;
  }
  return <BoxMesh footprint={footprint} style={style} />;
}

function Box({
  placement,
  footprint,
  style,
  modelUrl,
  propModel,
  text,
}: {
  placement: PlacementWire;
  footprint: FootprintWire;
  style: BoxStyle;
  modelUrl?: string;
  propModel?: PropModel;
  text?: string;
}) {
  const { height } = style;
  const fallback = propFallback(footprint, style, propModel, text);

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
    </group>
  );
}

export function Boxes({
  props,
  buildings,
}: {
  props: NearbyPropSnapshot[];
  buildings: NearbyBuildingSnapshot[];
}) {
  return (
    <>
      {props.map(({ id, model, placement, footprint, description }) => (
        <Box
          key={id}
          placement={placement}
          footprint={footprint}
          style={PROP_STYLES[model]}
          propModel={model}
          text={description}
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
          {connector.stairs ? <StairConnector direction={connector.stairs} /> : <DoorConnector />}
        </group>
      ))}
    </>
  );
}
