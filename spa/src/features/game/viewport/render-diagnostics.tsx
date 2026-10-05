import { useEffect, useState, type RefObject } from 'react';
import { DoubleSide, MeshBasicMaterial, type Scene, type WebGPURenderer } from 'three/webgpu';

interface RenderMetrics {
  fps: number;
  frameMs: number;
  drawCalls: number;
  triangles: number;
  passes: number;
  geometries: number;
  textures: number;
  memoryMiB: number;
}

function readMetrics(renderer: WebGPURenderer, fps: number): RenderMetrics {
  const { render, memory } = renderer.info;
  return {
    fps,
    frameMs: 1000 / fps,
    drawCalls: render.drawCalls,
    triangles: render.triangles,
    passes: render.frameCalls,
    geometries: memory.geometries,
    textures: memory.textures,
    memoryMiB: memory.total / (1024 * 1024),
  };
}

function useRenderMetrics(rendererRef: RefObject<WebGPURenderer | null>, visible: boolean) {
  const [metrics, setMetrics] = useState<RenderMetrics>();
  useEffect(() => {
    if (!visible) return;

    let frame = 0;
    let count = 0;
    let start = performance.now();
    const sample = (now: number) => {
      count++;
      const elapsed = now - start;
      const renderer = rendererRef.current;
      if (elapsed >= 500 && renderer) {
        setMetrics(readMetrics(renderer, (count * 1000) / elapsed));
        count = 0;
        start = now;
      }
      frame = requestAnimationFrame(sample);
    };
    frame = requestAnimationFrame(sample);
    return () => cancelAnimationFrame(frame);
  }, [visible, rendererRef]);
  return metrics;
}

function MetricsPanel({ metrics }: { metrics: RenderMetrics | undefined }) {
  if (!metrics) return <span>Measuring…</span>;
  const rows = [
    ['FPS', metrics.fps.toFixed(0)],
    ['Frame interval', `${metrics.frameMs.toFixed(1)} ms`],
    ['Draw calls', metrics.drawCalls.toLocaleString()],
    ['Triangles', metrics.triangles.toLocaleString()],
    ['Render calls', metrics.passes.toLocaleString()],
    ['Geometries', metrics.geometries.toLocaleString()],
    ['Textures', metrics.textures.toLocaleString()],
    ['Tracked GPU', `${metrics.memoryMiB.toFixed(1)} MiB`],
  ];
  return (
    <dl className="grid grid-cols-2 gap-x-4 gap-y-1">
      {rows.map(([label, value]) => (
        <div key={label} className="col-span-2 grid grid-cols-subgrid">
          <dt>{label}</dt>
          <dd className="text-right">{value}</dd>
        </div>
      ))}
    </dl>
  );
}

export function RenderDiagnostics({
  rendererRef,
  scene,
}: {
  rendererRef: RefObject<WebGPURenderer | null>;
  scene: Scene | null;
}) {
  const [visible, setVisible] = useState(false);
  const [wireframe, setWireframe] = useState(false);
  const metrics = useRenderMetrics(rendererRef, visible);

  useEffect(() => {
    const toggle = (event: KeyboardEvent) => {
      if (event.repeat || (event.key !== 'F3' && event.key !== 'F4')) return;
      event.preventDefault();
      if (event.key === 'F3') setVisible((current) => !current);
      if (event.key === 'F4') setWireframe((current) => !current);
    };
    window.addEventListener('keydown', toggle);
    return () => window.removeEventListener('keydown', toggle);
  }, []);

  useEffect(() => {
    if (!scene || !wireframe) return;
    const previous = scene.overrideMaterial;
    const material = new MeshBasicMaterial({ color: '#b9f8ff', wireframe: true, side: DoubleSide });
    scene.overrideMaterial = material;
    return () => {
      scene.overrideMaterial = previous;
      material.dispose();
    };
  }, [scene, wireframe]);

  return (
    <div className="absolute right-3 bottom-3 z-20 flex flex-col items-end gap-2 text-xs">
      {visible && (
        <div className="pointer-events-none rounded bg-black/80 p-3 font-mono text-white shadow-lg">
          <div className="mb-2 font-semibold">Rendering</div>
          <MetricsPanel metrics={metrics} />
        </div>
      )}
      <div className="flex gap-2">
        <button
          type="button"
          aria-pressed={wireframe}
          onClick={() => setWireframe((current) => !current)}
          className="rounded bg-black/70 px-2 py-1 font-medium text-white hover:bg-black/90"
        >
          {wireframe ? 'Solid · F4' : 'Wireframe · F4'}
        </button>
        <button
          type="button"
          aria-pressed={visible}
          onClick={() => setVisible((current) => !current)}
          className="rounded bg-black/70 px-2 py-1 font-medium text-white hover:bg-black/90"
        >
          {visible ? 'Hide stats' : 'Stats · F3'}
        </button>
      </div>
    </div>
  );
}
