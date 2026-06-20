/*
data model:
Person
- id: uuid
- name: string
- age: int
- raceId: uuid
- birthCityId: uuid
- professionId: uuid
- biography: String
- location: Location
- level: int
- experience: Meter
- stats: Stats
- gold: int

Stats
- hp: Meter
- ap: Meter
- strength: int
- defense: int
- dexterity: int
- endurance: int
- intelligence: int

Meter
- current: int
- maximum: int

Item
- id: uuid
- name: string
- description: string
- activeEffectIds: List<uuid>
- passiveEffectIds: List<uuid>
- category: ItemCategory
- isStackable: boolean
- weight: int
- value: int

InventoryItem
- id: uuid
- personId: uuid
- itemId: uuid
- quantity: int
- index: int
- equippedSlot: EquipmentSlot?

Skill
- id: uuid
- name: string
- activeEffectIds: List<uuid>
- passiveEffectIds: List<uuid>
- apCost: int
- cooldownTurns: int


Effect
- id: uuid
- name: string
- description: string
- applicationMode: origin | target
- stat: EffectStat
- type: flat | percent
- value: float
- duration: int?


NPC conversations
- id: uuid
- playerId: uuid
- npcId: uuid
- summary: string
- lastMessageSummarized: int

NPC chat messages
- conversationId: uuid
- index: int
- from: uuid
- to: uuid
- message: string
- date: Instant


World event
- id: uuid
- description: string
- tags: List<string>
- region: Circle
- date: Instant

Faction
- id: uuid
- name: string
- description: string

Reputation:
- id: uuid
- personId: uuid
- factionId: uuid
- score: int

World
- id: uuid
- name: string
- description: string

Country
- id: uuid
- worldId: uuid
- name: string
- boundary: Circle
- description: string

Race
- id
- name
- description

Province
- id: uuid
- countryId: uuid
- name: string
- boundary: Circle
- description: string

City
- id: uuid
- provinceId: uuid
- name: string
- boundary: Circle
- width: int
- height: int
- description: string

Building
- id: uuid
- cityId: uuid
- name: string
- description: string
- boundary: Rectangle

Circle
- center: Location
- radius: float

Quest
- id: uuid
- name: string
- description
- giverId: uuid
- itemRewards: List<uuid>
- goldReward: int
- experienceReward: int
- prerequisiteQuests: List<uuid>

QuestObjective
- id: uuid
- questId: uuid
- name: string
- description: string
- region: Circle
- type: kill | collect | explore | speak
- target: uuid
- amount: int?

Profession
- id
- name
- description

FactionMembers
- id: uuid
- personId: uuid
- factionId: uuid
- role: FactionRole

PersonSkills
- id: uuid
- personId: uuid
- skillId: uuid
- cooldown: int

BuildingOwners
- id: uuid
- buildingId: uuid
- ownerId: uuid

PersonQuestObjective
- id: uuid
- personId: uuid
- objectiveId: uuid
- amount: int

TravelRoute
- id: uuid
- name: string
- originCityId: uuid
- destinationCityId: uuid
- distance: float
- travelTime: int
- dangerLevel: float

Job
- id: uuid
- personId: uuid
- action: sleep | work | idle | patrol | socialize
- startHour: int
- endHour: int
- daily: boolean
- priority: int
- location: Location

BuildingProps
- id: uuid
- buildingId: uuid
- coordinates: Point
- name: string
- description: string

PersonQuest
- id: uuid
- personId: uuid
- questId: uuid
- progress: accepted | completed | failed | abandoned

SkillPrerequisites
- skillId: uuid
- prerequisiteSkillId: uuid

Location
- coordinates: Point
- worldId: uuid
- cityId: uuid?
- buildingId: uuid?

Rectangle
- left: int
- top: int
- right: int
- bottom: int

Point
- x: int
- y: int

EquipmentSlot = helm | chest | leftHand | rightHand | boots | necklace | gloves | leftRing | rightRing | belt

EffectStat = current hp | maximum hp | current ap | maximum ap | strength | defense | dexterity | endurance | intelligence

ItemCategory = helm | chest | sword | spear | bow | staff | shield | boots | necklace | gloves | ring | belt | arrows | consumable | quest | crafting_material

FactionRole = leader | member

services:
PersonService
- Add(Person person, CancellationToken cancellationToken): Task
- GetById(Guid id, CancellationToken cancellationToken): Task<Person?>
- GetAllWithinRange(Location center, float radius, CancellationToken cancellationToken): Task<List<Person>>
- Update(Person person, CancellationToken cancellationToken): Task
- Delete(Guid id, CancellationToken cancellationToken): Task

ItemService
- Add(Item item, CancellationToken cancellationToken): Task
- GetById(Guid id, CancellationToken cancellationToken): Task<Item?>
- Update(Item item, CancellationToken cancellationToken): Task
- Delete(Guid id, CancellationToken cancellationToken): Task

InventoryService
- Add(Guid personId, Guid itemId, int quantity, CancellationToken cancellationToken): Task
- Equip(Guid personId, Guid itemId, EquipmentSlot slot, CancellationToken cancellationToken): Task
- Unequip(Guid personId, EquipmentSlot slot, CancellationToken cancellationToken): Task
- GetAllByPersonId(Guid personId, CancellationToken cancellationToken): Task<List<InventoryItem>>
- Remove(Guid personId, Guid itemId, int quantity, CancellationToken cancellationToken): Task

SkillService
- AddSkill(Guid personId, Guid skillId, CancellationToken cancellationToken): Task
- GetById(Guid id, CancellationToken cancellationToken): Task<Skill?>
- GetAll(CancellationToken cancellationToken): Task<List<Skill>>
- GetAllByPersonId(Guid personId, CancellationToken cancellationToken): Task<List<PersonSkill>>
- GetAllPrerequisites(Guid id, CancellationToken cancellationToken): Task<List<Guid>>
- RemoveSkill(Guid personId, Guid skillId, CancellationToken cancellationToken): Task

NpcConversationService
- AddMessage(Guid fromId, Guid toId, string message, CancellationToken cancellationToken): Task
- GetAllMessages(Guid playerId, Guid npcId, int startingMessageIndex, CancellationToken cancellationToken): Task<List<NpcChatMessage>>
- UpdateSummary(Guid playerId, Guid npcId, string summary, CancellationToken cancellationToken): Task

WorldEventService
- Add(WorldEvent worldEvent, CancellationToken cancellationToken): Task
- GetById(Guid id, CancellationToken cancellationToken): Task<WorldEvent?>
- GetAllByRegion(Circle region, CancellationToken cancellationToken): Task<List<WorldEvent>>

FactionService
- AddMember(Guid factionId, Guid personId, FactionRole role, CancellationToken cancellationToken): Task
- GetById(Guid id, CancellationToken cancellationToken): Task<Faction?>
- GetAllMembershipsByPersonId(Guid personId, CancellationToken cancellationToken): Task<List<FactionMember>>
- GetAllMembersByFactionId(Guid factionId, CancellationToken cancellationToken): Task<List<FactionMember>>
- UpdateMemberRole(Guid factionId, Guid memberId, FactionRole role, CancellationToken cancellationToken): Task
- RemoveMember(Guid factionId, Guid memberId, CancellationToken cancellationToken): Task

ReputationService
- AdjustReputation(Guid personId, Guid factionId, int deltaScore, CancellationToken cancellationToken): Task
- GetAllByPersonId(Guid personId, CancellationToken cancellationToken): Task<List<Reputation>>

LocationService
- GetWorldById(Guid id, CancellationToken cancellationToken): Task<World?>
- GetCountryById(Guid id, CancellationToken cancellationToken): Task<Country?>
- GetAllCountriesByWorldId(Guid worldId, CancellationToken cancellationToken): Task<List<Country>>
- GetProvinceById(Guid id, CancellationToken cancellationToken): Task<Province?>
- GetAllProvincesByCountryId(Guid countryId, CancellationToken cancellationToken): Task<List<Province>>
- GetCityById(Guid id, CancellationToken cancellationToken): Task<City?>
- GetAllCitiesByProvinceId(Guid provinceId, CancellationToken cancellationToken): Task<List<City>>

RaceService
- GetById(Guid id, CancellationToken cancellationToken): Task<Race?>
- GetAll(CancellationToken cancellationToken): Task<List<Race>>

QuestService
- AssignQuest(Guid questId, Guid personId, CancellationToken cancellationToken): Task
- GetById(Guid questId, CancellationToken cancellationToken): Task<Quest?>
- GetAllQuestsByGiverId(Guid giverId, CancellationToken cancellationToken): Task<List<Quest>>
- GetAllQuestObjectivesByQuestId(Guid questId, CancellationToken cancellationToken): Task<List<QuestObjective>>
- GetAllQuestsByPersonId(Guid personId, CancellationToken cancellationToken): Task<List<PersonQuest>>
- GetAllQuestObjectivesByPersonId(Guid personId, CancellationToken cancellationToken): Task<List<PersonQuestObjective>>
- ProgressObjective(Guid personId, Guid questObjectiveId, int amount, CancellationToken cancellationToken): Task
- SetQuestStatus(Guid questId, Guid personId, QuestStatus status, CancellationToken cancellationToken): Task

ProfessionService
- GetById(Guid id, CancellationToken cancellationToken): Task<Profession?>
- GetAll(CancellationToken cancellationToken): Task<List<Profession>>

BuildingService
- AddOwner(Guid buildingId, Guid ownerId, CancellationToken cancellationToken): Task
- GetById(Guid id, CancellationToken cancellationToken): Task<Building?>
- GetAllByCityId(Guid cityId, CancellationToken cancellationToken): Task<List<Building>>
- GetAllPropsByBuildingId(Guid buildingId, CancellationToken cancellationToken): Task<List<BuildingProp>>
- GetAllOwnersByBuildingId(Guid buildingId, CancellationToken cancellationToken): Task<List<BuildingOwner>>
- RemoveOwner(Guid buildingId, Guid ownerId, CancellationToken cancellationToken): Task

NavigationService
- GetShortestRoute(Guid originCityId, Guid destinationCityId, CancellationToken cancellationToken): Task<List<TravelRoute>>

JobService
- Add(Job job, CancellationToken cancellationToken): Task
- GetAllByPersonId(Guid personId, CancellationToken cancellationToken): Task<List<Job>>
- Update(Job job, CancellationToken cancellationToken): Task
- Delete(Guid id, CancellationToken cancellationToken): Task
*/