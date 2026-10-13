import type { CreatureAppearance } from './creature-appearance';
import { solidPart, type BodyPart } from './creature-body-parts';
import {
  creatureHeadGeometry,
  earGeometry,
  helmGeometry,
  hornGeometry,
  unitBoxGeometry,
} from './creature-resources';
import { JOINTS } from './creature-rig';

const ELDER_BEARD = '#cfcfcf';
const BEARD = '#4a3a2c';
const HORN = '#d8d0b8';

const [HEAD_X, HEAD_Y, HEAD_Z] = JOINTS.head;

function featureParts({ feature, skin, elderly }: CreatureAppearance): BodyPart[] {
  if (feature === 'beard') {
    return [
      solidPart('head', unitBoxGeometry, elderly ? ELDER_BEARD : BEARD, {
        position: [HEAD_X, HEAD_Y - 0.15, HEAD_Z - 0.1],
        scale: [0.2, 0.2, 0.1],
      }),
    ];
  }
  if (feature === 'pointed-ears') {
    return [
      solidPart('head', earGeometry, skin, {
        position: [HEAD_X + 0.2, HEAD_Y + 0.03, HEAD_Z],
        rotation: [0, 0, -Math.PI / 2.4],
      }),
      solidPart('head', earGeometry, skin, {
        position: [HEAD_X - 0.2, HEAD_Y + 0.03, HEAD_Z],
        rotation: [0, 0, Math.PI / 2.4],
      }),
    ];
  }
  if (feature === 'horns') {
    return [
      solidPart('head', hornGeometry, HORN, {
        position: [HEAD_X + 0.11, HEAD_Y + 0.19, HEAD_Z],
        rotation: [0, 0, -0.35],
      }),
      solidPart('head', hornGeometry, HORN, {
        position: [HEAD_X - 0.11, HEAD_Y + 0.19, HEAD_Z],
        rotation: [0, 0, 0.35],
      }),
    ];
  }
  return [];
}

export function headParts(appearance: CreatureAppearance, helm: string | undefined): BodyPart[] {
  return [
    solidPart('head', creatureHeadGeometry, appearance.skin, { position: JOINTS.head }),
    ...featureParts(appearance),
    ...(helm
      ? [solidPart('head', helmGeometry, helm, { position: [HEAD_X, HEAD_Y + 0.01, HEAD_Z] })]
      : []),
  ];
}
