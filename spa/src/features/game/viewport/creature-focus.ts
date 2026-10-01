import { Mesh, Object3D, Raycaster, Vector2, type Camera, type Scene } from 'three';

import type {
  CreatureLayoutWire,
  CreatureStatusSnapshot,
} from '@/api/signalr-client/TRPG.GameSessions.Responses';

export interface CreatureFocus {
  id: string;
  x: number;
  y: number;
  headHeight: number;
  facing: number;
}

export function pickCreature(
  camera: Camera,
  scene: Scene,
  raycaster: Raycaster,
): string | undefined {
  raycaster.setFromCamera(new Vector2(), camera);
  const hit = raycaster
    .intersectObjects(scene.children, true)
    .find(({ object }) => object instanceof Mesh);
  if (!hit || hit.distance > 3) return;
  let node: Object3D | null = hit.object;
  while (node) {
    if (typeof node.userData.creatureId === 'string') return node.userData.creatureId;
    node = node.parent;
  }
}

export function focusCreature(
  creature: CreatureLayoutWire,
  status: CreatureStatusSnapshot,
  player: { x: number; z: number },
): CreatureFocus {
  const { x, y } = creature.placement;
  return {
    id: creature.id,
    x,
    y,
    headHeight: status.posture === 'Sitting' ? 1.36 : 1.63,
    facing: Math.atan2(-(player.x - x), -(player.z - y)),
  };
}
