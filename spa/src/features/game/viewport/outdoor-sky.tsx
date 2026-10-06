import { useFrame } from '@react-three/fiber';
import { useEffect, useMemo } from 'react';
import { SkyMesh } from 'three/addons/objects/SkyMesh.js';
import { color, dot, mix, uniform, vec3, vec4 } from 'three/tsl';

import { useSceneHour } from './scene-hour';
import { NIGHT_SKY_COLOR, skyStateAt, sunDirectionAt } from './sky-state';
import { lightningFlashAt, type WeatherLook } from './weather-look';

const LIGHTNING_SKY_BOOST = 3;

export function createOutdoorSky() {
  const mesh = new SkyMesh();
  mesh.scale.setScalar(1000);
  mesh.frustumCulled = false;
  mesh.sunPosition.value.copy(sunDirectionAt(12));
  mesh.turbidity.value = 3;
  mesh.rayleigh.value = 1.8;
  const daylight = uniform(1);
  const saturation = uniform(1);
  const brightness = uniform(1);
  const night = vec4(color(NIGHT_SKY_COLOR), 1);
  const base = mesh.material.colorNode!.mul(0.05) as unknown as typeof night;
  const luma = dot(base.rgb, vec3(0.2126, 0.7152, 0.0722));
  const day = vec4(mix(vec3(luma), base.rgb, saturation).mul(brightness), base.a);
  mesh.material.colorNode = mix(night, day, daylight);
  mesh.material.allowOverride = false;
  return { mesh, daylight, saturation, brightness };
}

export function OutdoorSky({ look }: { look: WeatherLook }) {
  const hour = useSceneHour();
  const { daylight, sunDirection } = useMemo(() => skyStateAt(hour), [hour]);
  const sky = useMemo(createOutdoorSky, []);

  useEffect(() => {
    sky.mesh.sunPosition.value.copy(sunDirection);
    sky.daylight.value = daylight;
  }, [sky, sunDirection, daylight]);

  useEffect(() => {
    sky.mesh.cloudCoverage.value = look.cloudCoverage;
    sky.mesh.cloudDensity.value = look.cloudDensity;
    sky.saturation.value = look.saturation;
  }, [sky, look]);

  useEffect(
    () => () => {
      sky.mesh.geometry.dispose();
      sky.mesh.material.dispose();
    },
    [sky],
  );

  useFrame(({ camera, clock }) => {
    sky.mesh.position.copy(camera.position);
    const flash = look.lightning ? lightningFlashAt(clock.elapsedTime) : 0;
    sky.brightness.value = look.brightness * (1 + flash * LIGHTNING_SKY_BOOST);
  });

  return <primitive object={sky.mesh} />;
}
