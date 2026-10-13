import { createRoot, extend } from '@react-three/fiber';
import { act } from '@testing-library/react';
import * as THREE from 'three';
import { describe, expect, it } from 'vitest';

import { creatureAppearance } from './creature-appearance';
import { creatureBodyGeometry } from './creature-body-geometry';
import { resolveGear } from './creature-gear';
import { CreatureHeldGear } from './creature-held-gear';
import { createCreatureRig } from './creature-rig';

extend(THREE as never);
(globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

const appearance = creatureAppearance('npc', 'Human', 30);
const gear = resolveGear([{ itemId: 'sword', slot: 'RightHand', modelClass: 'Sword' }]);

describe('held gear', () => {
  it('moves to the new rig when the rig is replaced', async () => {
    // Arrange
    const first = createCreatureRig(creatureBodyGeometry(appearance, gear));
    const second = createCreatureRig(creatureBodyGeometry(appearance, gear));
    const root = createRoot(document.createElement('canvas'));
    const configure = () =>
      root.configure({ gl: (() => ({ render() {}, setSize() {}, setPixelRatio() {} })) as never });
    await act(async () => {
      await configure();
      root.render(<CreatureHeldGear rig={first} gear={gear} />);
    });

    expect(first.bones.handR.children.length).toBeGreaterThan(0);

    // Act
    await act(async () => {
      root.render(<CreatureHeldGear rig={second} gear={gear} />);
    });

    // Assert
    expect(second.bones.handR.children.length).toBeGreaterThan(0);
  });
});
