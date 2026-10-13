import {
  BoxGeometry,
  BufferGeometry,
  Color,
  Float32BufferAttribute,
  MeshStandardMaterial,
  TorusGeometry,
} from 'three';
import { mergeGeometries } from 'three/addons/utils/BufferGeometryUtils.js';

import { addGrainAttribute } from './wood-grain';
import { woodMaterial } from './wood-material';

function coloredGeometry(geometry: BufferGeometry, color: string) {
  const tint = new Color(color);
  const colors = new Float32Array(geometry.attributes.position.count * 3);
  for (let index = 0; index < colors.length; index += 3) {
    colors[index] = tint.r;
    colors[index + 1] = tint.g;
    colors[index + 2] = tint.b;
  }
  geometry.setAttribute('color', new Float32BufferAttribute(colors, 3));
  return geometry;
}

function coloredBox(
  size: [number, number, number],
  position: [number, number, number],
  color: string,
) {
  const geometry = new BoxGeometry(...size);
  geometry.translate(...position);
  return coloredGeometry(geometry, color);
}

function timberBox(
  size: [number, number, number],
  position: [number, number, number],
  color: string,
) {
  const geometry = coloredBox(size, position, color);
  addGrainAttribute(geometry, { shape: 'box', position, size, color }, 0, 0);
  return geometry;
}

function timberPieces() {
  return [
    ...[-0.7, 0.7].map((x) => timberBox([0.18, 2.5, 0.3], [x, 1.25, 0], '#65503a')),
    timberBox([1.62, 0.22, 0.36], [0, 2.53, 0], '#806548'),
    ...[-2, -1, 0, 1, 2].map((index) =>
      timberBox([0.23, 2.3, 0.09], [index * 0.24, 1.23, 0.08], index % 2 ? '#705035' : '#7e5c3b'),
    ),
  ];
}

function fittingPieces() {
  return [
    coloredBox([1.6, 0.09, 0.55], [0, 0.045, 0], '#999080'),
    ...[0.55, 1.9].map((height) => coloredBox([1.16, 0.085, 0.035], [0, height, 0.135], '#333c40')),
    coloredGeometry(new TorusGeometry(0.085, 0.018, 8, 16).translate(0.37, 1.17, 0.18), '#d2ac63'),
    coloredBox([0.38, 0.045, 0.015], [0, 2.53, 0.19], '#f5d88e'),
  ];
}

function mergePieces(pieces: BufferGeometry[]) {
  const geometry = mergeGeometries(pieces);
  pieces.forEach((piece) => piece.dispose());
  if (!geometry) throw new Error('Unable to merge door geometry');
  return geometry;
}

export const doorTimberGeometry = mergePieces(timberPieces());
export const doorFittingsGeometry = mergePieces(fittingPieces());
export const doorTimberMaterial = woodMaterial();
export const doorFittingsMaterial = new MeshStandardMaterial({
  vertexColors: true,
  roughness: 0.85,
});

export function DoorConnector() {
  return (
    <>
      <mesh
        geometry={doorTimberGeometry}
        material={doorTimberMaterial}
        receiveShadow
        dispose={null}
      />
      <mesh
        geometry={doorFittingsGeometry}
        material={doorFittingsMaterial}
        receiveShadow
        dispose={null}
      />
    </>
  );
}
