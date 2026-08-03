// Mirrors TRPG.Contracts.Combat.Responses.CombatOutcome. SignalR hub payloads aren't covered by
// the OpenAPI generator (that only sees REST endpoints), so this is hand-written like
// combat-action.ts and combat-round-event.ts.
export type CombatOutcome = 'Victory' | 'Defeat' | 'Fled';
