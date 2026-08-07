import { MenuIcon } from 'lucide-react';

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
  onOpenSkillTreeDialog: () => void;
  onQuit: () => void;
}

export function GameMenu({
  onOpenCharacterDialog,
  onOpenInventoryDialog,
  onOpenSkillTreeDialog,
  onQuit,
}: GameMenuProps) {
  const scene = useScene();

  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <Button variant="ghost" size="icon" className="ml-auto">
          <MenuIcon />
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end">
        <DropdownMenuItem disabled={!scene} onClick={onOpenCharacterDialog}>
          Character
        </DropdownMenuItem>
        <DropdownMenuItem disabled={!scene} onClick={onOpenInventoryDialog}>
          Inventory
        </DropdownMenuItem>
        <DropdownMenuItem disabled={!scene} onClick={onOpenSkillTreeDialog}>
          Skills
        </DropdownMenuItem>
        <DropdownMenuSeparator />
        <DropdownMenuItem onClick={onQuit}>Quit</DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  );
}
