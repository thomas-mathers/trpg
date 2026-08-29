import { GiTiedScroll } from 'react-icons/gi';

import { Button } from '@/components/ui/button';
import {
  DropdownMenuTrigger,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenu,
} from '@/components/ui/dropdown-menu';
import { useScene } from '@/features/game/contexts/scene-context';

interface GameMenuProps {
  onOpenCharacterDialog: () => void;
  onOpenInventoryDialog: () => void;
  onOpenQuestJournal: () => void;
  onOpenSkillTreeDialog: () => void;
  onOpenWaitDialog: () => void;
  onOpenWorldMapDialog: () => void;
  onQuit: () => void;
}

export function GameMenu({
  onOpenCharacterDialog,
  onOpenInventoryDialog,
  onOpenQuestJournal,
  onOpenSkillTreeDialog,
  onOpenWaitDialog,
  onOpenWorldMapDialog,
  onQuit,
}: GameMenuProps) {
  const scene = useScene();

  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <Button variant="ghost" size="icon">
          <GiTiedScroll />
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end">
        <DropdownMenuItem disabled={!scene} onClick={onOpenCharacterDialog}>
          Character
        </DropdownMenuItem>
        <DropdownMenuItem disabled={!scene} onClick={onOpenInventoryDialog}>
          Inventory
        </DropdownMenuItem>
        <DropdownMenuItem disabled={!scene} onClick={onOpenQuestJournal}>
          Quest Journal
        </DropdownMenuItem>
        <DropdownMenuItem disabled={!scene} onClick={onOpenSkillTreeDialog}>
          Skills
        </DropdownMenuItem>
        <DropdownMenuItem disabled={!scene} onClick={onOpenWorldMapDialog}>
          World Map
        </DropdownMenuItem>
        <DropdownMenuItem disabled={!scene} onClick={onOpenWaitDialog}>
          Wait
        </DropdownMenuItem>
        <DropdownMenuSeparator />
        <DropdownMenuItem onClick={onQuit}>Quit</DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  );
}
