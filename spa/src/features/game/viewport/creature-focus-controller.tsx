import { useFrame, useThree } from '@react-three/fiber';
import { useEffect, useRef } from 'react';
import { PerspectiveCamera, Quaternion, Raycaster } from 'three';

import type { CreatureStatusSnapshot } from '@/api/signalr-client/TRPG.GameSessions.Responses';

import { focusCreature, pickCreature, type CreatureFocus } from './creature-focus';

export function CreatureFocusController({
  focus,
  creatures,
  statuses,
  enabled,
  onTarget,
  onFocus,
  onRestored,
}: {
  focus: CreatureFocus | null;
  creatures: Pick<CreatureStatusSnapshot, 'id' | 'placement'>[];
  statuses: CreatureStatusSnapshot[];
  enabled: boolean;
  onTarget: (id: string | undefined) => void;
  onFocus: (focus: CreatureFocus) => void;
  onRestored: () => void;
}) {
  const { camera, scene } = useThree();
  const raycaster = useRef(new Raycaster());
  const target = useRef<string | undefined>(undefined);
  useConversationCamera(focus, onRestored);
  useEffect(() => {
    const interact = (event: KeyboardEvent) => {
      if (
        !enabled ||
        focus ||
        !document.pointerLockElement ||
        event.code !== 'KeyE' ||
        event.repeat
      )
        return;
      const id = pickCreature(camera, scene, raycaster.current);
      const creature = creatures.find((entry) => entry.id === id);
      const status = statuses.find((entry) => entry.id === id);
      if (!creature || !status) return;
      event.preventDefault();
      event.stopImmediatePropagation();
      onFocus(focusCreature(status, camera.position));
      document.exitPointerLock();
    };
    window.addEventListener('keydown', interact, true);
    return () => window.removeEventListener('keydown', interact, true);
  }, [camera, scene, enabled, focus, creatures, statuses, onFocus]);
  useFrame(() => {
    const id = enabled && !focus ? pickCreature(camera, scene, raycaster.current) : undefined;
    if (id !== target.current) {
      target.current = id;
      onTarget(id);
    }
  });
  return null;
}

function useConversationCamera(focus: CreatureFocus | null, onRestored: () => void) {
  const camera = useThree((state) => state.camera);
  const original = useRef<{ rotation: Quaternion; fov: number } | null>(null);
  const desired = useRef(new Quaternion());
  useEffect(() => {
    if (!focus || !(camera instanceof PerspectiveCamera)) return;
    original.current ??= { rotation: camera.quaternion.clone(), fov: camera.fov };
    const frame = camera.clone();
    frame.lookAt(focus.x, focus.headHeight - 0.15, focus.y);
    desired.current.copy(frame.quaternion);
  }, [camera, focus]);
  useFrame((_, dt) => {
    const saved = original.current;
    if (!saved || !(camera instanceof PerspectiveCamera)) return;
    const amount = matchMedia('(prefers-reduced-motion: reduce)').matches
      ? 1
      : 1 - Math.exp(-8 * dt);
    const rotation = focus ? desired.current : saved.rotation;
    const fov = focus ? 38 : saved.fov;
    camera.quaternion.slerp(rotation, amount);
    camera.fov += (fov - camera.fov) * amount;
    camera.updateProjectionMatrix();
    if (
      !focus &&
      camera.quaternion.angleTo(rotation) < 0.002 &&
      Math.abs(camera.fov - fov) < 0.05
    ) {
      camera.quaternion.copy(rotation);
      camera.fov = fov;
      camera.updateProjectionMatrix();
      original.current = null;
      onRestored();
    }
  });
}
