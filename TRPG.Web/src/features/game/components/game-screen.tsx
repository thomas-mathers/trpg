import { useNavigate, useParams } from '@tanstack/react-router';
import { useEffect, useState } from 'react';

import { SidebarInset, SidebarProvider } from '../../../components/ui/sidebar';
import { clearStoredMessages } from '../../../lib/session-storage';
import { InventoryDialog } from '../../inventory/components/inventory-dialog';
import { SkillTreeDialog } from '../../skills/components/skill-tree-dialog';
import { GameChatContext } from '../game-chat-context';
import { useGameChat } from '../hooks/use-game-chat';
import { useIsInCombat } from '../hooks/use-is-in-combat';
import { useSceneQuery } from '../hooks/use-scene-query';
import { ConnectionLostDialog } from './connection-lost-dialog';
import { GameChat } from './game-chat';
import { GameMenu } from './game-menu';
import { NearbySidebar } from './nearby-sidebar';
import { NearbyToggleButton } from './nearby-toggle-button';
import { StatusBar } from './status-bar';

function GameScreen() {
  const navigate = useNavigate();
  const { sessionId } = useParams({ from: '/session/$sessionId' });
  const gameChat = useGameChat(sessionId);
  const sceneQuery = useSceneQuery(sessionId);
  const isInCombat = useIsInCombat();
  const [isNearbyOpen, setIsNearbyOpen] = useState(true);
  const [isInventoryDialogOpen, setIsInventoryDialogOpen] = useState(false);
  const [isSkillTreeDialogOpen, setIsSkillTreeDialogOpen] = useState(false);
  const [isDisconnectedDialogOpen, setIsDisconnectedDialogOpen] = useState(false);

  useEffect(() => {
    setIsNearbyOpen(!isInCombat);
  }, [isInCombat]);

  const handleReturnToMenu = () => {
    clearStoredMessages(sessionId);
    setIsDisconnectedDialogOpen(false);
    navigate({ to: '/' });
  };

  const handleExitToMenu = async () => {
    clearStoredMessages(sessionId);
    await gameChat.endSession();
    navigate({ to: '/' });
  };

  return (
    <GameChatContext.Provider value={gameChat}>
      <SidebarProvider
        open={isNearbyOpen}
        onOpenChange={setIsNearbyOpen}
        className="h-screen flex-col"
      >
        <div className="flex items-center gap-4 border-b px-4 py-2">
          <StatusBar sessionId={sessionId} isInCombat={isInCombat} />
          {!isInCombat && <NearbyToggleButton />}
          <GameMenu
            hasSceneData={Boolean(sceneQuery.data)}
            onOpenInventory={() => setIsInventoryDialogOpen(true)}
            onOpenSkills={() => setIsSkillTreeDialogOpen(true)}
            onQuit={handleExitToMenu}
          />
        </div>

        <div className="relative flex min-h-0 flex-1 overflow-hidden will-change-transform">
          <SidebarInset>
            <GameChat sessionId={sessionId} />
          </SidebarInset>

          <NearbySidebar sessionId={sessionId} />
        </div>

        {sceneQuery.data && (
          <InventoryDialog
            playerId={sceneQuery.data.playerStatus.id}
            open={isInventoryDialogOpen}
            onClose={() => setIsInventoryDialogOpen(false)}
          />
        )}

        {sceneQuery.data && (
          <SkillTreeDialog
            playerId={sceneQuery.data.playerStatus.id}
            open={isSkillTreeDialogOpen}
            onClose={() => setIsSkillTreeDialogOpen(false)}
          />
        )}

        <ConnectionLostDialog open={isDisconnectedDialogOpen} onClose={handleReturnToMenu} />
      </SidebarProvider>
    </GameChatContext.Provider>
  );
}

export default GameScreen;
