# Notify kinds: save games

Sent with `IWorldOutput.Notify(kind, subject, data, player)` by `SaveGames/SaveGameRequests.cs`
(single player, `ServerOptions.SaveGames`). See [SAVEGAME.md](../SAVEGAME.md).

| kind | to | subject | data fields | meaning |
| --- | --- | --- | --- | --- |
| `saves-list` | player | the PC | `saves`: array of `folder`, `name`, `kind` (`manual` / `quick` / `auto`), `savedAt` ("yyyy-MM-dd HH:mm", local), `module`, `area`, `player`, `gameTime`, `played` (seconds played in total), `screenshot` (true when the save has a screen.tga) | Answer to `list-saves`, and after every save; newest first |
| `save-screenshot` | player | the PC | `folder`, `tga` (base64 screen.tga, empty when there is none) | Answer to `save-screenshot` (thumbnails for the Save/Load window) |
| `save-result` | player | the PC | `action` (`save` / `load`), `ok`, `message` | Outcome of a save, quick save, autosave or load (a failed load leaves the current game running) |

Client requests (`ClientRequestMessage` kind and JSON): `list-saves`; `save-game` {`name`, `folder` (optional:
manual save slot to overwrite), `screenshot` (optional base64 TGA)}; `quicksave` {`screenshot`?}; `load-game`
{`folder`}; `quickload`; `save-screenshot` {`folder`}. A load replaces the world: the client gets despawns, `LoadArea`, spawns and
`ControlObject` as on an area transition, plus `nui-close` for open NUI windows and `journal-reset` + `journal`.
