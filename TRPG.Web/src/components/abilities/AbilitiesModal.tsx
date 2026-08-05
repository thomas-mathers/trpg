import { useQuery } from '@tanstack/react-query';
import { useState } from 'react';

import type { Skill } from '@/api/client';
import {
  getAbilitiesBySkillOptions,
  getCreaturesByCreatureIdAbilitiesOptions,
  getCreaturesByCreatureIdSkillsOptions,
} from '@/api/client';
import { SkillTree } from '@/components/abilities/SkillTree';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';

const SKILL_ORDER: Skill[] = [
  'Melee',
  'Blocking',
  'Sneak',
  'Destruction',
  'Illusion',
  'Archery',
  'Restoration',
  'Alteration',
  'General',
  'Unarmed',
];

interface AbilitiesModalProps {
  playerId: string;
  open: boolean;
  onClose: () => void;
}

export function AbilitiesModal({ playerId, open, onClose }: AbilitiesModalProps) {
  return (
    <Dialog open={open} onOpenChange={(next) => !next && onClose()}>
      <DialogContent
        className="flex h-[min(94vh,880px)] flex-col gap-4 md:max-w-4xl"
        onPointerDownOutside={(event) => event.preventDefault()}
      >
        <AbilitiesModalBody playerId={playerId} onClose={onClose} />
      </DialogContent>
    </Dialog>
  );
}

function AbilitiesModalBody({ playerId, onClose }: { playerId: string; onClose: () => void }) {
  const [activeSkill, setActiveSkill] = useState<Skill>('Melee');

  const knownAbilities = useQuery(
    getCreaturesByCreatureIdAbilitiesOptions({ path: { creatureId: playerId } }),
  );
  const skillLevels = useQuery(
    getCreaturesByCreatureIdSkillsOptions({ path: { creatureId: playerId } }),
  );
  const tree = useQuery(getAbilitiesBySkillOptions({ path: { skill: activeSkill } }));

  if (!knownAbilities.data || !skillLevels.data) {
    return (
      <div className="flex flex-1 items-center justify-center py-12">
        <p className="text-muted-foreground text-sm">Loading abilities...</p>
      </div>
    );
  }

  const knownAbilityNames = new Set(knownAbilities.data.map((ability) => ability.name));
  const currentLevel =
    skillLevels.data.find((progress) => progress.skill === activeSkill)?.level ?? 0;

  return (
    <>
      <DialogHeader>
        <DialogTitle>Abilities</DialogTitle>
      </DialogHeader>

      <Tabs
        value={activeSkill}
        onValueChange={(value) => setActiveSkill(value as Skill)}
        className="flex min-h-0 flex-1 flex-col gap-3"
      >
        <div className="overflow-x-auto">
          <TabsList>
            {SKILL_ORDER.map((skill) => (
              <TabsTrigger key={skill} value={skill}>
                {skill}
              </TabsTrigger>
            ))}
          </TabsList>
        </div>

        <TabsContent value={activeSkill} className="min-h-0 flex-1 overflow-hidden">
          {!tree.data ? (
            <div className="flex h-full items-center justify-center">
              <p className="text-muted-foreground text-sm">Loading tree...</p>
            </div>
          ) : (
            <SkillTree
              abilities={tree.data}
              knownAbilityNames={knownAbilityNames}
              currentLevel={Number(currentLevel)}
            />
          )}
        </TabsContent>
      </Tabs>

      <DialogFooter>
        <Button variant="outline" onClick={onClose}>
          Close
        </Button>
      </DialogFooter>
    </>
  );
}
