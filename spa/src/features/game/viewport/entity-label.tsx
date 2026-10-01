import { useFrame } from '@react-three/fiber';
import { useEffect, useMemo, useRef } from 'react';
import { CanvasTexture, SRGBColorSpace, type Sprite, Vector3 } from 'three';

const LABEL_WORLD_HEIGHT = 0.2;
const REFERENCE_DISTANCE = 10;
const CONSTANT_SIZE_UNTIL = 25;
const PULL_TOWARD_CAMERA = 1.2;
const FONT_PX = 56;
const PADDING_X = 28;
const PADDING_Y = 14;

interface LabelTexture {
  texture: CanvasTexture;
  aspect: number;
}

function drawLabel(text: string): LabelTexture {
  const canvas = document.createElement('canvas');
  const context = canvas.getContext('2d');
  if (!context) {
    throw new Error('2D canvas context unavailable');
  }

  const font = `600 ${FONT_PX}px system-ui, sans-serif`;
  context.font = font;
  const width = Math.ceil(context.measureText(text).width) + PADDING_X * 2;
  const height = FONT_PX + PADDING_Y * 2;
  canvas.width = width;
  canvas.height = height;

  context.font = font;
  context.fillStyle = 'rgba(0, 0, 0, 0.45)';
  context.beginPath();
  context.roundRect(0, 0, width, height, height / 4);
  context.fill();
  context.fillStyle = '#ffffff';
  context.textBaseline = 'middle';
  context.fillText(text, PADDING_X, height / 2 + 2);

  const texture = new CanvasTexture(canvas);
  texture.colorSpace = SRGBColorSpace;
  return { texture, aspect: width / height };
}

export function EntityLabel({ text }: { text?: string }) {
  const label = useMemo(() => (text ? drawLabel(text) : null), [text]);

  const spriteRef = useRef<Sprite>(null);
  const anchor = useMemo(() => new Vector3(), []);
  const towardCamera = useMemo(() => new Vector3(), []);

  useEffect(() => () => label?.texture.dispose(), [label]);

  useFrame(({ camera }) => {
    const sprite = spriteRef.current;
    const parent = sprite?.parent;
    if (!label || !sprite || !parent) {
      return;
    }
    parent.getWorldPosition(anchor);
    towardCamera.copy(camera.position).sub(anchor);
    const distance = towardCamera.length();
    towardCamera.normalize();
    anchor.addScaledVector(towardCamera, Math.min(PULL_TOWARD_CAMERA, distance / 2));
    sprite.position.copy(parent.worldToLocal(anchor));

    const height =
      (LABEL_WORLD_HEIGHT * Math.min(distance, CONSTANT_SIZE_UNTIL)) / REFERENCE_DISTANCE;
    sprite.scale.set(height * label.aspect, height, 1);
  });

  if (!label) {
    return null;
  }

  return (
    <sprite ref={spriteRef} scale={[LABEL_WORLD_HEIGHT * label.aspect, LABEL_WORLD_HEIGHT, 1]}>
      <spriteMaterial map={label.texture} transparent depthWrite={false} toneMapped={false} />
    </sprite>
  );
}
