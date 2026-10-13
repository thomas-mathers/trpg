import { useFrame } from '@react-three/fiber';
import { useEffect, useMemo, useRef } from 'react';
import { CanvasTexture, SRGBColorSpace, type Sprite, Vector3 } from 'three';

import type { TargetPlate } from './target-plate';

const LABEL_WORLD_HEIGHT = 0.2;
const REFERENCE_DISTANCE = 10;
const CONSTANT_SIZE_UNTIL = 25;
const PULL_TOWARD_CAMERA = 1.2;
const HEAD_GAP_RATIO = 0.6;
const TARGET_RENDER_ORDER = 10;
const FONT_PX = 56;
const PADDING_X = 28;
const PADDING_Y = 14;
const TITLE_HEIGHT = FONT_PX + PADDING_Y * 2;
const BAR_HEIGHT = 20;
const BAR_GAP = 10;
const BAR_MIN_WIDTH = 440;

interface LabelTexture {
  texture: CanvasTexture;
  aspect: number;
  heightRatio: number;
}

function drawBars(
  context: CanvasRenderingContext2D,
  bars: TargetPlate['bars'],
  width: number,
): void {
  const barWidth = width - PADDING_X * 2;
  bars.forEach(({ color, fraction }, index) => {
    const top = TITLE_HEIGHT + index * (BAR_HEIGHT + BAR_GAP) - PADDING_Y / 2;
    context.fillStyle = 'rgba(255, 255, 255, 0.2)';
    context.beginPath();
    context.roundRect(PADDING_X, top, barWidth, BAR_HEIGHT, BAR_HEIGHT / 2);
    context.fill();
    if (fraction > 0) {
      context.fillStyle = color;
      context.beginPath();
      context.roundRect(
        PADDING_X,
        top,
        Math.max(BAR_HEIGHT, barWidth * fraction),
        BAR_HEIGHT,
        BAR_HEIGHT / 2,
      );
      context.fill();
    }
  });
}

function drawLabel(text: string, color: string, plate?: TargetPlate): LabelTexture {
  const canvas = document.createElement('canvas');
  const context = canvas.getContext('2d');
  if (!context) {
    throw new Error('2D canvas context unavailable');
  }

  const bars = plate?.bars ?? [];
  const font = `600 ${FONT_PX}px system-ui, sans-serif`;
  context.font = font;
  const textWidth = Math.ceil(context.measureText(text).width) + PADDING_X * 2;
  const width = bars.length > 0 ? Math.max(textWidth, BAR_MIN_WIDTH) : textWidth;
  const barsHeight = bars.length > 0 ? bars.length * (BAR_HEIGHT + BAR_GAP) : 0;
  const height = TITLE_HEIGHT + barsHeight;
  canvas.width = width;
  canvas.height = height;

  context.font = font;
  context.fillStyle = '#14110e';
  context.beginPath();
  context.roundRect(0, 0, width, height, TITLE_HEIGHT / 4);
  context.fill();
  context.fillStyle = color;
  context.textBaseline = 'middle';
  context.fillText(text, (width - textWidth) / 2 + PADDING_X, TITLE_HEIGHT / 2 + 2);
  drawBars(context, bars, width);

  const texture = new CanvasTexture(canvas);
  texture.colorSpace = SRGBColorSpace;
  return {
    texture,
    aspect: width / height,
    heightRatio: height / TITLE_HEIGHT,
  };
}

export function EntityLabel({
  text,
  color = '#ffffff',
  plate,
}: {
  text?: string;
  color?: string;
  plate?: TargetPlate;
}) {
  const label = useMemo(() => (text ? drawLabel(text, color, plate) : null), [text, color, plate]);

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

    const titleHeight =
      (LABEL_WORLD_HEIGHT * Math.min(distance, CONSTANT_SIZE_UNTIL)) / REFERENCE_DISTANCE;
    const height = titleHeight * label.heightRatio;
    sprite.position.y += titleHeight * HEAD_GAP_RATIO;
    sprite.scale.set(height * label.aspect, height, 1);
  });

  if (!label) {
    return null;
  }

  return (
    <sprite
      ref={spriteRef}
      renderOrder={plate ? TARGET_RENDER_ORDER : 0}
      center={[0.5, 0]}
      scale={[
        LABEL_WORLD_HEIGHT * label.heightRatio * label.aspect,
        LABEL_WORLD_HEIGHT * label.heightRatio,
        1,
      ]}
    >
      <spriteMaterial
        map={label.texture}
        transparent
        depthWrite={false}
        depthTest={!plate}
        toneMapped={false}
      />
    </sprite>
  );
}
