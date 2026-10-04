import { useEffect, useMemo } from 'react';
import { CanvasTexture, SRGBColorSpace } from 'three';

import type { FootprintWire } from '@/api/signalr-client/TRPG.GameSessions.Responses';

import type { BoxStyle } from './model-styles';

const BOARD_HEIGHT = 0.5;
const BOARD_THICKNESS = 0.06;
const POST_WIDTH = 0.1;
const BOARD_COLOR = '#b08c5a';
const TEXT_COLOR = '#2b1d0e';
const FONT_PX = 72;
const TEXTURE_HEIGHT = 128;
const TEXTURE_WIDTH_PER_METER = 320;

function drawBoardText(text: string, width: number): CanvasTexture {
  const canvas = document.createElement('canvas');
  canvas.width = Math.ceil(width * TEXTURE_WIDTH_PER_METER);
  canvas.height = TEXTURE_HEIGHT;
  const context = canvas.getContext('2d');
  if (!context) {
    throw new Error('2D canvas context unavailable');
  }

  context.fillStyle = BOARD_COLOR;
  context.fillRect(0, 0, canvas.width, canvas.height);
  context.fillStyle = TEXT_COLOR;
  context.font = `700 ${FONT_PX}px system-ui, sans-serif`;
  context.textAlign = 'center';
  context.textBaseline = 'middle';
  context.fillText(text, canvas.width / 2, canvas.height / 2 + 4, canvas.width - 24);

  const texture = new CanvasTexture(canvas);
  texture.colorSpace = SRGBColorSpace;
  return texture;
}

export function SignMesh({
  footprint,
  style,
  text,
}: {
  footprint: FootprintWire;
  style: BoxStyle;
  text: string;
}) {
  const { width } = footprint;
  const texture = useMemo(() => drawBoardText(text, width), [text, width]);
  useEffect(() => () => texture.dispose(), [texture]);
  const boardY = style.height / 2 - BOARD_HEIGHT / 2 - 0.05;
  const faceOffset = BOARD_THICKNESS / 2 + 0.002;

  return (
    <group>
      <mesh castShadow receiveShadow>
        <boxGeometry args={[POST_WIDTH, style.height, POST_WIDTH]} />
        <meshStandardMaterial color={style.color} />
      </mesh>
      <mesh castShadow receiveShadow position={[0, boardY, 0]}>
        <boxGeometry args={[width, BOARD_HEIGHT, BOARD_THICKNESS]} />
        <meshStandardMaterial color={BOARD_COLOR} />
      </mesh>
      {[0, Math.PI].map((turn) => (
        <group key={turn} position={[0, boardY, 0]} rotation={[0, turn, 0]}>
          <mesh position={[0, 0, faceOffset]}>
            <planeGeometry args={[width, BOARD_HEIGHT]} />
            <meshBasicMaterial map={texture} toneMapped={false} />
          </mesh>
        </group>
      ))}
    </group>
  );
}
