# Notify kinds: society package

| kind | subject | data fields | meaning |
| --- | --- | --- | --- |
| `party` | the receiving PC | `leader` (uint, party leader PC id), `members` (array of `{ id, pc, master, associateType }`: every creature in the party faction, PCs and their associates) | The receiving PC's party changed: someone joined or left (AddToParty, RemoveFromParty, logout), or an associate joined or left (AddHenchman, RemoveHenchman, summons via `AssociateService`). Sent to each PC in the party. |
