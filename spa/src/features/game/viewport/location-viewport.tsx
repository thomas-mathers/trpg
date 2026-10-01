import { Canvas } from '@react-three/fiber';
import { XIcon } from 'lucide-react';
import { useMemo, useState } from 'react';

import { Button } from '@/components/ui/button';

import { useScene } from '../contexts/scene-context';
import { FpsController } from './fps-controller';
import { buildEntityNames, findPlayerPlacement } from './layout-math';
import { Boxes, Connectors, Creatures, Ground } from './viewport-scene';

const CANVAS_ID = 'location-viewport-canvas';

export function LocationViewport({ onClose }: { onClose: () => void }) {
  const scene = useScene();
  const [locked, setLocked] = useState(false);
  const names = useMemo(
    () => (scene ? buildEntityNames(scene) : new Map<string, string>()),
    [scene],
  );

  if (!scene) {
    return null;
  }

  const { layout, playerStatus } = scene;
  const { size, props, buildings, creatures, connectors } = layout;
  const start = findPlayerPlacement(scene) ?? { x: size.width / 2, y: size.depth / 2, angle: 0 };

  return (
    <div className="absolute inset-0 z-20 bg-black" role="region" aria-label="Location viewport">
      <div id={CANVAS_ID} className="size-full">
        <Canvas camera={{ fov: 75, near: 0.1, far: 500 }}>
          <color attach="background" args={['#9bb7d4']} />
          <ambientLight intensity={0.8} />
          <directionalLight position={[size.width, 40, -size.depth]} intensity={1.2} />
          <Ground size={size} />
          <Boxes props={props} buildings={buildings} names={names} />
          <Creatures creatures={creatures} playerId={playerStatus.id} names={names} />
          <Connectors connectors={connectors} names={names} />
          <FpsController
            size={size}
            start={start}
            lockSelector={`#${CANVAS_ID}`}
            onLockChange={setLocked}
          />
        </Canvas>
      </div>
      {!locked && (
        <p className="pointer-events-none absolute inset-x-0 bottom-6 text-center text-sm text-white drop-shadow">
          Click to look around. WASD to move. Esc to release the mouse.
        </p>
      )}
      <Button
        variant="secondary"
        size="icon"
        className="absolute top-3 right-3"
        aria-label="Close viewport"
        onClick={onClose}
      >
        <XIcon />
      </Button>
    </div>
  );
}
