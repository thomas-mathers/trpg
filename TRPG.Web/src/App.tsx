import { Button } from '@/components/ui/button'
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from '@/components/ui/dialog'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { Input } from '@/components/ui/input'
import { ScrollArea } from '@/components/ui/scroll-area'

function App() {
  return (
    <div className="flex min-h-svh items-center justify-center">
      <Dialog>
        <DialogTrigger asChild>
          <Button>Open character</Button>
        </DialogTrigger>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Character</DialogTitle>
          </DialogHeader>
          <Tabs defaultValue="inventory">
            <TabsList>
              <TabsTrigger value="inventory">Inventory</TabsTrigger>
              <TabsTrigger value="abilities">Abilities</TabsTrigger>
            </TabsList>
            <TabsContent value="inventory">
              <ScrollArea className="h-48 rounded-md border p-2">
                {Array.from({ length: 20 }, (_, i) => (
                  <div key={i} className="py-1 text-sm">
                    Item {i + 1}
                  </div>
                ))}
              </ScrollArea>
            </TabsContent>
            <TabsContent value="abilities">
              <Input placeholder="Filter abilities..." />
            </TabsContent>
          </Tabs>
        </DialogContent>
      </Dialog>
    </div>
  )
}

export default App
