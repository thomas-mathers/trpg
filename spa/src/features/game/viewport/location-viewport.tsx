import { Canvas } from '@react-three/fiber';
import { useEffect, useMemo, useState } from 'react';

import type { NearbyExitSnapshot } from '@/api/signalr-client/TRPG.GameSessions.Responses';
import { useHasActiveEncounter } from '@/features/encounters/hooks/use-has-active-encounter';

import {
  CreatureInteractionPanel,
  type CreatureInteractionPanelProps,
} from '../components/creature-interaction-panel';
import { useScene } from '../contexts/scene-context';
import { useChatHub } from '../hooks/use-game-hub-connection';
import { useIsInCombat } from '../hooks/use-is-in-combat';
import { runAction } from '../run-action';
import type { CreatureFocus } from './creature-focus';
import { CreatureFocusController } from './creature-focus-controller';
import { FpsController } from './fps-controller';
import { buildEntityNames, buildObstacles, buildWalls, findPlayerPlacement } from './layout-math';
import { buildSeats, type ViewportSeat } from './seat-interaction';
import { useSeatInteraction } from './use-seat-interaction';
import { Boxes, Connectors, Creatures, Ground, Walls } from './viewport-scene';

const CANVAS_ID = 'location-viewport-canvas';

export function LocationViewport({
  onQuestDialogRequested,
  onDeliverItemDialogRequested,
}: Pick<CreatureInteractionPanelProps, 'onQuestDialogRequested' | 'onDeliverItemDialogRequested'>) {
  const { scene } = useScene();
  const [focus, setFocus] = useState<CreatureFocus | null>(null);
  const [restoring, setRestoring] = useState(false);
  const [targetId, setTargetId] = useState<string>();
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
    [scene],
  );
  const obstacles = useMemo(
    () => buildObstacles(scene.nearbyProps, scene.nearbyBuildings),
    [scene],
  );

  const walls = useMemo(() => (scene.roomName ? buildWalls(scene.size, scene.exits) : []), [scene]);

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
  const seats = buildSeats(props);
  const occupiedSeat = seats.find((seat) => seat.isOccupiedByPlayer);
  const canTravel = !seated && canInteract;
  const nearbyName = nearbyConnectorId ? names.get(nearbyConnectorId) : undefined;

  const handleEnterConnector = ({ connectorId }: NearbyExitSnapshot) => {
    if (!canTravel) {
      return;
    }
    void runAction(chatHub.sendMove(connectorId));
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
        <Canvas camera={{ fov: 75, near: 0.1, far: 500 }}>
          <color attach="background" args={['#9bb7d4']} />
          <ambientLight intensity={0.8} />
          <directionalLight position={[size.width, 40, -size.depth]} intensity={1.2} />
          <Ground size={size} />
          <Walls walls={walls} />
          <Boxes props={props} buildings={buildings} names={names} />
          <Creatures
            creatures={creatures}
            playerId={playerStatus.id}
            names={names}
            statuses={[playerStatus, ...scene.nearbyCreatures]}
            playerSeat={occupiedSeat}
            focus={focus}
          />
          <Connectors connectors={connectors} />
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
          />
        </Canvas>
      </div>
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
