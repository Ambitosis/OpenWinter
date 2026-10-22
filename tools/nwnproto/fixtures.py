"""Write conformance fixtures for the C# message codec, using nwn-message.py as the reference.

For every message type, random instances are encoded and decoded by nwn-message.py; each fixture is the encoded
frame and the decoded value. The C# tests decode every frame, compare the value and re-encode it to the same bytes.
Game data is replaced by a fixed fake context, mirrored in the tests: every 2DA has FAKE_ROWS rows, a cell holds its
row number, and talk table entries are not resolved.

    uv run --project <path to nwn-message.py> python tools/nwnproto/fixtures.py
"""
import argparse
import dataclasses
import enum
import gzip
import json
import random
import struct
import sys
import types
import typing
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import generate  # noqa: E402

from nwn.types import Gender  # noqa: E402
from nwn_message.context import Context  # noqa: E402
from nwn_message.message import REGISTRY, Direction, Message, MessagePrefix, MessageType  # noqa: E402
from nwn_message.message import read as read_message  # noqa: E402
from nwn_message.message._annotation import (  # noqa: E402
    BitmaskConditionedList,
    ComputedAddSizeHint,
    ComputedMultiplySizeHint,
    FixedSizeHint,
    SizePrefix,
    Tagged,
    TwoDARowCountHint,
    TwoDATagged,
    validate_annotation,
)
from nwn_message.messages.levelup import LevelUpNull, LevelUpSpellLevelChanges  # noqa: E402
from nwn_message.types import STRREF_NONE, LocStr, Version  # noqa: E402

OUT = generate.REPO / "tests" / "OpenWinter.Tests" / "Data" / "nwn-message-fixtures.jsonl.gz"
FAKE_ROWS = 4
OBJ_LIST_SAMPLES = 12
PEER_VERSION = Version(8193, 37, 17)

INT_RANGES = {
    MessageType.BOOL: (0, 1),
    MessageType.BYTE: (0, 255),
    MessageType.CHAR: (-128, 127),
    MessageType.WORD: (0, 65535),
    MessageType.SHORT: (-32768, 32767),
    MessageType.DWORD: (0, 2**32 - 1),
    MessageType.INT: (-2**31, 2**31 - 1),
    MessageType.DWORD64: (0, 2**64 - 1),
    MessageType.INT64: (-2**63, 2**63 - 1),
    MessageType.OBJECT_ID: (0, 2**32 - 1),
}


class _Peer:
    their_version = PEER_VERSION

    def satisfies(self, build, patch, postfix):
        return PEER_VERSION.satisfies(Version(build, patch, postfix))


class FakeContext(Context):
    def __init__(self):
        super().__init__(_Peer())
        self.fields = {}

    def twoda_row_count(self, twoda):
        return FAKE_ROWS

    def twoda(self, twoda, row, column):
        if twoda == "visualeffects" and column == "Type_FD":
            return "P" if row % 2 == 0 else "F"
        return str(row)

    def tlk(self, row, gender=Gender.MALE):
        return LocStr("", str_ref=row, gender=gender)

    def satisfies(self, build, patch, postfix):
        return self.peer.satisfies(build, patch, postfix)


class Gen:
    """Random values for annotated types, following the constraints the encoder needs."""

    def __init__(self, rng: random.Random):
        self.rng = rng

    def value(self, t, fields: dict, depth: int):
        anno = validate_annotation(t)
        rt = anno.real_type
        if typing.get_origin(rt) is typing.Annotated:
            return self.value(rt, fields, depth)
        if anno.tagged is not None:
            lut = anno.tagged.lut
            if isinstance(anno.tagged, TwoDATagged):
                key = int(fields[anno.tagged.field])
                variant = lut[key]
            else:
                variant = self.rng.choice(list(lut.values()))
            return self.value(variant, fields, depth + 1)
        if anno.bitmask_conditioned_list is not None:
            bcl = anno.bitmask_conditioned_list
            slots = bcl.max_slots
            if isinstance(slots, str):
                slots = int(fields.get(slots) or 0)
            elif isinstance(slots, TwoDARowCountHint):
                slots = FAKE_ROWS
            slots = min(slots, 64)
            inner = typing.get_args(rt)[0]
            return [self.value(inner, fields, depth + 1) if self.rng.random() < 0.5 else None for _ in range(slots)]
        origin = typing.get_origin(rt)
        if origin in (typing.Union, types.UnionType):
            choices = [a for a in typing.get_args(rt) if a is not type(None)]
            return self.value(self.rng.choice(choices), fields, depth + 1)
        if origin is tuple:
            return tuple(self.value(a, fields, depth + 1) for a in typing.get_args(rt))
        if origin is list:
            inner = typing.get_args(rt)[0]
            n = self.length(anno.size_hint, fields, depth)
            return [self.value(inner, fields, depth + 1) for _ in range(n)]
        if isinstance(rt, type) and issubclass(rt, enum.Enum):
            return self.enum(rt)
        if dataclasses.is_dataclass(rt):
            return self.instance(rt, depth + 1)
        if rt is LocStr:
            return LocStr("", str_ref=self.rng.randrange(0, 2**24), gender=self.rng.choice([Gender.MALE, Gender.FEMALE]))
        if rt is bytes:
            if isinstance(anno.size_hint, FixedSizeHint):
                n = anno.size_hint.size
            else:
                n = self.rng.randrange(0, 9)
            return bytes(self.rng.randrange(256) for _ in range(n))
        mt = anno.message_type
        if rt is bool or mt == MessageType.BOOL:
            return self.rng.random() < 0.5
        if rt is float or mt in (MessageType.FLOAT, MessageType.DOUBLE):
            v = self.rng.uniform(-1000, 1000)
            if mt != MessageType.DOUBLE:
                v = struct.unpack("<f", struct.pack("<f", v))[0]
            return v
        if rt is str:
            if mt == MessageType.RESREF:
                return "".join(self.rng.choice("abcdefghijklmnopqrstuvwxyz_0123456789") for _ in range(self.rng.randrange(0, 17)))
            return "".join(self.rng.choice("abcdefgh XYZ 123-éü") for _ in range(self.rng.randrange(0, 12)))
        if rt is list:
            return []
        if rt is int:
            lo, hi = INT_RANGES.get(mt, (0, 255))
            if anno.bits and lo == 0:
                hi = min(hi, 2**anno.bits - 1)
            return self.rng.choice([lo, hi, 0, 1, self.rng.randint(lo, hi), self.rng.randint(max(lo, 0), min(hi, 10))])
        raise TypeError(f"no generator for {t!r}")

    def length(self, hint, fields, depth) -> int:
        if isinstance(hint, FixedSizeHint):
            return hint.size
        if isinstance(hint, TwoDARowCountHint):
            return FAKE_ROWS
        if isinstance(hint, ComputedMultiplySizeHint):
            return int(fields[hint.field1]) * int(fields[hint.field2])
        if isinstance(hint, ComputedAddSizeHint):
            return int(fields[hint.field1]) + int(fields[hint.field2])
        return self.rng.randrange(0, 3 if depth < 3 else 1)

    def enum(self, cls):
        members = list(cls)
        if issubclass(cls, enum.Flag) and members:
            v = cls(0)
            for mm in members:
                if self.rng.random() < 0.4:
                    v |= mm
            return v
        return self.rng.choice(members)

    def instance(self, cls, depth: int):
        if cls is LevelUpNull:
            # Level-up choices always carry all ten spell levels.
            msg = self._instance(cls, depth)
            msg.spell_changes = [self._instance(LevelUpSpellLevelChanges, depth + 1) for _ in range(10)]
            return msg
        return self._instance(cls, depth)

    def _instance(self, cls, depth: int):
        fields: dict = {}
        constrained = self.constraints(cls)
        hints = typing.get_type_hints(cls, include_extras=True)
        for f in dataclasses.fields(cls):
            t = hints.get(f.name, f.type)
            if f.name in constrained:
                fields[f.name] = self.rng.choice(constrained[f.name])
            elif f.name in self.small_fields(cls):
                fields[f.name] = self.rng.randrange(0, 3)
            else:
                fields[f.name] = self.value(t, fields, depth)
        return cls(**fields)

    @staticmethod
    def constraints(cls) -> dict:
        """Fields that pick a 2DA-tagged variant must hold a key of that variant table."""
        out = {}
        hints = typing.get_type_hints(cls, include_extras=True)
        for f in dataclasses.fields(cls):
            anno = validate_annotation(hints.get(f.name, f.type))
            if isinstance(anno.tagged, TwoDATagged):
                out[anno.tagged.field] = list(anno.tagged.lut.keys())
        return out

    @staticmethod
    def small_fields(cls) -> set:
        """Fields used as list sizes or slot counts: keep them small."""
        out = set()
        hints = typing.get_type_hints(cls, include_extras=True)
        for f in dataclasses.fields(cls):
            anno = validate_annotation(hints.get(f.name, f.type))
            if isinstance(anno.size_hint, (ComputedMultiplySizeHint, ComputedAddSizeHint)):
                out |= {anno.size_hint.field1, anno.size_hint.field2}
            if anno.bitmask_conditioned_list and isinstance(anno.bitmask_conditioned_list.max_slots, str):
                out.add(anno.bitmask_conditioned_list.max_slots)
        return out


def object_list_messages(gen: Gen, rng: random.Random):
    """Object lists exercising every entry kind: random lists rarely hit them all."""
    from nwn_message.messages import game_obj_update as gou
    from nwn_message.messages.game_obj_update_shared import GameObjUpdateType
    from nwn_message.messages.game_obj_update_update import _GENERIC_UPDATE_LUT
    from nwn_message.types import ObjectType

    generic_type = {cls: t for t, cls in _GENERIC_UPDATE_LUT.items()}
    flag_bits = [int(f) for f in GameObjUpdateType]
    for entry_cls in typing.get_args(gou.ObjListEntry):
        # Creature updates have the most branches and fail the encoder most often when random.
        samples = OBJ_LIST_SAMPLES * (6 if entry_cls is gou.GameObjUpdateObjListUpdateCreature else 1)
        for _ in range(samples):
            entries = []
            for _ in range(rng.randrange(1, 4)):
                try:
                    e = gen.instance(entry_cls, 1)
                except TypeError:
                    continue
                if entry_cls in generic_type:
                    e.object_type = generic_type[entry_cls]
                    e.flags = sum(b for b in flag_bits if rng.random() < 0.35)
                    if entry_cls.__name__ == "ObjListUpdateItem" and rng.random() < 0.5:
                        e.flags |= 0x40
                    if entry_cls.__name__ == "ObjListUpdateTrigger":
                        add = gen.instance(gou.GameObjUpdateObjListAddTrigger, 1)
                        add.object_id = e.object_id
                        if add.is_trap:
                            e.clickable = None
                        elif e.clickable is None:
                            e.clickable = rng.random() < 0.5
                        entries.append(add)
                entries.append(e)
            yield gou.GameObjUpdateObjList(entries=entries)


def normalize(v):
    if v is None:
        return None
    if isinstance(v, LocStr):
        if v.str_ref != STRREF_NONE:
            return {"strref": v.str_ref, "gender": int(v.gender)}
        return {"text": str(v)}
    if isinstance(v, enum.Enum):
        return int(v.value)
    if isinstance(v, bool):
        return v
    if isinstance(v, (int, float, str)):
        return v
    if isinstance(v, bytes):
        return v.hex()
    if isinstance(v, (list, tuple)):
        return [normalize(x) for x in v]
    if dataclasses.is_dataclass(v):
        return {f.name: normalize(getattr(v, f.name)) for f in dataclasses.fields(v)}
    raise TypeError(f"can't normalize {v!r}")


def main():
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("--samples", type=int, default=6)
    ap.add_argument("--seed", type=int, default=1)
    ap.add_argument("--out", type=Path, default=OUT)
    args = ap.parse_args()

    model = generate.Model()
    for cls in list(model.owner):
        model.ref(cls)
    rng = random.Random(args.seed)
    gen = Gen(rng)

    rows, failures = [], {}
    messages = sorted(set(REGISTRY.values()), key=lambda c: (c.MAJOR, c.MINOR, c.__name__))
    for cls in messages:
        prefix = MessagePrefix.PLAYER_CLIENT_TO_SERVER if cls.DIRECTION == Direction.C2S \
            else MessagePrefix.PLAYER_SERVER_TO_CLIENT
        ok = 0
        for attempt in range(args.samples * 4):
            if ok >= args.samples:
                break
            ctx = FakeContext()
            try:
                msg = gen.instance(cls, 0)
                frame = msg.dump(prefix, ctx)
                decoded = read_message(frame, FakeContext())
                again = decoded.dump(prefix, FakeContext())
            except Exception as e:  # noqa: BLE001 - recorded per type below
                failures[cls.__name__] = f"{type(e).__name__}: {e}"
                continue
            if again != frame:
                failures[cls.__name__] = "nwn-message.py does not re-encode its own decoding identically"
                continue
            rows.append({"type": model.ref(cls), "hex": frame.hex(), "value": normalize(decoded)})
            ok += 1
        if ok:
            failures.pop(cls.__name__, None)

    from nwn_message.messages.game_obj_update import GameObjUpdateObjList
    list_ok = 0
    for msg in object_list_messages(gen, rng):
        try:
            frame = msg.dump(MessagePrefix.PLAYER_SERVER_TO_CLIENT, FakeContext())
            decoded = read_message(frame, FakeContext())
            if decoded.dump(MessagePrefix.PLAYER_SERVER_TO_CLIENT, FakeContext()) != frame:
                continue
        except Exception:  # noqa: BLE001 - an inconsistent random entry; others cover the kind
            continue
        rows.append({"type": model.ref(GameObjUpdateObjList), "hex": frame.hex(), "value": normalize(decoded)})
        list_ok += 1
    print(f"{list_ok} object lists covering every entry kind")

    args.out.parent.mkdir(parents=True, exist_ok=True)
    with gzip.open(args.out, "wt", encoding="utf-8", newline="\n") as f:
        for row in rows:
            f.write(json.dumps(row, separators=(",", ":"), ensure_ascii=False) + "\n")
    covered = len({r["type"] for r in rows})
    print(f"{len(rows)} fixtures for {covered} of {len(messages)} messages -> {args.out}")
    for name, why in sorted(failures.items()):
        print(f"  not covered: {name}: {why}")


if __name__ == "__main__":
    sys.exit(main())
