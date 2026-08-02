import type { SceneSnapshot } from '@/api/client';

export function formatLocation(scene: SceneSnapshot): string {
  const levels = [scene.buildingName, scene.districtName, scene.cityName, scene.stateName].filter(
    (level): level is string => Boolean(level),
  );
  return levels.slice(0, 2).join(', ');
}

export function locationKey(scene: SceneSnapshot): string {
  return [scene.stateName, scene.cityName, scene.districtName, scene.buildingName, scene.roomName]
    .map((level) => level ?? '')
    .join('|');
}
