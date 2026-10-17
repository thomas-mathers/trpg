import { useFrame } from '@react-three/fiber';
import { useMemo, useRef } from 'react';
import {
  CanvasTexture,
  LinearFilter,
  SRGBColorSpace,
  type Sprite,
  type SpriteMaterial,
} from 'three';

import type { FloatingText } from '../hooks/use-combat-effects';
import { COMBAT_TEXT_COLORS } from './combat-text';

const FONT_PX = 96;
const WORLD_HEIGHT = 0.4;
const RISE_METERS = 0.9;
const LIFETIME_SECONDS = 1.1;
const FADE_START = 0.6;

const textures = new Map<string, DrawnText>();

interface DrawnText {
  texture: CanvasTexture;
  aspect: number;
}

function textureFor(floater: FloatingText): DrawnText {
  const key = `${floater.kind}:${floater.text}`;
  const cached = textures.get(key);
  if (cached) {
    return cached;
  }
  const drawn = drawText(floater);
  textures.set(key, drawn);
  return drawn;
}

function drawText({ text, kind }: FloatingText): DrawnText {
  const canvas = document.createElement('canvas');
  const context = canvas.getContext('2d');
  if (!context) {
    throw new Error('2D canvas context unavailable');
  }
  const font = `800 ${FONT_PX}px system-ui, sans-serif`;
  context.font = font;
  canvas.width = Math.ceil(context.measureText(text).width) + 24;
  canvas.height = FONT_PX + 24;
  context.font = font;
  context.textBaseline = 'middle';
  context.lineWidth = 3;
  context.lineJoin = 'round';
  context.strokeStyle = '#14110e';
  context.strokeText(text, 12, canvas.height / 2 + 4);
  context.fillStyle = COMBAT_TEXT_COLORS[kind];
  context.fillText(text, 12, canvas.height / 2 + 4);
  const texture = new CanvasTexture(canvas);
  texture.colorSpace = SRGBColorSpace;
  texture.generateMipmaps = false;
  texture.minFilter = LinearFilter;
  return { texture, aspect: canvas.width / canvas.height };
}

function FloatingTextSprite({
  floater,
  baseHeight,
  index,
}: {
  floater: FloatingText;
  baseHeight: number;
  index: number;
}) {
  const drawn = useMemo(() => textureFor(floater), [floater]);
  const sprite = useRef<Sprite>(null);
  const startedAt = useRef<number | null>(null);

  useFrame(({ clock }) => {
    if (!sprite.current) return;
    startedAt.current ??= clock.elapsedTime;
    const progress = Math.min(1, (clock.elapsedTime - startedAt.current) / LIFETIME_SECONDS);
    sprite.current.position.y = baseHeight + index * 0.3 + progress * RISE_METERS;
    (sprite.current.material as SpriteMaterial).opacity =
      progress < FADE_START ? 1 : 1 - (progress - FADE_START) / (1 - FADE_START);
  });

  return (
    <sprite
      ref={sprite}
      renderOrder={20}
      position={[0, baseHeight, 0]}
      scale={[WORLD_HEIGHT * drawn.aspect, WORLD_HEIGHT, 1]}
    >
      <spriteMaterial
        map={drawn.texture}
        transparent
        alphaTest={0.02}
        depthTest={false}
        depthWrite={false}
        toneMapped={false}
      />
    </sprite>
  );
}

export function FloatingCombatText({
  floaters,
  baseHeight,
}: {
  floaters: FloatingText[];
  baseHeight: number;
}) {
  return (
    <>
      {floaters.map((floater, index) => (
        <FloatingTextSprite
          key={floater.id}
          floater={floater}
          baseHeight={baseHeight}
          index={index}
        />
      ))}
    </>
  );
}
