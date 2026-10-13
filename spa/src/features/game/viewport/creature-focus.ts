import { Mesh, Object3D, Raycaster, Vector2, type Camera, type Scene } from 'three';

import type { CreatureStatusSnapshot } from '@/api/signalr-client/TRPG.GameSessions.Responses';

export interface CreatureFocus {
  id: string;
  x: number;
  y: number;
  headHeight: number;
  facing: number;
}

const screenCenter = new Vector2();
const INTERACTION_REACH = 3;

export function pickCreature(
  camera: Camera,
  scene: Scene,
  raycaster: Raycaster,
): string | undefined {
  raycaster.setFromCamera(screenCenter, camera);
  raycaster.far = INTERACTION_REACH;
  const hit = raycaster
    .intersectObjects(scene.children, true)
    .find(({ object }) => object instanceof Mesh);
  if (!hit) return;
  let node: Object3D | null = hit.object;
  while (node) {
    if (typeof node.userData.creatureId === 'string') return node.userData.creatureId;
    node = node.parent;
  }
}

export function focusCreature(
  creature: CreatureStatusSnapshot,
  player: { x: number; z: number },
  position: { x: number; y: number } = creature.placement,
): CreatureFocus {
  const { x, y } = position;
  return {
    id: creature.id,
    x,
    y,
    headHeight: creature.posture === 'Sitting' ? 1.36 : 1.63,
    facing: Math.atan2(-(player.x - x), -(player.z - y)),
  };
}

export function findCreatureObject(scene: Scene, id: string): Object3D | undefined {
  let creature: Object3D | undefined;
  scene.traverse((object) => {
    if (!creature && object.userData.creatureId === id) creature = object;
  });
  return creature;
}
