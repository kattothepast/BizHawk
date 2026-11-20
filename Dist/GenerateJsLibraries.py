#!/usr/bin/env python3
"""Generate *JsLibrary.cs files from *LuaLibrary.cs by mechanical rewrite."""
from __future__ import annotations

import pathlib
import re
import sys

ROOT = pathlib.Path(__file__).resolve().parents[1]

TRANSFORMS = [
	(r"\bLuaLibraryBase\b", "JsLibraryBase"),
	(r"\bILuaLibraries\b", "IJsLibraries"),
	(r"\bluaLibsImpl\b", "jsLibsImpl"),
	(r"\b_luaLibsImpl\b", "_jsLibsImpl"),
	(r"\bLuaFunction\b", "ScriptObject"),
	(r"\bLuaTable\b", "object"),
	(r"\bNamedLuaFunction\b", "NamedJsFunction"),
	(r"\bINamedLuaFunction\b", "INamedJsFunction"),
	(r"\bIRegisterFunctions\b", "IRegisterJsFunctions"),
	(r"\bLuaFile\b", "JsFile"),
	(r"\bLuaCanvas\b", "JsCanvas"),
	(r"\bLuaWinform\b", "JsWinform"),
	(r"\bLuaButton\b", "JsButton"),
	(r"\bLuaTextBox\b", "JsTextBox"),
	(r"\bLuaCheckbox\b", "JsCheckbox"),
	(r"\bLuaDropDown\b", "JsDropDown"),
	(r"\bLuaPictureBox\b", "JsPictureBox"),
	(r"\bNLFAddCallback\b", "NLFAddCallback"),
	(r"using NLua;\s*\n", "using Microsoft.ClearScript;\n"),
	(r"using NLua\.\w+;\s*\n", ""),
	(r"\bLuaConsole\b", "JsConsole"),
	(r"\bluaf\b", "jsf"),
	(r"\bLua Hook\b", "JS Hook"),
	(r"lua function", "JS function"),
	(r"Lua function", "JS function"),
	(r"lua scripts", "JS scripts"),
	(r"Lua scripts", "JS scripts"),
	(r"\bCreateAndRegisterNamedFunction\b", "CreateAndRegisterNamedFunction"),
]

CLASS_RENAME = [
	(r"class (\w+)LuaLibrary\b", r"class \1JsLibrary"),
	(r"(\w+)LuaLibrary\(", r"\1JsLibrary("),
]


def transform(text: str) -> str:
	for pat, repl in TRANSFORMS:
		text = re.sub(pat, repl, text)
	for pat, repl in CLASS_RENAME:
		text = re.sub(pat, repl, text)
	# PrettyPrintShallow on tables -> ToString
	text = text.replace("table.PrettyPrintShallow()", "table?.ToString() ?? \"null\"")
	return text


def convert_file(src: pathlib.Path, dest: pathlib.Path) -> None:
	text = src.read_text(encoding="utf-8")
	out = transform(text)
	dest.parent.mkdir(parents=True, exist_ok=True)
	dest.write_text(out, encoding="utf-8")
	print(f"wrote {dest.relative_to(ROOT)}")


def main() -> int:
	pairs = [
		(ROOT / "src/BizHawk.Client.Common/lua/CommonLibs", ROOT / "src/BizHawk.Client.Common/js/CommonLibs"),
		(ROOT / "src/BizHawk.Client.Common/lua/LuaHelperLibs", ROOT / "src/BizHawk.Client.Common/js/HelperLibs"),
		(ROOT / "src/BizHawk.Client.EmuHawk/tools/Lua/Libraries", ROOT / "src/BizHawk.Client.EmuHawk/tools/Js/Libraries"),
	]
	for src_dir, dest_dir in pairs:
		for src in sorted(src_dir.glob("*LuaLibrary.cs")):
			name = src.name.replace("LuaLibrary.cs", "JsLibrary.cs")
			convert_file(src, dest_dir / name)
	return 0


if __name__ == "__main__":
	sys.exit(main())
