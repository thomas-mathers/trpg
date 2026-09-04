import { useMutation } from '@tanstack/react-query';
import { useEffect, useState } from 'react';
import { GiHoodedFigure } from 'react-icons/gi';

import { setCreatureSneakingMutation } from '@/api/client';
import { Toggle } from '@/components/ui/toggle';
import { useScene } from '@/features/game/contexts/scene-context';
import { cn } from '@/lib/utils';

export function SneakToggleButton() {
  const scene = useScene();
  const setSneaking = useMutation(setCreatureSneakingMutation());
  const [isSneaking, setIsSneaking] = useState(scene?.playerStatus.isSneaking ?? false);

  useEffect(() => {
    setIsSneaking(scene?.playerStatus.isSneaking ?? false);
  }, [scene?.playerStatus.isSneaking]);

  if (!scene) {
    return null;
  }

  const handlePressedChange = (pressed: boolean) => {
    setIsSneaking(pressed);
    setSneaking.mutate({
      path: { creatureId: scene.playerStatus.id },
      body: { isSneaking: pressed },
    });
  };

  return (
    <Toggle
      size="default"
      className="size-8 p-0"
      pressed={isSneaking}
      onPressedChange={handlePressedChange}
      aria-label="Sneak"
    >
      <GiHoodedFigure className={cn(isSneaking ? 'text-stamina' : 'text-muted-foreground')} />
    </Toggle>
  );
}
