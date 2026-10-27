"""Generate the C# message codec from nwn-message.py's message definitions.

Writes src/OpenWinter.Net.Nwn/Messages/Generated/*.g.cs. Run it inside nwn-message.py's environment:

    uv run --project <path to nwn-message.py> python tools/nwnproto/generate.py

Every dataclass and enum in nwn_message.messages and nwn_message.net_types becomes a C# type nested in one static
class per Python module. Field semantics follow nwn-message.py's runtime reader and writer: conditions see the
struct's earlier fields, a skipped field takes the class attribute's value (or null), and size prefixes, sparse
bitmask lists and tagged unions are encoded as it encodes them. Types with hand-written read or write methods are
emitted without those methods; their C# counterparts live in Messages/Custom.
"""
import argparse
import dataclasses
import enum
import importlib
import inspect
import keyword
import math
import pkgutil
import re
import subprocess
import sys
import types
import typing
from pathlib import Path

import nwn_message
import nwn_message.messages as messages_pkg
from nwn_message import net_types
from nwn_message.codegen import FieldKind, TypeResolver
from nwn_message.message import REGISTRY, Direction, Message, MessageType, validate_annotation
from nwn_message.message._annotation import (
    BuildSizeHint,
    ComputedAddSizeHint,
    ComputedMultiplySizeHint,
    FixedSizeHint,
    QuantFloat,
    RangeFloat,
    SizePrefix,
    TwoDARowCountHint,
)
from nwn_message.types import LocStr

REPO = Path(__file__).resolve().parents[2]
OUT = REPO / "src" / "OpenWinter.Net.Nwn" / "Messages" / "Generated"
NAMESPACE = "OpenWinter.Net.Nwn.Messages"

WIRE_CS = {
    MessageType.BOOL: "bool",
    MessageType.BYTE: "byte",
    MessageType.CHAR: "sbyte",
    MessageType.WORD: "ushort",
    MessageType.SHORT: "short",
    MessageType.DWORD: "uint",
    MessageType.INT: "int",
    MessageType.DWORD64: "ulong",
    MessageType.INT64: "long",
    MessageType.FLOAT: "float",
    MessageType.DOUBLE: "double",
    MessageType.RESREF: "string",
    MessageType.STRING: "string",
    MessageType.JSON: "string",
    MessageType.OBJECT_ID: "uint",
}
WIRE_NAME = {
    MessageType.BOOL: "Bool",
    MessageType.BYTE: "Byte",
    MessageType.CHAR: "Char",
    MessageType.WORD: "Word",
    MessageType.SHORT: "Short",
    MessageType.DWORD: "Dword",
    MessageType.INT: "Int",
    MessageType.DWORD64: "Dword64",
    MessageType.INT64: "Int64",
    MessageType.FLOAT: "Float",
    MessageType.DOUBLE: "Double",
    MessageType.RESREF: "ResRef",
    MessageType.STRING: "String",
    MessageType.JSON: "Json",
    MessageType.OBJECT_ID: "ObjectId",
}
VALUE_TYPES = {"bool", "byte", "sbyte", "ushort", "short", "uint", "int", "ulong", "long", "float", "double",
               "NwnLocString"}
CS_KEYWORDS = {"base", "event", "object", "string", "params", "operator", "class", "struct", "default", "checked",
               "fixed", "lock", "namespace", "internal", "public", "private", "protected", "readonly", "ref", "out",
               "in", "is", "as", "new", "this", "throw", "typeof", "using", "virtual", "volatile", "void", "while"}


def pascal(name: str) -> str:
    parts = [p for p in re.split(r"_+", name.strip("_")) if p]
    out = "".join(p[:1].upper() + p[1:] if not p.isupper() else p.capitalize() for p in parts)
    if not out:
        out = "Value"
    if out[0].isdigit():
        out = "N" + out
    return out


def class_name(py: type) -> str:
    n = py.__name__.lstrip("_")
    return n[0].upper() + n[1:] if n else "Anon"


def module_class(module_name: str) -> str:
    return pascal(module_name.rsplit(".", 1)[-1]) + "Messages"


def apply_corrections():
    """Fields the server encodes differently from their declaration, measured from recorded WSNetLayer traffic. NUI
    window definitions, bind values, event payloads and group layouts are JSON fields (type byte, length, text), not
    size-prefixed blobs, and a bind entry ends after its value. The reference reader and writer are corrected to
    match and keep JSON as its text, as the C# codec does. A body bag's creature is an object id (type byte 0x11),
    not a dword."""
    from nwn_message.message import _io
    from nwn_message.message import _message
    from nwn_message.messages import game_obj_update_add, nui

    def read_json(self, **_):
        self._read_canary(MessageType.JSON)
        return self._bytes(self._u32()).decode(_io.get_codepage())

    def write_json(self, value, **_):
        self._write_canary(MessageType.JSON)
        encoded = value.encode(_io.get_codepage())
        self._u32(len(encoded))
        self._buf += encoded

    _io.Reader.read_json = read_json
    _io.Writer.write_json = write_json
    json_field = typing.Annotated[object, MessageType.JSON]
    data = nui.NuiCreateWindow.__dataclass_fields__["data"]
    data.type = typing.Annotated[json_field | None, typing.get_args(data.type)[1]]
    nui.NuiEventEntry.__dataclass_fields__["payload"].type = json_field
    nui.NuiBindEntry.__dataclass_fields__["value"].type = json_field
    nui.NuiSetLayout.__dataclass_fields__["data"].type = json_field
    del nui.NuiBindEntry.__dataclass_fields__["array_index"]
    bag = game_obj_update_add.GameObjUpdateObjListAddPlaceable.__dataclass_fields__["body_bag_creature_id"]
    bag.type = typing.Annotated[_message.ObjectId, *(a for a in typing.get_args(bag.type)[1:] if not isinstance(a, MessageType))]
    add_chat_play_sound()


def add_chat_play_sound():
    """PlaySound (chat 0x09 0x07, server to client), which the definitions lack: the object that plays it and the
    sound's resref as a string (recorded from nwserver: a creature's PlaySound("as_cv_bell1"))."""
    from nwn_message import message as m
    from nwn_message.messages import chat

    if hasattr(chat, "ChatPlaySound"):
        return
    ns = {"MAJOR": 0x09, "MINOR": 0x07, "DIRECTION": m.Direction.S2C, "__module__": chat.__name__,
          "__annotations__": {"speaker_id": m.ObjectId, "sound": m.String}}
    chat.ChatPlaySound = dataclasses.dataclass(kw_only=True)(type("ChatPlaySound", (m.Message,), ns))


def git_commit() -> str:
    root = Path(nwn_message.__file__).resolve().parents[2]
    try:
        return subprocess.check_output(["git", "-C", str(root), "rev-parse", "--short", "HEAD"], text=True).strip()
    except Exception:  # noqa: BLE001 - the commit is informational
        return "unknown"


class Model:
    """Every generated type, keyed by Python type, with its C# name."""

    def __init__(self):
        self.modules = [importlib.import_module(f"{messages_pkg.__name__}.{m.name}")
                        for m in pkgutil.iter_modules(messages_pkg.__path__)] + [net_types]
        self.module_names = {m.__name__ for m in self.modules}
        self.types: dict[type, str] = {}  # python type -> C# reference
        self.children: dict[object, list[type]] = {}  # module or type -> nested types, in source order
        self.owner: dict[type, object] = {}
        for mod in self.modules:
            for v in vars(mod).values():
                if inspect.isclass(v) and v.__module__ == mod.__name__ and self._wanted(v):
                    self._add(v, mod)
        self.external: list[type] = []

    @staticmethod
    def _wanted(v) -> bool:
        return dataclasses.is_dataclass(v) or (issubclass(v, enum.Enum) and v is not enum.Enum)

    def _add(self, cls: type, owner):
        if cls in self.owner:
            return
        self.owner[cls] = owner
        self.children.setdefault(owner, []).append(cls)
        for v in vars(cls).values():
            if inspect.isclass(v) and v.__module__ == cls.__module__ and self._wanted(v):
                self._add(v, cls)

    def ref(self, cls: type) -> str:
        if cls in self.types:
            return self.types[cls]
        if cls not in self.owner:
            if not (inspect.isclass(cls) and issubclass(cls, enum.Enum)):
                raise TypeError(f"type {cls!r} is outside the generated modules")
            self.owner[cls] = "external"
            self.children.setdefault("external", []).append(cls)
            self.external.append(cls)
        chain = []
        node = cls
        while not isinstance(node, str) and inspect.isclass(node):
            chain.append(class_name(node))
            node = self.owner[node]
        top = "NwnExternalTypes" if node == "external" else module_class(node.__name__)
        name = top + "." + ".".join(reversed(chain))
        self.types[cls] = name
        return name


class Emitter:
    def __init__(self, model: Model):
        self.m = model
        self.tmp = 0

    def t(self, prefix: str) -> str:
        self.tmp += 1
        return f"{prefix}{self.tmp}"

    # -- types --------------------------------------------------------------

    def cs_type(self, f) -> str:
        k = f.kind
        if k == FieldKind.PRIMITIVE:
            if f.wire_type is None:
                raise TypeError(f"{f.name}: primitive without a wire type")
            return WIRE_CS[f.wire_type]
        if k == FieldKind.ENUM:
            return self.m.ref(self.py_type(f.type_ref))
        if k == FieldKind.DATACLASS:
            return self.m.ref(self.py_type(f.type_ref))
        if k == FieldKind.BYTES:
            return "byte[]"
        if k == FieldKind.LOCSTRING:
            return "NwnLocString"
        if k == FieldKind.TUPLE:
            return "(" + ", ".join(self.elem_type(e) for e in f.elements) + ")"
        if k == FieldKind.LIST:
            return f"List<{self.elem_type(f.element)}>"
        if k == FieldKind.BITMASK_LIST:
            inner = self.cs_type(f.bitmask_element)
            return f"List<{inner}?>"
        if k == FieldKind.TAGGED:
            return "NwnTagged"
        raise TypeError(f"unknown kind {k}")

    def elem_type(self, f) -> str:
        t = self.cs_type(f)
        return t + "?" if (f.conditionals or f.bool_prefix) else t

    def py_type(self, qualname: str) -> type:
        mod, _, rest = qualname.partition(".messages.")
        if rest:
            module_name, _, inner = rest.partition(".")
            obj = importlib.import_module(f"nwn_message.messages.{module_name}")
        else:
            parts = qualname.split(".")
            for i in range(len(parts) - 1, 0, -1):
                try:
                    obj = importlib.import_module(".".join(parts[:i]))
                    inner = ".".join(parts[i:])
                    break
                except ModuleNotFoundError:
                    continue
        for part in inner.split("."):
            obj = getattr(obj, part)
        return obj

    # -- literals -----------------------------------------------------------

    def literal(self, value, cs: str, f=None) -> str:
        nullable = cs.endswith("?")
        base = cs[:-1] if nullable else cs
        if value is None:
            return "null"
        if isinstance(value, LocStr):
            if value.str_ref != 0xFFFFFFFF:
                return f"NwnLocString.FromStrRef({value.str_ref}u, {int(value.gender)})"
            return f"NwnLocString.FromText({self.string(str(value))})"
        if isinstance(value, enum.Enum):
            if base in VALUE_TYPES:
                return self.literal(int(value.value), cs)
            name = getattr(value, "name", None)
            members = type(value).__members__
            if name and name in members and members[name] is value:
                return f"{base}.{self.member_name(type(value), name)}"
            return f"({base})({int(value.value)}L)"
        if isinstance(value, bool):
            return "true" if value else "false"
        if isinstance(value, int):
            if base == "bool":
                return "true" if value else "false"
            if base == "float":
                return f"{float(value)!r}f"
            if base == "double":
                return f"{float(value)!r}d"
            if base in ("byte", "sbyte", "ushort", "short", "int"):
                lo, hi = {"byte": (0, 255), "sbyte": (-128, 127), "ushort": (0, 65535), "short": (-32768, 32767),
                          "int": (-2**31, 2**31 - 1)}[base]
                return str(value) if lo <= value <= hi else f"unchecked(({base})({value}L))"
            if base == "uint":
                return f"{value}u" if 0 <= value < 2**32 else f"unchecked((uint)({value}L))"
            if base == "ulong":
                return f"{value}ul" if value >= 0 else f"unchecked((ulong)({value}L))"
            if base == "long":
                return f"{value}L"
            return f"({base})({value}L)"
        if isinstance(value, float):
            if base == "float":
                return self.float_literal(value, "f")
            return self.float_literal(value, "d")
        if isinstance(value, str):
            return self.string(value)
        if isinstance(value, bytes):
            return "[" + ", ".join(str(b) for b in value) + "]" if value else "[]"
        if isinstance(value, tuple):
            elems = self.tuple_types(base)
            return "(" + ", ".join(self.literal(v, t) for v, t in zip(value, elems)) + ")"
        if isinstance(value, list) and not value:
            return "new()"
        raise TypeError(f"no C# literal for {value!r} as {cs}")

    @staticmethod
    def float_literal(v: float, suffix: str) -> str:
        if math.isinf(v):
            return ("float" if suffix == "f" else "double") + (".PositiveInfinity" if v > 0 else ".NegativeInfinity")
        if math.isnan(v):
            return ("float" if suffix == "f" else "double") + ".NaN"
        return f"{v!r}{suffix}"

    @staticmethod
    def tuple_types(cs: str) -> list[str]:
        inner = cs.strip()[1:-1]
        out, depth, cur = [], 0, ""
        for ch in inner:
            if ch in "(<":
                depth += 1
            elif ch in ")>":
                depth -= 1
            if ch == "," and depth == 0:
                out.append(cur.strip())
                cur = ""
            else:
                cur += ch
        out.append(cur.strip())
        return out

    @staticmethod
    def string(s: str) -> str:
        out = ['"']
        for ch in s:
            if ch in '"\\':
                out.append("\\" + ch)
            elif 32 <= ord(ch) < 127:
                out.append(ch)
            else:
                out.append(f"\\u{ord(ch):04x}")
        out.append('"')
        return "".join(out)

    def member_name(self, enum_cls: type, name: str) -> str:
        n = pascal(name.lower()) if name.isupper() or "_" in name else pascal(name)
        if n == class_name(enum_cls):
            n += "Value"
        return n

    # -- conditions ---------------------------------------------------------

    def cond(self, c, props: dict[str, str]) -> str:
        if c.op in ("build", "not_build"):
            b, p, x = c.value
            s = f"r.Context.Satisfies({b}, {p}, {x})"
            return s if c.op == "build" else "!" + s
        field = props.get(c.field)
        if field is None:
            raise KeyError(f"condition on unknown field {c.field}")
        if c.op == "eq":
            return f"NwnCond.L({field}) == {self.num(c.value)}"
        if c.op == "ne":
            return f"NwnCond.L({field}) != {self.num(c.value)}"
        if c.op == "gt":
            return f"NwnCond.L({field}) > {self.num(c.value)}"
        if c.op == "flag":
            return f"NwnCond.Flag({field}, {self.num(c.value)})"
        if c.op in ("in", "not_in"):
            fn = "In" if c.op == "in" else "NotIn"
            vals = ", ".join(self.num(v) for v in sorted(c.value, key=lambda v: int(v)))
            return f"NwnCond.{fn}({field}, {vals})"
        if c.op == "twoda":
            table, column, values = c.value
            vals = ", ".join(self.string(v) for v in sorted(values))
            return (f"NwnCond.InSet(r.Context.TwoDA({self.string(table)}, NwnCond.Row({field}), "
                    f"{self.string(column)}), {vals})")
        raise ValueError(f"unknown condition {c.op}")

    @staticmethod
    def num(v) -> str:
        if isinstance(v, enum.Enum):
            v = v.value
        if isinstance(v, bool):
            return "1L" if v else "0L"
        if isinstance(v, int):
            return f"{v}L"
        raise TypeError(f"condition value {v!r} is not numeric")

    # -- reading ------------------------------------------------------------

    def read(self, f, target: str, props: dict[str, str], missing: str, ind: str) -> list[str]:
        """Statements that read field f into target; missing is the value of a skipped field."""
        lines: list[str] = []
        conds = [self.cond(c, props) for c in f.conditionals]
        inner = self.read_present(f, target, props, ind + ("    " if conds else ""), missing)
        if conds:
            lines.append(f"{ind}if ({' && '.join(conds)})")
            lines.append(f"{ind}{{")
            lines += inner
            lines.append(f"{ind}}}")
            lines.append(f"{ind}else {target} = {missing};")
        else:
            lines += inner
        return lines

    def read_present(self, f, target, props, ind, missing) -> list[str]:
        if f.bool_prefix:
            body = self.read_value(f, target, props, ind + "    ")
            return [f"{ind}if (r.ReadBool())", f"{ind}{{", *body, f"{ind}}}", f"{ind}else {target} = {missing};"]
        return self.read_value(f, target, props, ind)

    def count_expr(self, f, props, prefix_var) -> str:
        h = f.size_hint
        if isinstance(h, SizePrefix):
            return prefix_var
        if isinstance(h, FixedSizeHint):
            return str(h.size)
        if isinstance(h, TwoDARowCountHint):
            return f"r.Context.TwoDARowCount({self.string(h.twoda_name)})"
        if isinstance(h, ComputedMultiplySizeHint):
            return f"(int)((NwnCond.L({props[h.field1]}) ?? 0) * (NwnCond.L({props[h.field2]}) ?? 0))"
        if isinstance(h, ComputedAddSizeHint):
            return f"(int)((NwnCond.L({props[h.field1]}) ?? 0) + (NwnCond.L({props[h.field2]}) ?? 0))"
        raise TypeError(f"{f.name}: unsupported size hint {h!r}")

    def slots_expr(self, f, props) -> str:
        v = f.bitmask_max_slots
        if isinstance(v, int):
            return str(v)
        if isinstance(v, str):
            return f"(int)(NwnCond.L({props[v]}) ?? 0)"
        if isinstance(v, TwoDARowCountHint):
            return f"r.Context.TwoDARowCount({self.string(v.twoda_name)})"
        raise TypeError(f"{f.name}: unsupported slot count {v!r}")

    def read_value(self, f, target, props, ind) -> list[str]:
        k = f.kind
        lines: list[str] = []
        prefix_var = None
        if isinstance(f.size_hint, SizePrefix):
            prefix_var = self.t("n")
            lines.append(f"{ind}var {prefix_var} = (int)r.ReadInteger(NwnWireType.{WIRE_NAME[f.size_hint.value]});")
        elif isinstance(f.size_hint, BuildSizeHint):
            raise TypeError(f"{f.name}: build-dependent sizes are not generated")
        if k == FieldKind.PRIMITIVE:
            lines.append(f"{ind}{target} = {self.read_primitive(f)};")
        elif k == FieldKind.ENUM:
            lines.append(f"{ind}{target} = ({self.cs_type(f)})r.ReadInteger(NwnWireType.{WIRE_NAME[f.wire_type]}"
                         f"{self.bits_arg(f)});")
        elif k == FieldKind.DATACLASS:
            lines.append(f"{ind}{target} = r.ReadStruct<{self.cs_type(f)}>();")
        elif k == FieldKind.BYTES:
            lines.append(f"{ind}{target} = r.ReadVoid();")
        elif k == FieldKind.LOCSTRING:
            lines.append(f"{ind}{target} = r.ReadLocString();")
        elif k == FieldKind.TUPLE:
            temps = []
            for e in f.elements:
                tv = self.t("e")
                temps.append(tv)
                lines.append(f"{ind}{self.elem_type(e)} {tv};")
                lines += self.read(e, tv, props, self.missing_elem(e), ind)
            lines.append(f"{ind}{target} = ({', '.join(temps)});")
        elif k == FieldKind.LIST:
            count = self.count_expr(f, props, prefix_var)
            lv, iv, ev = self.t("list"), self.t("i"), self.t("e")
            et = self.elem_type(f.element)
            cv = self.t("count")
            lines.append(f"{ind}var {cv} = {count};")
            lines.append(f"{ind}var {lv} = new List<{et}>(Math.Max(0, {cv}));")
            lines.append(f"{ind}for (int {iv} = 0; {iv} < {cv}; {iv}++)")
            lines.append(f"{ind}{{")
            lines.append(f"{ind}    {et} {ev};")
            lines += self.read(f.element, ev, props, self.missing_elem(f.element), ind + "    ")
            lines.append(f"{ind}    {lv}.Add({ev});")
            lines.append(f"{ind}}}")
            lines.append(f"{ind}{target} = {lv};")
        elif k == FieldKind.BITMASK_LIST:
            fv, sv, lv, iv, ev = self.t("flags"), self.t("slots"), self.t("list"), self.t("i"), self.t("e")
            et = self.cs_type(f.bitmask_element)
            lines.append(f"{ind}var {fv} = unchecked((ulong)r.ReadInteger(NwnWireType."
                         f"{WIRE_NAME[f.bitmask_size_prefix.value]}));")
            lines.append(f"{ind}var {sv} = {self.slots_expr(f, props)};")
            lines.append(f"{ind}var {lv} = new List<{et}?>(Math.Max(0, {sv}));")
            lines.append(f"{ind}for (int {iv} = 0; {iv} < {sv}; {iv}++)")
            lines.append(f"{ind}{{")
            lines.append(f"{ind}    if ({iv} >= 64 || ((({fv} >> {iv}) & 1) == 0)) {{ {lv}.Add(null); continue; }}")
            lines.append(f"{ind}    {et}? {ev};")
            lines += self.read(f.bitmask_element, ev, props, "null", ind + "    ")
            lines.append(f"{ind}    {lv}.Add({ev});")
            lines.append(f"{ind}}}")
            lines.append(f"{ind}{target} = {lv};")
        elif k == FieldKind.TAGGED:
            kv = self.t("tag")
            lines.append(f"{ind}var {kv} = {self.tag_read(f, props)};")
            lines.append(f"{ind}switch ({kv})")
            lines.append(f"{ind}{{")
            for key, variant in self.variants(f):
                vv = self.t("v")
                vt = self.elem_type(variant)
                lines.append(f"{ind}    case {key}L:")
                lines.append(f"{ind}    {{")
                lines.append(f"{ind}        {vt} {vv};")
                lines += self.read(variant, vv, props, self.missing_elem(variant), ind + "        ")
                lines.append(f"{ind}        {target} = new NwnTagged({kv}, {vv}!);")
                lines.append(f"{ind}        break;")
                lines.append(f"{ind}    }}")
            lines.append(f"{ind}    default:")
            lines.append(f"{ind}        throw new NwnWireException($\"{f.name}: unknown variant {{{kv}}}\");")
            lines.append(f"{ind}}}")
        else:
            raise TypeError(f"unknown kind {k}")
        return lines

    def tag_read(self, f, props) -> str:
        if f.tagged_twoda_name:
            return (f"long.Parse(r.Context.TwoDA({self.string(f.tagged_twoda_name)}, "
                    f"NwnCond.Row({props[f.tagged_twoda_field]}), {self.string(f.tagged_twoda_column)}))")
        bits = f", {f.tagged_discriminator_bits}" if f.tagged_discriminator_bits else ""
        return f"r.ReadInteger(NwnWireType.{WIRE_NAME[f.tagged_discriminator_type]}{bits})"

    def variants(self, f):
        for v in f.tagged_variants:
            yield self.tag_key(v.key), v.field

    def tag_key(self, key: str) -> int:
        if key in ("true", "false"):
            return 1 if key == "true" else 0
        try:
            return int(key)
        except ValueError:
            qual, _, member = key.rpartition(".")
            return int(getattr(self.py_type(qual), member).value)

    @staticmethod
    def bits_arg(f) -> str:
        return f", {f.bits}" if f.bits else ""

    def read_primitive(self, f) -> str:
        wt = f.wire_type
        if wt == MessageType.BOOL:
            return "r.ReadBool()"
        if wt == MessageType.FLOAT:
            return f"r.ReadFloat({self.float_enc(f)})"
        if wt == MessageType.DOUBLE:
            return "r.ReadDouble()"
        if wt == MessageType.RESREF:
            return "r.ReadResRef()"
        if wt == MessageType.STRING:
            return "r.ReadString()"
        if wt == MessageType.JSON:
            return "r.ReadJson()"
        if wt == MessageType.OBJECT_ID:
            return "r.ReadObjectId()"
        bits = str(f.bits) if f.bits else ""
        return f"r.Read{WIRE_NAME[wt]}({bits})"

    @staticmethod
    def float_enc(f) -> str:
        fb = f.float_bits
        if isinstance(fb, QuantFloat):
            return f"NwnFloatEncoding.Quantised({fb.multiplier!r}f, {fb.bits})"
        if isinstance(fb, RangeFloat):
            return f"NwnFloatEncoding.Ranged({fb.min!r}f, {fb.max!r}f, {fb.bits})"
        return ""

    def missing_elem(self, f) -> str:
        return "null" if (f.conditionals or f.bool_prefix) else "default!"

    # -- writing ------------------------------------------------------------

    def write(self, f, value: str, props, ind, nullable: bool = False) -> list[str]:
        """Statements that write field f from value; nullable says whether value's C# type is nullable."""
        conds = [self.cond(c, props).replace("r.Context", "w.Context") for c in f.conditionals]
        inner_ind = ind + ("    " if conds else "")
        present = self.strip(f, value, nullable)
        if f.bool_prefix:
            hv = self.t("has")
            body = self.write_value(f, present, props, inner_ind + "    ")
            inner = [f"{inner_ind}var {hv} = NwnCond.Truthy({value});", f"{inner_ind}w.WriteBool({hv});",
                     f"{inner_ind}if ({hv})", f"{inner_ind}{{", *body, f"{inner_ind}}}"]
        else:
            inner = self.write_value(f, present, props, inner_ind)
        if conds:
            return [f"{ind}if ({' && '.join(conds)})", f"{ind}{{", *inner, f"{ind}}}"]
        return inner

    def strip(self, f, value: str, nullable: bool) -> str:
        """value without its nullability (the field is known to be present)."""
        if not nullable:
            return value
        t = self.cs_type(f)
        if t in VALUE_TYPES or t.startswith("(") or f.kind == FieldKind.ENUM:
            return f"{value}.GetValueOrDefault()"
        return f"{value}!"

    @staticmethod
    def optional(f) -> bool:
        return bool(f.conditionals or f.bool_prefix)

    def write_value(self, f, value, props, ind) -> list[str]:
        k = f.kind
        lines: list[str] = []
        if isinstance(f.size_hint, SizePrefix):
            length = f"{value}.Count" if k in (FieldKind.LIST, FieldKind.BITMASK_LIST) else f"{value}.Length"
            lines.append(f"{ind}w.WriteInteger(NwnWireType.{WIRE_NAME[f.size_hint.value]}, {length});")
        if k == FieldKind.PRIMITIVE:
            lines.append(f"{ind}{self.write_primitive(f, value)};")
        elif k == FieldKind.ENUM:
            lines.append(f"{ind}w.WriteInteger(NwnWireType.{WIRE_NAME[f.wire_type]}, (long){value}{self.bits_arg(f)});")
        elif k == FieldKind.DATACLASS:
            lines.append(f"{ind}w.WriteStruct({value});")
        elif k == FieldKind.BYTES:
            lines.append(f"{ind}w.WriteVoid({value});")
        elif k == FieldKind.LOCSTRING:
            lines.append(f"{ind}w.WriteLocString({value});")
        elif k == FieldKind.TUPLE:
            tv = self.t("tuple")
            lines.append(f"{ind}var {tv} = {value};")
            for i, e in enumerate(f.elements):
                lines += self.write(e, f"{tv}.Item{i + 1}", props, ind, self.optional(e))
        elif k == FieldKind.LIST:
            ev = self.t("e")
            lines.append(f"{ind}foreach (var {ev} in {value})")
            lines.append(f"{ind}{{")
            lines += self.write(f.element, ev, props, ind + "    ", self.optional(f.element))
            lines.append(f"{ind}}}")
        elif k == FieldKind.BITMASK_LIST:
            fv, sv, iv = self.t("flags"), self.t("slots"), self.t("i")
            lines.append(f"{ind}var {sv} = {self.slots_expr(f, props).replace('r.Context', 'w.Context')};")
            lines.append(f"{ind}ulong {fv} = 0;")
            lines.append(f"{ind}for (int {iv} = 0; {iv} < {sv} && {iv} < {value}.Count && {iv} < 64; {iv}++)")
            lines.append(f"{ind}    if ({value}[{iv}] != null) {fv} |= 1UL << {iv};")
            lines.append(f"{ind}w.WriteInteger(NwnWireType.{WIRE_NAME[f.bitmask_size_prefix.value]}, "
                         f"unchecked((long){fv}));")
            iv2 = self.t("i")
            lines.append(f"{ind}for (int {iv2} = 0; {iv2} < {sv} && {iv2} < {value}.Count; {iv2}++)")
            lines.append(f"{ind}{{")
            lines.append(f"{ind}    if ({value}[{iv2}] == null) continue;")
            lines += self.write(f.bitmask_element, f"{value}[{iv2}]", props, ind + "    ", True)
            lines.append(f"{ind}}}")
        elif k == FieldKind.TAGGED:
            kv = self.t("tag")
            if f.tagged_twoda_name:
                lines.append(f"{ind}var {kv} = {self.tag_read(f, props).replace('r.Context', 'w.Context')};")
            else:
                bits = f", {f.tagged_discriminator_bits}" if f.tagged_discriminator_bits else ""
                lines.append(f"{ind}var {kv} = {value}.Tag;")
                lines.append(f"{ind}w.WriteInteger(NwnWireType.{WIRE_NAME[f.tagged_discriminator_type]}, {kv}{bits});")
            lines.append(f"{ind}switch ({kv})")
            lines.append(f"{ind}{{")
            for key, variant in self.variants(f):
                vt = self.cs_type(variant)
                lines.append(f"{ind}    case {key}L:")
                lines.append(f"{ind}    {{")
                vv = self.t("v")
                lines.append(f"{ind}        var {vv} = ({vt}){value}.Value;")
                lines += self.write(variant, vv, props, ind + "        ", self.optional(variant))
                lines.append(f"{ind}        break;")
                lines.append(f"{ind}    }}")
            lines.append(f"{ind}    default:")
            lines.append(f"{ind}        throw new NwnWireException($\"{f.name}: unknown variant {{{kv}}}\");")
            lines.append(f"{ind}}}")
        else:
            raise TypeError(f"unknown kind {k}")
        return lines

    def write_primitive(self, f, value) -> str:
        wt = f.wire_type
        if wt == MessageType.BOOL:
            return f"w.WriteBool({value})"
        if wt == MessageType.FLOAT:
            enc = self.float_enc(f)
            return f"w.WriteFloat({value}{', ' + enc if enc else ''})"
        if wt == MessageType.DOUBLE:
            return f"w.WriteDouble({value})"
        if wt == MessageType.RESREF:
            return f"w.WriteResRef({value})"
        if wt == MessageType.STRING:
            return f"w.WriteString({value})"
        if wt == MessageType.JSON:
            return f"w.WriteJson({value})"
        if wt == MessageType.OBJECT_ID:
            return f"w.WriteObjectId({value})"
        bits = f", {f.bits}" if f.bits else ""
        return f"w.Write{WIRE_NAME[wt]}({value}{bits})"


def resolve(resolver: TypeResolver, annotation, name: str):
    """nwn-message.py's field model, with the presence flag restored: its model keeps BoolPrefix only on primitive
    fields, while its runtime honours it on every field."""
    af = resolver.resolve_field(annotation, name)
    restore_presence(af, annotation)
    return af


def restore_presence(af, annotation):
    anno = validate_annotation(annotation)
    af.bool_prefix = anno.bool_prefix
    rt = anno.real_type
    if typing.get_origin(rt) is typing.Annotated:
        inner = validate_annotation(rt)
        af.bool_prefix = af.bool_prefix or inner.bool_prefix
        rt = inner.real_type
    args = typing.get_args(rt)
    if af.kind == FieldKind.LIST and args:
        restore_presence(af.element, args[0])
    elif af.kind == FieldKind.BITMASK_LIST and args:
        restore_presence(af.bitmask_element, args[0])
    elif af.kind == FieldKind.TUPLE:
        for e, a in zip(af.elements, args):
            restore_presence(e, a)
    elif af.kind == FieldKind.TAGGED and anno.tagged is not None:
        for v, a in zip(af.tagged_variants, anno.tagged.lut.values()):
            restore_presence(v.field, a)


def custom_io(cls: type) -> tuple[bool, bool]:
    """Types with a hand-written read or write are hand-written in C# in both directions."""
    custom = any("read" in b.__dict__ or "write" in b.__dict__ for b in cls.__mro__)
    return custom, custom


def emit_type(e: Emitter, cls: type, ind: str, out: list[str], report: dict):
    m = e.m
    name = class_name(cls)
    if issubclass(cls, enum.Enum):
        if issubclass(cls, enum.Flag):
            out.append(f"{ind}[Flags]")
        out.append(f"{ind}public enum {name} : long")
        out.append(f"{ind}{{")
        used = set()
        for member_name, member in cls.__members__.items():
            n = e.member_name(cls, member_name)
            while n in used:
                n += "_"
            used.add(n)
            out.append(f"{ind}    {n} = {int(member.value)}L,")
        out.append(f"{ind}}}")
        return

    is_message = inspect.isclass(cls) and issubclass(cls, Message)
    custom_read, custom_write = custom_io(cls)
    if custom_read or custom_write:
        # Hand-written in Messages/Custom; only its nested types are generated here.
        report["custom_read"].append(m.ref(cls))
        nested = m.children.get(cls, [])
        if not nested:
            out.append(f"{ind}// {name}: Messages/Custom")
            return
        out.append(f"{ind}public sealed partial class {name}")
        out.append(f"{ind}{{")
        for i, n in enumerate(nested):
            if i:
                out.append("")
            emit_type(e, n, ind + "    ", out, report)
        out.append(f"{ind}}}")
        return
    base = " : NwnMessage" if is_message else " : INwnStruct"
    out.append(f"{ind}public sealed partial class {name}{base}")
    out.append(f"{ind}{{")
    inner = ind + "    "

    if is_message:
        direction = {Direction.BOTH: "Both", Direction.C2S: "ClientToServer", Direction.S2C: "ServerToClient"}[
            cls.DIRECTION]
        out.append(f"{inner}public const byte MajorId = 0x{cls.MAJOR:02X};")
        out.append(f"{inner}public const byte MinorId = 0x{cls.MINOR:02X};")
        out.append(f"{inner}public override byte Major => MajorId;")
        out.append(f"{inner}public override byte Minor => MinorId;")
        out.append(f"{inner}public override NwnDirection Direction => NwnDirection.{direction};")
        out.append("")

    nested = m.children.get(cls, [])
    nested_names = {class_name(n) for n in nested}
    reserved = nested_names | {name} | ({"Major", "Minor", "Direction", "MajorId", "MinorId"} if is_message else set())
    reserved |= {"Read", "Write"}

    resolver = TypeResolver(None, None)
    fields = []
    props: dict[str, str] = {}
    used: set[str] = set()
    for fld in dataclasses.fields(cls):
        af = resolve(resolver, fld.type, fld.name)
        prop = pascal(fld.name)
        if prop in reserved or prop in used or keyword.iskeyword(prop.lower()) and prop.lower() in CS_KEYWORDS:
            prop += "Value"
        while prop in reserved or prop in used:
            prop += "_"
        used.add(prop)
        props[fld.name] = prop
        fields.append((fld, af, prop))

    for fld, af, prop in fields:
        cs = field_type(e, cls, fld, af)
        if_missing = getattr(cls, fld.name, None)
        optional = bool(af.conditionals or af.bool_prefix)
        nullable = optional and if_missing is None
        decl = cs + ("?" if nullable else "")
        init = initializer(e, fld, decl, nullable)
        out.append(f"{inner}[NwnField({e.string(fld.name)})]")
        out.append(f"{inner}public {decl} {prop} {{ get; set; }}{(' = ' + init + ';') if init else ''}")

    if fields:
        out.append("")

    if not custom_read:
        out.append(f"{inner}public {'override ' if is_message else ''}void Read(NwnReader r)")
        out.append(f"{inner}{{")
        for fld, af, prop in fields:
            if_missing = getattr(cls, fld.name, None)
            cs = e.cs_type(af)
            nullable = bool(af.conditionals or af.bool_prefix) and if_missing is None
            missing = e.literal(if_missing, cs + ("?" if nullable else "")) if if_missing is not None else (
                "null" if nullable else "default!")
            out += e.read(af, prop, props, missing, inner + "    ")
        out.append(f"{inner}}}")
        out.append("")
    if not custom_write:
        out.append(f"{inner}public {'override ' if is_message else ''}void Write(NwnWriter w)")
        out.append(f"{inner}{{")
        for fld, af, prop in fields:
            nullable = bool(af.conditionals or af.bool_prefix) and getattr(cls, fld.name, None) is None
            out += e.write(af, prop, props, inner + "    ", nullable)
        out.append(f"{inner}}}")

    for n in nested:
        out.append("")
        emit_type(e, n, inner, out, report)
    while out and out[-1] == "":
        out.pop()
    out.append(f"{ind}}}")


def field_type(e: Emitter, cls: type, fld, af) -> str:
    """The property's C# type. Hand-coded types may use plain Python types and unions."""
    if not any(custom_io(cls)):
        return e.cs_type(af)
    try:
        return e.cs_type(af)
    except (TypeError, KeyError):
        return plain_type(e, fld.type)


def plain_type(e: Emitter, t) -> str:
    origin, args = typing.get_origin(t), typing.get_args(t)
    if origin is typing.Annotated:
        resolved = TypeResolver(None, None).resolve_field(t, "x")
        try:
            return e.cs_type(resolved)
        except (TypeError, KeyError):
            return plain_type(e, args[0])
    if origin in (typing.Union, types.UnionType):
        rest = [a for a in args if a is not type(None)]
        if len(rest) == 1:
            inner = plain_type(e, rest[0])
            return inner if inner.endswith("?") else inner + "?"
        return "object"
    if origin is list:
        return f"List<{plain_type(e, args[0])}>"
    if origin is tuple:
        return "(" + ", ".join(plain_type(e, a) for a in args) + ")"
    if t is LocStr:
        return "NwnLocString"
    simple = {int: "long", float: "float", bool: "bool", str: "string", bytes: "byte[]"}
    if t in simple:
        return simple[t]
    if inspect.isclass(t) and (dataclasses.is_dataclass(t) or issubclass(t, enum.Enum)):
        return e.m.ref(t)
    return "object"


def initializer(e: Emitter, fld, decl: str, nullable: bool) -> str:
    if fld.default is not dataclasses.MISSING:
        return e.literal(fld.default, decl)
    if fld.default_factory is not dataclasses.MISSING:
        produced = fld.default_factory()
        if isinstance(produced, list):
            return "new()" if not produced else e.literal(produced, decl)
        if dataclasses.is_dataclass(produced):
            return "new()"
        return e.literal(produced, decl)
    if nullable:
        return ""
    base = decl
    if base == "string":
        return '""'
    if base == "byte[]":
        return "[]"
    if base.startswith("List<"):
        return "new()"
    if base == "NwnTagged":
        return "null!"
    if base in VALUE_TYPES or base.startswith("("):
        return ""
    if "." in base and not base.startswith("("):
        # enums need nothing; struct classes start empty
        return "" if is_enum_ref(e, base) else "new()"
    return ""


def is_enum_ref(e: Emitter, ref: str) -> bool:
    return any(v == ref and issubclass(k, enum.Enum) for k, v in e.m.types.items())


HEADER = """// <auto-generated>
// Generated by tools/nwnproto/generate.py from nwn-message.py {commit} (GPL-3.0). Do not edit.
// </auto-generated>
#nullable enable
#pragma warning disable CS0108
using OpenWinter.Net.Nwn.Wire;

namespace {ns};
"""


def main():
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("--out", type=Path, default=OUT)
    args = ap.parse_args()

    apply_corrections()
    model = Model()
    emitter = Emitter(model)
    # Name every type first so references resolve the same way everywhere.
    for cls in list(model.owner):
        model.ref(cls)

    commit = git_commit()
    args.out.mkdir(parents=True, exist_ok=True)
    for old in args.out.glob("*.g.cs"):
        old.unlink()

    report = {"custom_read": [], "custom_write": []}
    owners = [m for m in model.modules if model.children.get(m)]
    for mod in owners:
        out = [HEADER.format(commit=commit, ns=NAMESPACE), f"public static partial class {module_class(mod.__name__)}",
               "{"]
        first = True
        for cls in model.children[mod]:
            if not first:
                out.append("")
            first = False
            emit_type(emitter, cls, "    ", out, report)
        out.append("}")
        path = args.out / f"{module_class(mod.__name__)}.g.cs"
        path.write_text("\n".join(out) + "\n", encoding="utf-8", newline="\n")

    if model.external:
        out = [HEADER.format(commit=commit, ns=NAMESPACE), "public static partial class NwnExternalTypes", "{"]
        for i, cls in enumerate(model.external):
            if i:
                out.append("")
            emit_type(emitter, cls, "    ", out, report)
        out.append("}")
        (args.out / "NwnExternalTypes.g.cs").write_text("\n".join(out) + "\n", encoding="utf-8", newline="\n")

    # The registry: (major, minor, direction) -> message type.
    entries = []
    for (major, minor, direction), cls in sorted(REGISTRY.items(), key=lambda kv: (kv[0][0], kv[0][1], kv[0][2].value)):
        d = "ServerToClient" if direction == Direction.S2C else "ClientToServer"
        entries.append(f"            (0x{major:02X}, 0x{minor:02X}, NwnDirection.{d}) => new {model.ref(cls)}(),")
    reg = [HEADER.format(commit=commit, ns=NAMESPACE),
           "/// <summary>Creates the message type for a major, minor and direction.</summary>",
           "public static class NwnMessageRegistry",
           "{",
           f"    public const int Count = {len(set(REGISTRY.values()))};",
           "",
           "    public static NwnMessage? Create(byte major, byte minor, NwnDirection direction) =>",
           "        (major, minor, direction) switch",
           "        {",
           *entries,
           "            _ => null,",
           "        };",
           "}"]
    (args.out / "NwnMessageRegistry.g.cs").write_text("\n".join(reg) + "\n", encoding="utf-8", newline="\n")

    print(f"{len(model.owner)} types, {len(set(REGISTRY.values()))} messages, nwn-message.py {commit}")
    print("hand-written (Messages/Custom):", len(report["custom_read"]))
    for r in report["custom_read"]:
        print("   ", r)


if __name__ == "__main__":
    sys.exit(main())
