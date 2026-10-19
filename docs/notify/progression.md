# Notify kinds and requests: character creation and level-up

Requests go client → server with `ClientSession.Request(kind, data)`. Before the player has a
creature (character selection screen) they are handled by `Progression.ChargenRequests.HandlePreLogin`
and answered with notifies whose subject is OBJECT_INVALID; in game they are normal
`[ClientRequest]` handlers answered to the player.

Choices use the JSON of `LevelChoice.ToJson()` (`class`, `ability`, `skills` {skill: ranks}, `feats` [ids],
`spells` {level: [ids]}, `domain1`, `domain2`, `school`, `familiar`, `familiarName`, `companion`,
`companionName`, `package`) and `NewCharacter.ToJson()` (identity, looks, `abilities` before racial
adjustments, `level`: a LevelChoice).

| request | when | data | answer (notify) |
| --- | --- | --- | --- |
| `chargen-data` | selection screen, game | – | `chargen-data`: races, classes (alignments allowed, packages, recommended scores), skills, feats, spells, packages, domains, schools, familiars, companions, phenotypes, soundsets, point buy (`min`, `max`, `primaryMin`, `cost[]`), `customPortraits`, `recommended`, `wings`, `tails` |
| `chargen-portraits` | selection screen | `race`, `gender` | `chargen-portraits`: `race`, `gender`, `portraits` [{`id` (portraits.2da row), `resref`}] |
| `chargen-describe` | selection screen, game | `kind` (feat, spell, skill, class, race, domain, school, package), `id` | `chargen-describe`: `kind`, `id`, `name`, `text` |
| `chargen-validate` | selection screen | `character` (NewCharacter), `auto` (fill empty parts from the package) | `chargen-plan`: `plan` (below), `identity` [{`code`, `message`}], `pointsSpent`, `pointsBudget` |
| `chargen-create` | selection screen | `character` | `chargen-result`: `ok`, `errors` [{`code`, `message`}], `file`, `name`; on success the player enters the game |
| `levelup-data` | game | – | `levelup-data`: `canLevelUp`, `level`, `xp`, `nextLevelXp`, `abilityIncrease`, `abilities` [6], `skillPoints` (saved), `classes` [{`id`, `level`, `available`, `reasons` []}] |
| `levelup-data` | game | `choice` (LevelChoice), `auto` | `levelup-plan`: plan (below) |
| `levelup-apply` | game | `choice` | `levelup-result`: `ok`, `errors`, `level`; then `xp-changed` and `character-sheet` |

Plan: `ok`, `errors` [{`code`, `message`}] (codes: class, prereq, alignment, ability, abilities, skills,
feats, feat-prereq, feats-unspent, spells, domains, school, familiar, companion, xp), `class`, `newLevel`,
`newClassLevel`, `hitDie`, `abilityIncrease`, `skillPoints` {`available`, `carried`, `gained`, `spent`, `left`},
`skills` [{`id`, `cost`, `max`, `current`, `classSkill`}], `feats` {`general`, `bonus`, `granted`,
`generalChoices`, `bonusChoices`}, `spells` [{`class`, `perLevel` {level: n}, `anyLevel`, `maxLevel`,
`choices` {level: [ids]}, `granted` {level: [ids]}}], `pickDomains`, `pickSchool`, `pickFamiliar`,
`pickCompanion`, `stats` {`abilities`, `bab`, `fortitude`, `reflex`, `will`} and, with `auto`, `auto` (the completed LevelChoice).
