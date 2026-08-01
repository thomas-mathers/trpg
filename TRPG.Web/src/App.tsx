import { useEffect } from 'react';

import { Bubble, BubbleContent } from '@/components/ui/bubble';
import { Message, MessageContent } from '@/components/ui/message';
import {
  MessageScroller,
  MessageScrollerButton,
  MessageScrollerContent,
  MessageScrollerItem,
  MessageScrollerProvider,
  MessageScrollerViewport,
} from '@/components/ui/message-scroller';

import { useGameHubConnection } from './hooks/useGameHubConnection';

function App() {
  const { isConnected, streamOpening } = useGameHubConnection();

  console.log(isConnected);

  return <div className="flex h-screen flex-col"></div>;
}

export default App;
