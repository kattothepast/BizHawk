# BizHawk JS/TS scripting

JavaScript and TypeScript scripting runs beside Lua via **Microsoft ClearScript V8**.

## Layout

- `samples/` — starter scripts (`hello.js`, `hello.ts`)
- `vendor/typescript.js` — in-process TypeScript transpile-on-load (no system Node/`tsc`)
- `_docs/bizhawk.d.ts` — editor IntelliSense for the curated API surface

## Packaging notes

ClearScript natives are **not** vendored under `Assets/dll` (unlike `lua54.dll`). NuGet packages supply:

- `ClearScriptV8.win-x64.dll`
- `ClearScriptV8.linux-x64.so`

plus managed `ClearScript.Core.dll` / `ClearScript.V8.dll` / `ClearScript.V8.ICUData.dll`.

They land in `output/dll/` at build time (same folder as `lua54.dll`). `Dist/Package.sh` includes `output/JS/*`, keeps the Linux `.so` on Linux packages, and strips `ClearScriptV8.win-x64.dll` from Linux packages (and `*.so` from Windows packages).

If natives fail to load, `JsLibraries.IsAvailable` is `false` and the JS Console shows an error instead of crashing.

## Cooperative yield

Use top-level `await` (or an async IIFE):

```js
while (true) {
  print(emu.framecount());
  await emu.frameadvance();
}
```

## Tools

Open **Tools → JS Console** to load `.js` / `.ts` scripts. Lua Console is unchanged.
