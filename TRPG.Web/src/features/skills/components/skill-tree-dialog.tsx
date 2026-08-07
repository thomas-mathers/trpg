import { useQuery } from '@tanstack/react-query';
import { ReactFlowProvider } from '@xyflow/react';
import { useState } from 'react';

import type { Skill } from '@/api/client';
import { getAbilitiesBySkillOptions, getCreatureSkillsOptions } from '@/api/client';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { SkillTree } from '@/features/skills/components/skill-tree';

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

interface SkillTreeDialogProps {
  playerId: string;
  open: boolean;
  onClose: () => void;
}

export function SkillTreeDialog({ playerId, open, onClose }: SkillTreeDialogProps) {
  return (
    <Dialog open={open} onOpenChange={(next) => !next && onClose()}>
      <DialogContent
        className="flex h-[min(94vh,880px)] flex-col gap-4 md:max-w-7xl"
        onPointerDownOutside={(event) => event.preventDefault()}
      >
        <ReactFlowProvider>
          <SkillTreeDialogBody playerId={playerId} onClose={onClose} />
        </ReactFlowProvider>
      </DialogContent>
    </Dialog>
  );
}

function SkillTreeDialogBody({ playerId, onClose }: { playerId: string; onClose: () => void }) {
  const [activeSkill, setActiveSkill] = useState<Skill>('Melee');

  const skillLevels = useQuery(getCreatureSkillsOptions({ path: { creatureId: playerId } }));

  const tree = useQuery(getAbilitiesBySkillOptions({ path: { skill: activeSkill } }));

  if (!skillLevels.data) {
    return (
      <div className="flex flex-1 items-center justify-center py-12">
        <p className="text-muted-foreground text-sm">Loading abilities...</p>
      </div>
    );
  }

  const activeProgress = skillLevels.data.find((progress) => progress.skill === activeSkill);
  const currentLevel = Number(activeProgress?.level ?? 0);
  const experienceCurrent = Number(activeProgress?.experienceCurrent ?? 0);
  const experienceToNextLevel = Number(activeProgress?.experienceToNextLevel ?? 0);
  const experiencePercent =
    experienceToNextLevel > 0
      ? Math.max(0, Math.min(100, Math.round((experienceCurrent / experienceToNextLevel) * 100)))
      : 0;

  return (
    <>
      <DialogHeader>
        <DialogTitle>Skills</DialogTitle>
      </DialogHeader>

      <Tabs
        value={activeSkill}
        onValueChange={(value) => setActiveSkill(value as Skill)}
        className="flex min-h-0 flex-1 flex-col gap-3"
      >
        <TabsList>
          {SKILL_ORDER.map((skill) => (
            <TabsTrigger key={skill} value={skill}>
              {skill}
            </TabsTrigger>
          ))}
        </TabsList>

        <TabsContent
          value={activeSkill}
          className="flex min-h-0 flex-1 flex-col gap-3 overflow-hidden"
        >
          <div className="flex items-center gap-2 text-sm">
            <span className="font-medium">Level {currentLevel}</span>
            <span className="bg-muted h-[6px] flex-1 overflow-hidden rounded-full">
              <span
                className="bg-primary block h-full rounded-full transition-[width] duration-300 ease-out"
                style={{ width: `${experiencePercent}%` }}
              />
            </span>
            <span className="text-muted-foreground w-24 shrink-0 text-right text-xs tabular-nums">
              {experienceCurrent}/{experienceToNextLevel} XP
            </span>
          </div>

          {!tree.data ? (
            <div className="flex h-full items-center justify-center">
              <p className="text-muted-foreground text-sm">Loading tree...</p>
            </div>
          ) : (
            <SkillTree level={currentLevel} abilities={tree.data} />
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
