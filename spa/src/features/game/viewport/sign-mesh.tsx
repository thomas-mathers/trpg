import { useEffect, useMemo } from 'react';
import { CanvasTexture, SRGBColorSpace } from 'three';

import type { FootprintWire } from '@/api/signalr-client/TRPG.GameSessions.Responses';

import { LINE_HEIGHT, layoutBoardText } from './board-text';
import type { BoxStyle } from './model-styles';

const BOARD_HEIGHT = 0.5;
const BOARD_THICKNESS = 0.08;
const POST_WIDTH = 0.1;
const POST_DEPTH = 0.06;
const BOARD_COLOR = '#b08c5a';
const TEXT_COLOR = '#2b1d0e';
const PX_PER_METER = 256;
const TEXT_PADDING_PX = 12;
const FACE_OFFSET = 0.002;

function drawBoardText(text: string, width: number, height: number): CanvasTexture {
  const canvas = document.createElement('canvas');
  canvas.width = Math.ceil(width * PX_PER_METER);
  canvas.height = Math.ceil(height * PX_PER_METER);
  const context = canvas.getContext('2d');
  if (!context) {
    throw new Error('2D canvas context unavailable');
  }

  const fontFor = (size: number) => `700 ${size}px system-ui, sans-serif`;
  const { lines, fontSize } = layoutBoardText(
    text,
    canvas.width - TEXT_PADDING_PX * 2,
    canvas.height - TEXT_PADDING_PX * 2,
    (line, size) => {
      context.font = fontFor(size);
      return context.measureText(line).width;
    },
  );

  context.fillStyle = BOARD_COLOR;
  context.fillRect(0, 0, canvas.width, canvas.height);
  context.fillStyle = TEXT_COLOR;
  context.font = fontFor(fontSize);
  context.textAlign = 'center';
  context.textBaseline = 'middle';
  lines.forEach((line, index) => {
    const offset = (index - (lines.length - 1) / 2) * fontSize * LINE_HEIGHT;
    context.fillText(line, canvas.width / 2, canvas.height / 2 + offset);
  });

  const texture = new CanvasTexture(canvas);
  texture.colorSpace = SRGBColorSpace;
  return texture;
}

function useBoardTexture(text: string, width: number, height: number): CanvasTexture {
  const texture = useMemo(() => drawBoardText(text, width, height), [text, width, height]);
  useEffect(() => () => texture.dispose(), [texture]);
  return texture;
}

function BoardFace({
  texture,
  width,
  height,
}: {
  texture: CanvasTexture;
  width: number;
  height: number;
}) {
  return (
    <mesh>
      <planeGeometry args={[width, height]} />
      <meshBasicMaterial map={texture} toneMapped={false} />
    </mesh>
  );
}

export function NameBoard({
  text,
  width,
  height,
}: {
  text: string;
  width: number;
  height: number;
}) {
  const texture = useBoardTexture(text, width, height);

  return (
    <group position={[0, 0, BOARD_THICKNESS / 2]}>
      <mesh castShadow receiveShadow>
        <boxGeometry args={[width, height, BOARD_THICKNESS]} />
        <meshStandardMaterial color={BOARD_COLOR} />
      </mesh>
      <group position={[0, 0, BOARD_THICKNESS / 2 + FACE_OFFSET]}>
        <BoardFace texture={texture} width={width} height={height} />
      </group>
    </group>
  );
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
  const texture = useBoardTexture(text, width, BOARD_HEIGHT);
  const boardY = style.height / 2 - BOARD_HEIGHT / 2 - 0.05;
  const faceOffset = BOARD_THICKNESS / 2 + FACE_OFFSET;

  return (
    <group>
      <mesh castShadow receiveShadow>
        <boxGeometry args={[POST_WIDTH, style.height, POST_DEPTH]} />
        <meshStandardMaterial color={style.color} />
      </mesh>
      <mesh castShadow receiveShadow position={[0, boardY, 0]}>
        <boxGeometry args={[width, BOARD_HEIGHT, BOARD_THICKNESS]} />
        <meshStandardMaterial color={BOARD_COLOR} />
      </mesh>
      {[0, Math.PI].map((turn) => (
        <group key={turn} position={[0, boardY, 0]} rotation={[0, turn, 0]}>
          <group position={[0, 0, faceOffset]}>
            <BoardFace texture={texture} width={width} height={BOARD_HEIGHT} />
          </group>
        </group>
      ))}
    </group>
  );
}
