import { useFrame } from '@react-three/fiber';
import { useState } from 'react';

import type { WeatherLook } from './weather-look';
import { stepLook } from './weather-transition';

const MAX_FRAME_SECONDS = 0.1;

export function useSmoothedLook(target: WeatherLook) {
  const [look, setLook] = useState(target);
  useFrame((_, delta) => {
    if (look === target) return;
    setLook(stepLook(look, target, Math.min(delta, MAX_FRAME_SECONDS)));
  });
  return look;
}
