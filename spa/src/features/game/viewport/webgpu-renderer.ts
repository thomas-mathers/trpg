import { WebGPURenderer } from 'three/webgpu';

export async function createWebGpuRenderer({ canvas }: { canvas: EventTarget }) {
  const renderer = new WebGPURenderer({ canvas: canvas as HTMLCanvasElement, antialias: true });
  await renderer.init();
  return renderer;
}
