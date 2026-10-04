import { OrbitControls } from '@react-three/drei';
import { Canvas } from '@react-three/fiber';
import type { Meta, StoryObj } from '@storybook/react-vite';

import { DoorConnector } from './door-connector';
import { createWebGpuRenderer } from './webgpu-renderer';

function DoorPreview() {
  return (
    <div style={{ width: '100vw', height: '100vh' }}>
      <Canvas gl={createWebGpuRenderer} camera={{ position: [3, 2.6, 5], fov: 42 }}>
        <color attach="background" args={['#9bb7d4']} />
        <ambientLight intensity={0.8} />
        <directionalLight position={[3, 7, 5]} intensity={1.4} />
        <DoorConnector />
        <mesh rotation={[-Math.PI / 2, 0, 0]}>
          <planeGeometry args={[10, 10]} />
          <meshStandardMaterial color="#454c3b" />
        </mesh>
        <OrbitControls target={[0, 1.2, 0]} />
      </Canvas>
    </div>
  );
}
const meta = {
  title: 'Game/Viewport/Door Connector',
  component: DoorPreview,
  parameters: { layout: 'fullscreen' },
} satisfies Meta<typeof DoorPreview>;
export default meta;
type Story = StoryObj<typeof meta>;
export const TimberDoor: Story = {};
