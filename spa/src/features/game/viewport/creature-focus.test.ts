import {
  BoxGeometry,
  Group,
  Mesh,
  MeshBasicMaterial,
  PerspectiveCamera,
  Raycaster,
  Scene,
} from 'three';
import { describe, expect, it } from 'vitest';

import type { CreatureStatusSnapshot } from '@/api/signalr-client/TRPG.GameSessions.Responses';

import { focusCreature, pickCreature } from './creature-focus';

function setup() {
  const camera = new PerspectiveCamera(75, 1, 0.1, 100);
  const scene = new Scene();
  const npc = new Group();
  npc.userData.creatureId = 'npc';
  npc.position.z = -2;
  npc.add(new Mesh(new BoxGeometry(0.6, 1.8, 0.6), new MeshBasicMaterial()));
  scene.add(npc);
  scene.updateMatrixWorld(true);
  camera.updateMatrixWorld(true);
  return { camera, scene, npc, raycaster: new Raycaster() };
}
describe('NPC targeting', () => {
  it('targets the creature under the crosshair, including from behind', () => {
    const { camera, scene, raycaster, npc } = setup();
    npc.rotation.y = Math.PI;
    scene.updateMatrixWorld(true);
    expect(pickCreature(camera, scene, raycaster)).toBe('npc');
  });
  it('does not interact through a wall', () => {
    const { camera, scene, raycaster } = setup();
    const wall = new Mesh(new BoxGeometry(3, 3, 0.2), new MeshBasicMaterial());
    wall.position.z = -1;
    scene.add(wall);
    scene.updateMatrixWorld(true);
    expect(pickCreature(camera, scene, raycaster)).toBeUndefined();
  });
  it('does not target a creature beyond reach or outside the crosshair', () => {
    const { camera, scene, raycaster, npc } = setup();
    npc.position.z = -5;
    scene.updateMatrixWorld(true);
    expect(pickCreature(camera, scene, raycaster)).toBeUndefined();
    npc.position.set(2, 0, -2);
    scene.updateMatrixWorld(true);
    expect(pickCreature(camera, scene, raycaster)).toBeUndefined();
  });
  it.each([
    [0, 2, Math.PI],
    [0, -2, 0],
    [2, 0, -Math.PI / 2],
    [-2, 0, Math.PI / 2],
  ])('faces a player at (%s, %s)', (x, z, expected) => {
    const result = focusCreature(
      { id: 'npc', placement: { x: 0, y: 0, angle: 0 } },
      { posture: 'Sitting' } as CreatureStatusSnapshot,
      { x, z },
    );
    expect(Math.cos(result.facing)).toBeCloseTo(Math.cos(expected));
    expect(Math.sin(result.facing)).toBeCloseTo(Math.sin(expected));
    expect(result.headHeight).toBe(1.36);
  });
});
