import { useQueryClient } from '@tanstack/react-query';
import { useNavigate, useParams } from '@tanstack/react-router';
import { useEffect, useState } from 'react';

import { getQuestJournalQueryKey } from '@/api/client';
import { CharacterDialog } from '@/features/character/components/character-dialog';
import { useHasActiveEncounter } from '@/features/encounters/hooks/use-encounter-state';

import { SidebarInset, SidebarProvider } from '../../../components/ui/sidebar';
import { gameEventBus, type QuestDialogRequested } from '../../../lib/game-event-bus';
import { clearStoredMessages } from '../../../lib/session-storage';
import { InventoryDialog } from '../../inventory/components/inventory-dialog';
import { QuestDialog } from '../../quests/components/quest-dialog';
import { QuestJournalDialog } from '../../quests/components/quest-journal-dialog';
import { SkillTreeDialog } from '../../skills/components/skill-tree-dialog';
import { usePlayerId, useScene } from '../contexts/scene-context';
import { GameChatContext, useGameChatBuilder } from '../hooks/use-game-chat';
import {
  GameHubConnectionContext,
  useConnectToHub,
  useGameHubConnection,
} from '../hooks/use-game-hub-connection';
import { useIsInCombat } from '../hooks/use-is-in-combat';
import { SceneProvider } from '../providers/scene-provider';
import { ConnectionLostDialog } from './connection-lost-dialog';
import { GameChat } from './game-chat';
import { GameMenu } from './game-menu';
import { GameNotifications } from './game-notifications';
import { NearbySidebar } from './nearby-sidebar';
import { NearbyToggleButton } from './nearby-toggle-button';
import { StatusBar } from './status-bar';

type OpenDialog = 'character' | 'inventory' | 'questJournal' | 'skillTree' | null;

function GameScreen() {
  const navigate = useNavigate();
  const { sessionId } = useParams({ from: '/session/$sessionId' });
  const hubConnection = useConnectToHub(sessionId);
  const isInCombat = useIsInCombat();
  const hasActiveEncounter = useHasActiveEncounter();
  const isActionBlocked = isInCombat || hasActiveEncounter;
  const [isNearbyOpen, setIsNearbyOpen] = useState(true);
  const [openDialog, setOpenDialog] = useState<OpenDialog>(null);
  const [isConnectionLostDialogOpen, setIsConnectionLostDialogOpen] = useState(false);

  useEffect(() => {
    setIsNearbyOpen(!isActionBlocked);
  }, [isActionBlocked]);

  useEffect(
    () =>
      gameEventBus.on('ConnectionStatusChanged', (status) => {
        if (status === 'disconnected') {
          setIsConnectionLostDialogOpen(true);
        }
      }),
    [],
  );

  const handleReturnToMenu = () => {
    clearStoredMessages(sessionId);
    setOpenDialog(null);
    navigate({ to: '/' });
  };

  const handleExitToMenu = async () => {
    clearStoredMessages(sessionId);
    if (hubConnection.chatHub) {
      try {
        await hubConnection.chatHub.endSession();
      } catch (e) {
        console.error('Error ending session', e);
      }
    }
    navigate({ to: '/' });
  };

  return (
    <GameHubConnectionContext.Provider value={hubConnection}>
      <SceneProvider key={sessionId} sessionId={sessionId}>
        {hubConnection.chatHub && (
          <GameScreenContent
            sessionId={sessionId}
            isInCombat={isActionBlocked}
            isNearbyOpen={isNearbyOpen}
            openDialog={openDialog}
            isConnectionLostDialogOpen={isConnectionLostDialogOpen}
            onNearbyOpenChange={setIsNearbyOpen}
            onOpenDialog={setOpenDialog}
            onQuit={handleExitToMenu}
            onConnectionLostClose={handleReturnToMenu}
          />
        )}
      </SceneProvider>
    </GameHubConnectionContext.Provider>
  );
}

interface GameScreenContentProps {
  sessionId: string;
  isInCombat: boolean;
  isNearbyOpen: boolean;
  openDialog: OpenDialog;
  isConnectionLostDialogOpen: boolean;
  onNearbyOpenChange: (open: boolean) => void;
  onOpenDialog: (dialog: OpenDialog) => void;
  onQuit: () => void;
  onConnectionLostClose: () => void;
}

function GameScreenContent({
  sessionId,
  isInCombat,
  isNearbyOpen,
  openDialog,
  isConnectionLostDialogOpen,
  onNearbyOpenChange,
  onOpenDialog,
  onQuit,
  onConnectionLostClose,
}: GameScreenContentProps) {
  const { connectionStatus } = useGameHubConnection();
  const gameChat = useGameChatBuilder(sessionId);
  const playerId = usePlayerId();
  const scene = useScene();
  const queryClient = useQueryClient();
  const [questDialog, setQuestDialog] = useState<QuestDialogRequested | null>(null);

  useEffect(() => gameEventBus.on('QuestDialogRequested', setQuestDialog), []);

  useEffect(
    () =>
      gameEventBus.on('ConnectionStatusChanged', (status) => {
        if (status === 'reconnected') {
          void queryClient.invalidateQueries();
        }
      }),
    [queryClient],
  );

  useEffect(
    () =>
      gameEventBus.on('QuestJournalUpdated', () => {
        if (playerId && scene) {
          void queryClient.invalidateQueries({
            queryKey: getQuestJournalQueryKey({
              path: { playerId },
              query: { worldId: scene.worldId },
            }),
          });
        }
      }),
    [playerId, queryClient, scene],
  );

  return (
    <GameChatContext.Provider value={gameChat}>
      <SidebarProvider
        open={isNearbyOpen}
        onOpenChange={onNearbyOpenChange}
        className="h-screen flex-col"
      >
        <div className="border-b px-4 py-2">
          <StatusBar
            connectionStatus={connectionStatus}
            isInCombat={isInCombat}
            controls={
              <>
                {!isInCombat && <NearbyToggleButton />}
                <GameMenu
                  onOpenCharacterDialog={() => onOpenDialog('character')}
                  onOpenInventoryDialog={() => onOpenDialog('inventory')}
                  onOpenQuestJournal={() => onOpenDialog('questJournal')}
                  onOpenSkillTreeDialog={() => onOpenDialog('skillTree')}
                  onQuit={onQuit}
                />
              </>
            }
          />
        </div>

        <div className="relative flex min-h-0 flex-1 overflow-hidden will-change-transform">
          <SidebarInset>
            <GameChat />
          </SidebarInset>

          <NearbySidebar onOpenQuestJournal={() => onOpenDialog('questJournal')} />
        </div>

        {playerId && (
          <>
            <CharacterDialog open={openDialog === 'character'} onClose={() => onOpenDialog(null)} />
            <InventoryDialog
              playerId={playerId}
              open={openDialog === 'inventory'}
              onClose={() => onOpenDialog(null)}
            />
            <SkillTreeDialog
              playerId={playerId}
              open={openDialog === 'skillTree'}
              onClose={() => onOpenDialog(null)}
            />
            <QuestDialog
              playerId={playerId}
              quest={questDialog}
              onClose={() => setQuestDialog(null)}
            />
            {scene && (
              <QuestJournalDialog
                playerId={playerId}
                worldId={scene.worldId}
                open={openDialog === 'questJournal'}
                onClose={() => onOpenDialog(null)}
              />
            )}
          </>
        )}

        <ConnectionLostDialog open={isConnectionLostDialogOpen} onClose={onConnectionLostClose} />
        <GameNotifications />
      </SidebarProvider>
    </GameChatContext.Provider>
  );
}

export default GameScreen;
