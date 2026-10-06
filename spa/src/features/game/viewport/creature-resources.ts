import {
  BoxGeometry,
  CapsuleGeometry,
  ConeGeometry,
  CylinderGeometry,
  MeshStandardMaterial,
  SphereGeometry,
} from 'three';

const capsuleGeometries = new Map<string, CapsuleGeometry>();
const materials = new Map<string, MeshStandardMaterial>();

export const creatureHeadGeometry = new SphereGeometry(0.18, 16, 12);
export const creatureFootGeometry = new BoxGeometry(0.19, 0.14, 0.3);
export const unitBoxGeometry = new BoxGeometry(1, 1, 1);
export const handGeometry = new SphereGeometry(0.085, 10, 8);
export const helmGeometry = new SphereGeometry(0.2, 16, 8, 0, Math.PI * 2, 0, Math.PI / 2);
export const shieldGeometry = new CylinderGeometry(0.3, 0.3, 0.04, 20);
export const earGeometry = new ConeGeometry(0.045, 0.16, 8);
export const hornGeometry = new ConeGeometry(0.04, 0.2, 8);

export function creatureCapsuleGeometry(radius: number, length: number) {
  const key = `${radius}:${length}`;
  let geometry = capsuleGeometries.get(key);
  if (!geometry) {
    geometry = new CapsuleGeometry(radius, length, 6, 12);
    capsuleGeometries.set(key, geometry);
  }
  return geometry;
}

export function creatureMaterial(color: string) {
  let material = materials.get(color);
  if (!material) {
    material = new MeshStandardMaterial({ color, roughness: 0.85 });
    materials.set(color, material);
  }
  return material;
}
