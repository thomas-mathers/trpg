import { MenuIcon } from 'lucide-react';

import { Button } from '@/components/ui/button';
import {
  DropdownMenuTrigger,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenu,
} from '@/components/ui/dropdown-menu';

interface GameMenuProps {
  hasSceneData: boolean;
  onOpenInventory: () => void;
  onOpenSkills: () => void;
  onQuit: () => void;
}

export function GameMenu({ hasSceneData, onOpenInventory, onOpenSkills, onQuit }: GameMenuProps) {
  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <Button variant="ghost" size="icon" className="ml-auto">
          <MenuIcon />
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end">
        <DropdownMenuItem disabled>Character</DropdownMenuItem>
        <DropdownMenuItem disabled={!hasSceneData} onClick={onOpenInventory}>
          Inventory
        </DropdownMenuItem>
        <DropdownMenuItem disabled={!hasSceneData} onClick={onOpenSkills}>
          Skills
        </DropdownMenuItem>
        <DropdownMenuSeparator />
        <DropdownMenuItem onClick={onQuit}>Quit</DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  );
}
