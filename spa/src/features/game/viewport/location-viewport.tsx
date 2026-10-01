import { Canvas } from '@react-three/fiber';
import { useMemo, useState } from 'react';

import type { ConnectorLayoutWire } from '@/api/signalr-client/TRPG.GameSessions.Responses';
import { useHasActiveEncounter } from '@/features/encounters/hooks/use-has-active-encounter';

import { CHAT_INPUT_ID } from '../components/chat-input';
import { useScene } from '../contexts/scene-context';
import { useGameChat } from '../hooks/use-game-chat';
import { useChatHub } from '../hooks/use-game-hub-connection';
import { useIsInCombat } from '../hooks/use-is-in-combat';
import { FpsController } from './fps-controller';
import { buildEntityNames, buildObstacles, buildWalls, findPlayerPlacement } from './layout-math';
import { Boxes, Connectors, Creatures, Ground, Walls } from './viewport-scene';

const CANVAS_ID = 'location-viewport-canvas';

const focusChatInput = () => document.getElementById(CHAT_INPUT_ID)?.focus();

export function LocationViewport() {
  const scene = useScene();
  const chatHub = useChatHub();
  const { isStreaming, submitNarratedTurn } = useGameChat();
  const isInCombat = useIsInCombat();
  const hasActiveEncounter = useHasActiveEncounter();
  const [locked, setLocked] = useState(false);
  const [nearbyConnectorId, setNearbyConnectorId] = useState<string>();
  const names = useMemo(
    () => (scene ? buildEntityNames(scene) : new Map<string, string>()),
    [scene],
  );
  const obstacles = useMemo(
    () => (scene ? buildObstacles(scene.layout.props, scene.layout.buildings) : []),
    [scene],
  );

  const walls = useMemo(
    () => (scene?.roomName ? buildWalls(scene.layout.size, scene.layout.connectors) : []),
    [scene],
  );

  if (!scene) {
    return null;
  }

  const { layout, playerStatus } = scene;
  const { size, props, buildings, creatures, connectors } = layout;
  const start = findPlayerPlacement(scene) ?? { x: size.width / 2, y: size.depth / 2, angle: 0 };
  const canTravel = !isStreaming && !isInCombat && !hasActiveEncounter;
  const nearbyName = nearbyConnectorId ? names.get(nearbyConnectorId) : undefined;

  const handleEnterConnector = ({ connectorId }: ConnectorLayoutWire) => {
    if (!canTravel) {
      return;
    }
    submitNarratedTurn(null, chatHub.sendMove(connectorId));
  };

  return (
    <div className="absolute inset-0 bg-black" role="region" aria-label="Location viewport">
      <div id={CANVAS_ID} className="size-full">
        <Canvas camera={{ fov: 75, near: 0.1, far: 500 }}>
          <color attach="background" args={['#9bb7d4']} />
          <ambientLight intensity={0.8} />
          <directionalLight position={[size.width, 40, -size.depth]} intensity={1.2} />
          <Ground size={size} />
          <Walls walls={walls} />
          <Boxes props={props} buildings={buildings} names={names} />
          <Creatures creatures={creatures} playerId={playerStatus.id} names={names} />
          <Connectors connectors={connectors} />
          <FpsController
            size={size}
            start={start}
            obstacles={obstacles}
            connectors={connectors}
            lockSelector={`#${CANVAS_ID}`}
            onLockChange={setLocked}
            onNearbyConnectorChange={(connector) => setNearbyConnectorId(connector?.connectorId)}
            onEnterConnector={handleEnterConnector}
            onChatRequested={focusChatInput}
          />
        </Canvas>
      </div>
      {!locked && (
        <p className="pointer-events-none absolute inset-x-0 top-4 text-center text-sm text-white drop-shadow">
          Click to look around. WASD to move, Enter to chat, Esc to release the mouse.
        </p>
      )}
      {locked && nearbyConnectorId && canTravel && (
        <p className="pointer-events-none absolute inset-x-0 top-1/2 mt-12 text-center text-base font-medium text-white drop-shadow">
          {nearbyName ? `E: Enter ${nearbyName}` : 'E: Enter'}
        </p>
      )}
    </div>
  );
}
