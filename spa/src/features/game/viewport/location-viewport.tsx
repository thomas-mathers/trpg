import { Canvas } from '@react-three/fiber';
import { memo, useEffect, useMemo, useRef, useState } from 'react';
import { PCFShadowMap, type Scene, type WebGPURenderer } from 'three/webgpu';

import type {
  NearbyExitSnapshot,
  PlacementWire,
} from '@/api/signalr-client/TRPG.GameSessions.Responses';
import { useHasActiveEncounter } from '@/features/encounters/hooks/use-has-active-encounter';

import {
  CreatureInteractionPanel,
  type CreatureInteractionPanelProps,
} from '../components/creature-interaction-panel';
import { useScene } from '../contexts/scene-context';
import { useChatHub } from '../hooks/use-game-hub-connection';
import { useIsInCombat } from '../hooks/use-is-in-combat';
import { runAction } from '../run-action';
import { RoadAprons, Roads } from './boundary-scene';
import type { CreatureFocus } from './creature-focus';
import { CreatureFocusController } from './creature-focus-controller';
import { DebugFloorGrid } from './debug-floor-grid';
import { FpsController } from './fps-controller';
import { IndoorLighting } from './indoor-lighting';
import {
  boundaryTowers,
  boundaryWalls,
  buildDoorHeaders,
  buildEntityNames,
  buildObstacles,
  buildWalls,
  isRoomScene,
  findPlayerPlacement,
} from './layout-math';
import { NeighborDistricts } from './neighbor-scene';
import { OutdoorFog } from './outdoor-fog';
import { OutdoorLighting } from './outdoor-lighting';
import { OutdoorSky } from './outdoor-sky';
import { RenderDiagnostics } from './render-diagnostics';
import { buildSeats, type ViewportSeat } from './seat-interaction';
import { useSeatInteraction } from './use-seat-interaction';
import {
  Boxes,
  Ceiling,
  BuildingNameBoards,
  Connectors,
  Creatures,
  Ground,
  ROOM_FLOOR_COLOR,
  Terrain,
  Walls,
} from './viewport-scene';
import { weatherLookFor } from './weather-look';
import { WeatherParticles } from './weather-particles';
import { createWebGpuRenderer } from './webgpu-renderer';

const CANVAS_ID = 'location-viewport-canvas';
const DEFAULT_BACKGROUND = '#9bb7d4';
const StaticGround = memo(Ground);
const StaticWalls = memo(Walls);
const StaticRoads = memo(Roads);
const StaticRoadAprons = memo(RoadAprons);
const StaticTerrain = memo(Terrain);
const StaticNeighborDistricts = memo(NeighborDistricts);
const StaticCeiling = memo(Ceiling);
const StaticBoxes = memo(Boxes);
const StaticConnectors = memo(Connectors);
const StaticBuildingNameBoards = memo(BuildingNameBoards);

export function LocationViewport({
  onQuestDialogRequested,
  onDeliverItemDialogRequested,
}: Pick<CreatureInteractionPanelProps, 'onQuestDialogRequested' | 'onDeliverItemDialogRequested'>) {
  const { scene } = useScene();
  const [focus, setFocus] = useState<CreatureFocus | null>(null);
  const [restoring, setRestoring] = useState(false);
  const [targetId, setTargetId] = useState<string>();
  const [renderScene, setRenderScene] = useState<Scene | null>(null);
  const weatherLook = weatherLookFor(scene.weather);
  const rendererRef = useRef<WebGPURenderer | null>(null);
  const closeInteraction = () => {
    setFocus(null);
    setRestoring(true);
  };

  const chatHub = useChatHub();
  const isInCombat = useIsInCombat();
  const hasActiveEncounter = useHasActiveEncounter();
  const selectedCreature = scene.nearbyCreatures.find((creature) => creature.id === focus?.id);
  useEffect(() => {
    if (focus && (!selectedCreature || isInCombat || hasActiveEncounter)) {
      setFocus(null);
      setRestoring(true);
    }
  }, [focus, selectedCreature, isInCombat, hasActiveEncounter]);
  const seated = scene.playerStatus.posture === 'Sitting';
  const canInteract = seated || (!isInCombat && !hasActiveEncounter);
  const handleSeatInteraction = useSeatInteraction(seated, canInteract);
  const [nearbySeat, setNearbySeat] = useState<ViewportSeat>();
  const [locked, setLocked] = useState(false);
  const [nearbyConnectorId, setNearbyConnectorId] = useState<string>();
  const names = useMemo(
    () => (scene ? buildEntityNames(scene) : new Map<string, string>()),
    [scene.nearbyCreatures, scene.nearbyBuildings, scene.nearbyProps, scene.exits],
  );
  const obstacles = useMemo(
    () => buildObstacles(scene.nearbyProps, scene.nearbyBuildings, scene.exits),
    [scene.nearbyProps, scene.nearbyBuildings, scene.exits],
  );

  const wells = useMemo(() => scene.exits.filter(({ stairs }) => stairs === 'Down'), [scene.exits]);
  const shafts = useMemo(() => scene.exits.filter(({ stairs }) => stairs === 'Up'), [scene.exits]);
  const walls = useMemo(
    () =>
      isRoomScene(scene) ? buildWalls(scene.size, scene.exits) : boundaryWalls(scene.boundary),
    [scene.roomName, scene.size, scene.exits, scene.boundary],
  );
  const towers = useMemo(() => boundaryTowers(scene.boundary), [scene.boundary]);
  const headers = useMemo(
    () => (isRoomScene(scene) ? buildDoorHeaders(scene.size, scene.exits) : []),
    [scene.roomName, scene.size, scene.exits],
  );
  const openEdges = useMemo(() => scene.boundary?.openEdges ?? [], [scene.boundary]);
  const seats = useMemo(() => buildSeats(scene.nearbyProps), [scene.nearbyProps]);

  if (!scene) {
    return null;
  }

  const {
    size,
    playerStatus,
    nearbyProps: props,
    nearbyBuildings: buildings,
    exits: connectors,
  } = scene;
  const creatures = [playerStatus, ...scene.nearbyCreatures];
  const start = findPlayerPlacement(scene) ?? {
    x: size.width / 2,
    y: size.depth / 2,
    angle: 0,
  };
  const occupiedSeat = seats.find((seat) => seat.isOccupiedByPlayer);
  const canTravel = !seated && canInteract;
  const nearbyName = nearbyConnectorId ? names.get(nearbyConnectorId) : undefined;

  const handleEnterConnector = ({ connectorId }: NearbyExitSnapshot) => {
    if (!canTravel) {
      return;
    }
    void runAction(chatHub.sendMove(connectorId));
  };

  const handlePoseChange = ({ x, y, angle }: PlacementWire) => {
    chatHub.reportPose(scene.locationId, x, y, angle).catch(() => {});
  };

  const targetCreature = scene.nearbyCreatures.find((creature) => creature.id === targetId);
  const interactionPrompt = targetCreature
    ? `E: Interact with ${targetCreature.name}`
    : seated
      ? 'E: Stand up'
      : nearbySeat
        ? nearbySeat.isOccupied
          ? `${nearbySeat.name} · Occupied`
          : `E: Sit on ${nearbySeat.name}`
        : nearbyConnectorId
          ? nearbyName
            ? `E: Enter ${nearbyName}`
            : 'E: Enter'
          : undefined;

  return (
    <div className="absolute inset-0 bg-black" role="region" aria-label="Location viewport">
      <div
        id={CANVAS_ID}
        className={focus ? 'h-[40%] w-full md:h-full md:w-[calc(100%-28rem)]' : 'size-full'}
        style={{ pointerEvents: focus || restoring ? 'none' : undefined }}
      >
        <Canvas
          key="webgpu"
          gl={createWebGpuRenderer}
          onCreated={({ gl, scene: canvasScene }) => {
            rendererRef.current = gl as unknown as WebGPURenderer;
            setRenderScene(canvasScene);
          }}
          shadows={{ type: PCFShadowMap }}
          camera={{ fov: 75, near: 0.1, far: 500 }}
        >
          <color attach="background" args={[DEFAULT_BACKGROUND]} />
          <StaticGround
            size={size}
            wells={wells}
            color={isRoomScene(scene) ? ROOM_FLOOR_COLOR : undefined}
          />
          <DebugFloorGrid size={size} />
          <StaticWalls walls={walls} headers={headers} towers={towers} />
          {scene.roads && <StaticRoads roads={scene.roads} />}
          {scene.roads && (
            <StaticRoadAprons roads={scene.roads} size={size} openEdges={openEdges} />
          )}
          {!isRoomScene(scene) && <StaticTerrain size={size} />}
          {scene.neighbors && <StaticNeighborDistricts neighbors={scene.neighbors} />}
          {isRoomScene(scene) && <StaticCeiling size={size} openings={shafts} />}
          <StaticBoxes props={props} buildings={buildings} />
          <Creatures
            creatures={creatures}
            playerId={playerStatus.id}
            names={names}
            statuses={[playerStatus, ...scene.nearbyCreatures]}
            playerSeat={occupiedSeat}
            focus={focus}
          />
          <StaticConnectors connectors={connectors} boundary={scene.boundary} size={size} />
          {!isRoomScene(scene) && <StaticBuildingNameBoards connectors={connectors} />}
          <CreatureFocusController
            focus={focus}
            creatures={creatures}
            statuses={scene.nearbyCreatures}
            enabled={!isInCombat && !hasActiveEncounter && !restoring}
            onTarget={setTargetId}
            onFocus={setFocus}
            onRestored={() => setRestoring(false)}
          />
          <FpsController
            movementSpeed={playerStatus.movementSpeed}
            movementLocked={!!focus || restoring}
            seats={seats}
            seated={seated}
            seatedPlacement={occupiedSeat?.placement}
            onNearbySeatChange={setNearbySeat}
            onSeatInteraction={handleSeatInteraction}
            size={size}
            start={start}
            obstacles={obstacles}
            connectors={connectors}
            lockSelector={`#${CANVAS_ID}`}
            onLockChange={setLocked}
            onNearbyConnectorChange={(connector) => setNearbyConnectorId(connector?.connectorId)}
            onEnterConnector={handleEnterConnector}
            onPoseChange={handlePoseChange}
          />
          {isRoomScene(scene) ? (
            <IndoorLighting size={size} props={props} />
          ) : (
            <>
              <OutdoorSky look={weatherLook} />
              <OutdoorFog size={size} look={weatherLook} />
              <WeatherParticles look={weatherLook} />
              <OutdoorLighting size={size} props={props} look={weatherLook} />
            </>
          )}
        </Canvas>
      </div>
      <RenderDiagnostics rendererRef={rendererRef} scene={renderScene} />
      {locked && !focus && (
        <span
          aria-hidden="true"
          className="pointer-events-none absolute top-1/2 left-1/2 size-1 -translate-x-1/2 -translate-y-1/2 rounded-full bg-white/80"
        />
      )}
      {!locked && !focus && (
        <p className="pointer-events-none absolute inset-x-0 top-4 text-center text-sm text-white drop-shadow">
          Click to look around. WASD to move, E to interact, Esc to release the mouse.
        </p>
      )}
      {focus && selectedCreature && (
        <CreatureInteractionPanel
          key={selectedCreature.id}
          scene={scene}
          creature={selectedCreature}
          onClose={closeInteraction}
          onQuestDialogRequested={onQuestDialogRequested}
          onDeliverItemDialogRequested={onDeliverItemDialogRequested}
        />
      )}
      {locked && !focus && interactionPrompt && canInteract && (
        <p className="pointer-events-none absolute inset-x-0 top-1/2 mt-12 text-center text-base font-medium text-white drop-shadow">
          {interactionPrompt}
        </p>
      )}
    </div>
  );
}
