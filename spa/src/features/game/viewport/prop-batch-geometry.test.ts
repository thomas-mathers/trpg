import {
  Color,
  Frustum,
  Matrix4,
  Mesh,
  MeshBasicMaterial,
  PerspectiveCamera,
  Raycaster,
  Vector3,
} from 'three';
import { describe, expect, it } from 'vitest';

import type {
  NearbyPropSnapshot,
  PropModel,
} from '@/api/signalr-client/TRPG.GameSessions.Responses';

import { FURNITURE_BUILDERS } from './furniture-parts';
import { buildPropBatches } from './prop-batch-geometry';

function prop(model: PropModel, x = 3, y = 3): NearbyPropSnapshot {
  return {
    id: `${model}:${x}:${y}`,
    model,
    placement: { x, y, angle: 0 },
    footprint: { width: 2, depth: 1 },
  } as NearbyPropSnapshot;
}

describe('prop batching', () => {
  it('renders nearby tables in one draw without losing their geometry or colors', () => {
    const { batches, moving } = buildPropBatches([
      prop('FurnitureTable'),
      prop('FurnitureTable', 6),
    ]);
    expect(batches).toHaveLength(1);
    expect(moving).toHaveLength(0);
    const { geometry } = batches[0];
    expect(geometry.index?.count).toBe(10 * 36);
    expect(geometry.groups).toHaveLength(0);
    const colors = geometry.getAttribute('color');
    const darkWood = new Color('#5e4529');
    expect(colors.getX(24)).toBeCloseTo(darkWood.r);
    expect(colors.getY(24)).toBeCloseTo(darkWood.g);
    expect(colors.getZ(24)).toBeCloseTo(darkWood.b);
    expect(colors.getX(0)).not.toBe(colors.getX(24));
  });

  it('preserves world position and orientation of asymmetric props', () => {
    const table = prop('FurnitureTable', 4, 6);
    table.placement.angle = Math.PI / 2;
    const { geometry } = buildPropBatches([table]).batches[0];
    const bounds = geometry.boundingBox!;
    expect(bounds.min.x).toBeCloseTo(3.5);
    expect(bounds.max.x).toBeCloseTo(4.5);
    expect(bounds.min.z).toBeCloseTo(5);
    expect(bounds.max.z).toBeCloseTo(7);
    expect(bounds.min.y).toBeCloseTo(0);
    expect(bounds.max.y).toBeCloseTo(0.8);
  });

  it('keeps distant props independently cullable', () => {
    const { batches } = buildPropBatches([prop('FurnitureTable'), prop('FurnitureTable', 100)]);
    expect(batches).toHaveLength(2);
    const camera = new PerspectiveCamera(75, 1, 0.1, 50);
    camera.position.set(3, 1, 10);
    camera.updateMatrixWorld();
    const frustum = new Frustum().setFromProjectionMatrix(
      new Matrix4().multiplyMatrices(camera.projectionMatrix, camera.matrixWorldInverse),
    );
    expect(batches.map(({ geometry }) => frustum.intersectsObject(new Mesh(geometry)))).toEqual([
      true,
      false,
    ]);
  });

  it('preserves openings for targeting and occlusion', () => {
    const { geometry } = buildPropBatches([prop('FurnitureTable', 0, 0)]).batches[0];
    const mesh = new Mesh(geometry, new MeshBasicMaterial());
    const beneath = new Raycaster(new Vector3(0, 0.4, 2), new Vector3(0, 0, -1));
    const tabletop = new Raycaster(new Vector3(0, 0.75, 2), new Vector3(0, 0, -1));
    expect(beneath.intersectObject(mesh)).toHaveLength(0);
    expect(tabletop.intersectObject(mesh).length).toBeGreaterThan(0);
  });

  it('keeps metal, emissive surfaces, and hollow basins in separate materials', () => {
    const { batches } = buildPropBatches([
      prop('ContainerBarrel'),
      prop('FurnitureShrine'),
      prop('FurnitureWell'),
    ]);
    expect(batches.some(({ finish }) => finish === 'metal')).toBe(true);
    expect(batches.find(({ finish }) => finish === 'glow')?.emissive).toBe('#e19c51');
    expect(batches.some(({ doubleSided }) => doubleSided)).toBe(true);
  });

  it('leaves glass and animated water individually sortable', () => {
    const { moving } = buildPropBatches([prop('FurnitureFountain'), prop('FurnitureDisplayCase')]);
    expect(moving.filter(({ part }) => part.finish === 'water')).toHaveLength(14);
    expect(moving.some(({ part }) => part.opacity === 0.35 && part.finish !== 'water')).toBe(true);
    expect(moving.every(({ placement }) => placement.x === 3 && placement.y === 3)).toBe(true);
  });

  it('preserves chair height and excludes signs that carry text', () => {
    const sign = { ...prop('Sign'), description: 'Market' };
    const { batches } = buildPropBatches([prop('SeatChair'), sign]);
    expect(batches).toHaveLength(1);
    expect(batches[0].geometry.index?.count).toBe(6 * 36);
    expect(batches[0].geometry.boundingBox?.max.y).toBeCloseTo(0.9);
  });

  it.each(Object.keys(FURNITURE_BUILDERS) as PropModel[])(
    'builds finite, cullable geometry for %s',
    (model) => {
      const { batches } = buildPropBatches([prop(model)]);
      expect(batches.length).toBeGreaterThan(0);
      for (const { geometry } of batches) {
        expect([...geometry.getAttribute('position').array].every(Number.isFinite)).toBe(true);
        expect(geometry.boundingSphere?.radius).toBeGreaterThan(0);
        expect(geometry.getAttribute('color').count).toBe(geometry.getAttribute('position').count);
      }
    },
  );
});
