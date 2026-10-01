import { OrbitControls } from '@react-three/drei';
import { Canvas } from '@react-three/fiber';
import type { Meta, StoryObj } from '@storybook/react-vite';

import type { PropLayoutWire } from '@/api/signalr-client/TRPG.GameSessions.Responses';

import { Boxes, Creatures, Ground } from './viewport-scene';

const chairs: PropLayoutWire[] = [
  {
    id: 'chair',
    model: 'SeatChair',
    placement: { x: 2, y: 3, angle: 0 },
    footprint: { width: 0.7, depth: 0.7 },
  },
  {
    id: 'bench',
    model: 'SeatBench',
    placement: { x: 4, y: 3, angle: 0 },
    footprint: { width: 1.8, depth: 0.7 },
  },
];
function SeatingPreview() {
  return (
    <div style={{ width: '100vw', height: '100vh' }}>
      <Canvas camera={{ position: [7, 4, -10], fov: 50 }}>
        <color attach="background" args={['#9bb7d4']} />
        <ambientLight intensity={0.8} />
        <directionalLight position={[5, 8, -4]} intensity={1.2} />
        <Ground size={{ width: 8, depth: 6 }} />
        <Boxes props={chairs} buildings={[]} names={new Map()} />
        <Creatures
          creatures={[
            { id: 'npc', placement: chairs[0].placement },
            { id: 'player', placement: chairs[1].placement },
            { id: 'standing', placement: { x: 6, y: 3, angle: 0 } },
          ]}
          playerId="viewer"
          names={
            new Map([
              ['npc', 'Sitting'],
              ['standing', 'Standing'],
            ])
          }
          statuses={[
            { id: 'npc', posture: 'Sitting' },
            { id: 'player', posture: 'Sitting' },
            { id: 'standing', posture: 'Standing' },
          ]}
        />
        <OrbitControls target={[4, 0.8, 3]} />
      </Canvas>
    </div>
  );
}
const meta = {
  title: 'Game/Viewport/Seating',
  component: SeatingPreview,
  parameters: { layout: 'fullscreen' },
} satisfies Meta<typeof SeatingPreview>;
export default meta;
type Story = StoryObj<typeof meta>;
export const SeatedAndStanding: Story = {};
