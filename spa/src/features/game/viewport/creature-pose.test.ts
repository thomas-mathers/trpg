import { Group, PerspectiveCamera, Vector3 } from 'three';
import { describe, expect, it } from 'vitest';

import { creatureAppearance } from './creature-appearance';
import { creatureBodyGeometry } from './creature-body-geometry';
import { resolveGear } from './creature-gear';
import { applyPose, SEATED_POSE, strikeSwing } from './creature-pose';
import { BONE_NAMES, createCreatureRig, JOINTS } from './creature-rig';
import { EYE_HEIGHT } from './layout-math';

const appearance = creatureAppearance('npc', 'Human', 30);
const gear = resolveGear([]);

describe('creature rig', () => {
  it('weights every vertex fully to one bone', () => {
    // Arrange
    const geometry = creatureBodyGeometry(appearance, gear);

    // Act
    const weights = geometry.attributes.skinWeight;
    const indices = geometry.attributes.skinIndex;

    // Assert
    for (let vertex = 0; vertex < weights.count; vertex++) {
      expect(weights.getX(vertex)).toBe(1);
      expect(indices.getX(vertex)).toBeLessThan(BONE_NAMES.length);
    }
  });

  it('leaves the head out of the body geometry', () => {
    // Act
    const whole = creatureBodyGeometry(appearance, gear);
    const body = creatureBodyGeometry(appearance, gear, 'body');

    // Assert
    expect(body.boundingBox!.max.y).toBeLessThan(whole.boundingBox!.max.y - 0.1);
  });

  it('keeps only the head in the shadow-only geometry', () => {
    // Act
    const head = creatureBodyGeometry(appearance, gear, 'head');

    // Assert
    expect(head.boundingBox!.min.y).toBeGreaterThan(1.3);
  });

  it('shares one geometry between creatures that look the same', () => {
    // Act
    const first = creatureBodyGeometry(appearance, gear);
    const second = creatureBodyGeometry(appearance, resolveGear([]));

    // Assert
    expect(second).toBe(first);
  });

  it('puts every bone at its joint in the bind pose', () => {
    // Act
    const rig = createCreatureRig(creatureBodyGeometry(appearance, gear));
    rig.mesh.updateMatrixWorld(true);

    // Assert
    for (const name of BONE_NAMES) {
      const [x, y, z] = JOINTS[name];
      const position = rig.bones[name].getWorldPosition(rig.bones[name].position.clone());
      expect([position.x, position.y, position.z]).toEqual([
        expect.closeTo(x),
        expect.closeTo(y),
        expect.closeTo(z),
      ]);
    }
  });
});

describe('seated pose', () => {
  it('keeps the feet on the floor', () => {
    // Arrange
    const rig = createCreatureRig(creatureBodyGeometry(appearance, gear));

    // Act
    applyPose(rig, { stance: 'seated', gaitPhase: 0, gaitAmount: 0, upperYaw: 0 });
    rig.mesh.updateMatrixWorld(true);

    // Assert
    const ankle = rig.bones.footR.getWorldPosition(rig.bones.footR.position.clone());
    expect(ankle.y).toBeCloseTo(JOINTS.footR[1], 2);
    expect(SEATED_POSE.rootY).toBeLessThan(0);
  });

  it('sends the knees forward', () => {
    // Arrange
    const rig = createCreatureRig(creatureBodyGeometry(appearance, gear));

    // Act
    applyPose(rig, { stance: 'seated', gaitPhase: 0, gaitAmount: 0, upperYaw: 0 });
    rig.mesh.updateMatrixWorld(true);

    // Assert
    const knee = rig.bones.shinR.getWorldPosition(rig.bones.shinR.position.clone());
    expect(knee.z).toBeLessThan(-0.2);
  });
});

describe('looking pose', () => {
  it('holds the hands out in front of the waist so held gear is in view', () => {
    // Arrange
    const rig = createCreatureRig(creatureBodyGeometry(appearance, gear, 'body'));

    // Act
    applyPose(rig, { stance: 'looking', gaitPhase: 0, gaitAmount: 0, upperYaw: 0 });
    rig.mesh.updateMatrixWorld(true);

    // Assert
    const hand = rig.bones.handR.getWorldPosition(rig.bones.handR.position.clone());
    expect(hand.z).toBeLessThan(-0.4);
    expect(hand.y).toBeGreaterThan(1.1);
  });

  it('keeps the held sword and shield inside the first-person view', () => {
    // Arrange
    const rig = createCreatureRig(creatureBodyGeometry(appearance, gear, 'body'));
    const body = new Group();
    body.scale.setScalar((EYE_HEIGHT - 0.07) / JOINTS.head[1]);
    body.position.z = 0.12;
    body.add(rig.mesh);
    const camera = new PerspectiveCamera(75, 16 / 9, 0.1, 100);
    camera.position.y = EYE_HEIGHT;
    camera.updateMatrixWorld(true);

    // Act
    applyPose(rig, { stance: 'looking', gaitPhase: 0, gaitAmount: 0, upperYaw: 0 });
    body.updateMatrixWorld(true);

    // Assert
    const bladeMiddle = new Vector3(0, 0.5, 0).applyMatrix4(rig.bones.handR.matrixWorld);
    const shieldTop = new Vector3()
      .setFromMatrixPosition(rig.bones.handL.matrixWorld)
      .add(new Vector3(0, 0.3, 0));
    for (const point of [bladeMiddle, shieldTop]) {
      const onScreen = point.project(camera);
      expect(Math.abs(onScreen.x)).toBeLessThan(1);
      expect(onScreen.y).toBeGreaterThan(-1);
      expect(onScreen.y).toBeLessThan(0.2);
    }
  });
});

describe('strikeSwing', () => {
  it('is at rest outside the strike', () => {
    expect([strikeSwing(0), strikeSwing(1)]).toEqual([0, 0]);
  });

  it('peaks at the end of the wind-up, then swings past rest and recovers', () => {
    expect(strikeSwing(0.3)).toBeCloseTo(1);
    expect(strikeSwing(0.5)).toBeLessThan(0);
    expect(strikeSwing(0.99)).toBeCloseTo(0, 1);
  });
});
