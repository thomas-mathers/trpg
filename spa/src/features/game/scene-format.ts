import type { SceneSnapshot } from '@/api/signalr-client/TRPG.GameSessions.Responses';

// Named from the inside out, so scrolling back through a dungeon shows which room each description
// belongs to rather than the same building name over and over.
export function formatLocation(scene: SceneSnapshot): string {
  const levels = [
    scene.roomName,
    scene.buildingName,
    scene.districtName,
    scene.cityName,
    scene.stateName,
  ].filter((level): level is string => Boolean(level));
  return levels.slice(0, 2).join(', ');
}

export function locationKey(scene: SceneSnapshot): string {
  return [scene.stateName, scene.cityName, scene.districtName, scene.buildingName, scene.roomName]
    .map((level) => level ?? '')
    .join('|');
}
