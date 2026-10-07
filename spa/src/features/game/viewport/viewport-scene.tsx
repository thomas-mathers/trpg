import { useGLTF } from '@react-three/drei';
import { Suspense, useMemo } from 'react';
import { BackSide, Mesh } from 'three';

import type {
  BuildingType,
  CreatureStatusSnapshot,
  FootprintWire,
  LocationBoundarySnapshot,
  NearbyBuildingSnapshot,
  NearbyExitSnapshot,
  NearbyPropSnapshot,
  PlacementWire,
  PropModel,
} from '@/api/signalr-client/TRPG.GameSessions.Responses';
import type { GameClockAnchor } from '@/features/game/game-clock';

import { OUTDOOR_FLOOR_COLOR } from './boundary-scene';
import { BuildingMesh } from './building-mesh';
import { varyColor } from './color-variation';
import { connectorYaw } from './connector-placement';
import { buildingNameBoards, hasDoor } from './connector-visibility';
import { CreatureFigure } from './creature-figure';
import type { CreatureFocus } from './creature-focus';
import { DoorConnector } from './door-connector';
import { floorGeometry, terrainGeometry } from './floor-geometry';
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
import { NameBoard, SignMesh } from './sign-mesh';
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
    <mesh rotation={[-Math.PI / 2, 0, 0]} geometry={floor} receiveShadow renderOrder={-10}>
      <meshLambertMaterial color={color} />
    </mesh>
  );
}

const TERRAIN_EXTENT = 600;
const TERRAIN_OVERLAP = 0.3;
const TERRAIN_DROP = 0.002;

export function Terrain({ size }: { size: FootprintWire }) {
  const terrain = useMemo(() => terrainGeometry(size, TERRAIN_EXTENT, TERRAIN_OVERLAP), [size]);

  return (
    <mesh
      rotation={[-Math.PI / 2, 0, 0]}
      position={[0, -TERRAIN_DROP, 0]}
      geometry={terrain}
      renderOrder={-11}
    >
      <meshLambertMaterial color={OUTDOOR_FLOOR_COLOR} />
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

function propFallback(
  id: string,
  footprint: FootprintWire,
  style: BoxStyle,
  model?: PropModel,
  text?: string,
) {
  if (model === 'Sign' && text) {
    return <SignMesh footprint={footprint} style={style} text={text} />;
  }
  if (model?.startsWith('Seat')) {
    return <SeatMesh footprint={footprint} style={style} model={model} />;
  }
  if (model && isFurnitureModel(model)) {
    return <FurnitureMesh id={id} footprint={footprint} style={style} model={model} />;
  }
  return <BoxMesh footprint={footprint} style={style} />;
}

function Box({
  id,
  buildingType,
  placement,
  footprint,
  style,
  modelUrl,
  propModel,
  text,
}: {
  id: string;
  buildingType?: BuildingType;
  placement: PlacementWire;
  footprint: FootprintWire;
  style: BoxStyle;
  modelUrl?: string;
  propModel?: PropModel;
  text?: string;
}) {
  const { height } = style;
  const fallback = buildingType ? (
    <BuildingMesh id={id} type={buildingType} footprint={footprint} style={style} />
  ) : (
    propFallback(id, footprint, style, propModel, text)
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
    </group>
  );
}

function propStyle(model: PropModel, id: string): BoxStyle {
  const style = PROP_STYLES[model];
  return { ...style, color: varyColor(style.color, id, 0.5) };
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
          id={id}
          placement={placement}
          footprint={footprint}
          style={propStyle(model, id)}
          propModel={model}
          text={description}
          modelUrl={PROP_MODEL_URLS[model]}
        />
      ))}
      {buildings.map(({ id, type, placement, footprint, floorCount }) => (
        <Box
          key={id}
          id={id}
          buildingType={type}
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
  clock,
}: {
  clock: GameClockAnchor;
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
      {creatures.map(({ id, placement, walk, creatureType, age, equipment }) => {
        const status = statuses.find((creature) => creature.id === id);
        const posture = status?.posture;
        if (id === playerId && posture !== 'Sitting') return null;
        return (
          <CreatureFigure
            key={id}
            id={id}
            placement={id === playerId && playerSeat ? playerSeat.placement : placement}
            walk={walk}
            clock={clock}
            posture={posture}
            playerId={playerId}
            label={names.get(id)}
            creatureType={creatureType}
            age={age}
            equipment={equipment}
            facing={focus?.id === id && status?.condition !== 'Dead' ? focus.facing : undefined}
          />
        );
      })}
    </>
  );
}

export function Connectors({
  connectors,
  boundary,
  size,
}: {
  connectors: NearbyExitSnapshot[];
  boundary?: LocationBoundarySnapshot;
  size: FootprintWire;
}) {
  return (
    <>
      {connectors.map((connector) => (
        <group
          key={connector.connectorId}
          position={toScenePosition(connector.placement.x, connector.placement.y)}
          rotation={[0, connectorYaw(connector), 0]}
        >
          {connector.stairs ? (
            <StairConnector direction={connector.stairs} />
          ) : (
            hasDoor(connector, boundary, size) && <DoorConnector />
          )}
        </group>
      ))}
    </>
  );
}

const NAME_BOARD_HEIGHT = 0.3;
const NAME_BOARD_CENTER = DOOR_HEIGHT + 0.03 + NAME_BOARD_HEIGHT / 2;

export function BuildingNameBoards({ connectors }: { connectors: NearbyExitSnapshot[] }) {
  const boards = useMemo(() => buildingNameBoards(connectors), [connectors]);

  return (
    <>
      {boards.map(({ exit, text, width }) => (
        <group
          key={exit.connectorId}
          position={toScenePosition(exit.placement.x, exit.placement.y, NAME_BOARD_CENTER)}
          rotation={[0, connectorYaw(exit), 0]}
        >
          <NameBoard text={text} width={width} height={NAME_BOARD_HEIGHT} />
        </group>
      ))}
    </>
  );
}
