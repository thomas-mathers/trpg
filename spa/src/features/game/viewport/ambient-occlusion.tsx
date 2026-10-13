import { useFrame, useThree } from '@react-three/fiber';
import { useEffect, useMemo } from 'react';
import { denoise } from 'three/addons/tsl/display/DenoiseNode.js';
import { fxaa } from 'three/addons/tsl/display/FXAANode.js';
import { ao } from 'three/addons/tsl/display/GTAONode.js';
import { mrt, nodeObject, normalView, output, pass, vec4 } from 'three/tsl';
import { type Camera, RenderPipeline, type Scene, type WebGPURenderer } from 'three/webgpu';

const AO_RADIUS = 1;
const AO_SCALE = 2;
const AO_SAMPLES = 16;
const DENOISE_RADIUS = 9;
const AO_RESOLUTION = 1;
const POST_PROCESS_PRIORITY = 1;

function createAoPipeline(renderer: WebGPURenderer, scene: Scene, camera: Camera) {
  const scenePass = pass(scene, camera, { samples: 0 });
  scenePass.setMRT(mrt({ output, normal: normalView }));
  const color = scenePass.getTextureNode('output');
  const depth = scenePass.getTextureNode('depth');
  const normal = scenePass.getTextureNode('normal');

  const occlusion = ao(depth, normal, camera);
  occlusion.radius.value = AO_RADIUS;
  occlusion.scale.value = AO_SCALE;
  occlusion.samples.value = AO_SAMPLES;
  occlusion.resolutionScale = AO_RESOLUTION;
  const denoised = denoise(occlusion.getTextureNode(), depth, normal, camera);
  denoised.radius.value = DENOISE_RADIUS;
  const smooth = nodeObject(denoised);

  const pipeline = new RenderPipeline(renderer);
  pipeline.outputNode = fxaa(vec4(color.rgb.mul(smooth.r), color.a));
  return pipeline;
}

export function AmbientOcclusion() {
  const gl = useThree((state) => state.gl);
  const scene = useThree((state) => state.scene);
  const camera = useThree((state) => state.camera);
  const pipeline = useMemo(
    () => createAoPipeline(gl as unknown as WebGPURenderer, scene, camera),
    [gl, scene, camera],
  );
  useEffect(() => () => pipeline.dispose(), [pipeline]);

  useFrame(() => pipeline.render(), POST_PROCESS_PRIORITY);
  return null;
}
