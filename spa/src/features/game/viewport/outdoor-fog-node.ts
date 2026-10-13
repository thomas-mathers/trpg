import { Color, Vector3 } from 'three';
import {
  cameraPosition,
  dot,
  float,
  fog,
  mix,
  normalize,
  positionWorld,
  pow,
  rangeFogFactor,
  uniform,
} from 'three/tsl';

const MIST_FALLOFF = 0.15;
const SCATTER_TIGHTNESS = 6;

export function createOutdoorFog() {
  const color = uniform(new Color());
  const sunTint = uniform(new Color());
  const sunDirection = uniform(new Vector3(0, 1, 0));
  const near = uniform(0);
  const far = uniform(1);
  const mist = uniform(0);
  const scatter = uniform(0);

  const toFragment = positionWorld.sub(cameraPosition);
  const distance = toFragment.length();
  const averageHeight = cameraPosition.y.add(positionWorld.y).mul(0.5).max(0);
  const mistWeight = averageHeight.mul(-MIST_FALLOFF).exp();
  const mistFactor = float(1).sub(mist.mul(distance).mul(mistWeight).negate().exp());
  const rangeFactor = rangeFogFactor(near, far);
  const factor = float(1).sub(float(1).sub(rangeFactor).mul(float(1).sub(mistFactor)));

  const towardSun = dot(normalize(toFragment), sunDirection).max(0);
  const glow = pow(towardSun, SCATTER_TIGHTNESS).mul(scatter);
  const tinted = mix(color, sunTint, glow);

  return { node: fog(tinted, factor), color, sunTint, sunDirection, near, far, mist, scatter };
}
