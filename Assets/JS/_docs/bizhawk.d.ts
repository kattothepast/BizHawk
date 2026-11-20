/**
 * BizHawk JS/TS scripting surface (ClearScript V8).
 * Generated to mirror LuaCats / [LuaMethod] names. Prefer this for editor IntelliSense.
 * Note: use `await emu.frameadvance()` / `await emu.yield()` (Promise-based cooperative yield).
 * Arrays returned from memory helpers are 0-based (JS-native), unlike Lua's 1-based tables.
 */

declare function print(...args: unknown[]): void;

declare namespace emu {
  function framecount(): number;
  function frameadvance(): Promise<void>;
  function yield(): Promise<void>;
  function lagcount(): number;
  function islagged(): boolean;
  function getsystemid(): string;
  function displayvsync(enabled: boolean): void;
  function limitframerate(enabled: boolean): void;
}

declare namespace event {
  function onframeend(fn: () => void, name?: string): string;
  function onframestart(fn: () => void, name?: string): string;
  function onloadstate(fn: (name: string) => void, name?: string): string;
  function onsavestate(fn: (name: string) => void, name?: string): string;
  function onexit(fn: () => void, name?: string): string;
  function unregisterbyid(id: string): boolean;
  function unregisterbyname(name: string): boolean;
}

declare namespace memory {
  function readbyte(addr: number, domain?: string): number;
  function writebyte(addr: number, value: number, domain?: string): void;
  function getmemorydomainlist(): string[];
  function getcurrentmemorydomain(): string;
  function usememorydomain(domain: string): boolean;
}

declare namespace mainmemory {
  function readbyte(addr: number): number;
  function writebyte(addr: number, value: number): void;
}

declare namespace joypad {
  function get(controller?: number): Record<string, boolean | number>;
  function set(buttons: Record<string, boolean>, controller?: number): void;
}

declare namespace input {
  function get(): Record<string, boolean>;
}

declare namespace console {
  function log(...args: unknown[]): void;
  function clear(): void;
  function write(...args: unknown[]): void;
  function writeline(...args: unknown[]): void;
}

declare namespace client {
  function getwindowsize(): number;
  function openrom(path: string): void;
  function closerom(): void;
  function reboot_core(): void;
}

declare namespace movie {
  function mode(): string;
  function isloaded(): boolean;
  function length(): number;
}

declare namespace savestate {
  function save(path: string): void;
  function load(path: string): void;
}

declare namespace bit {
  function band(val: number, amt: number): number;
  function bor(val: number, amt: number): number;
  function bxor(val: number, amt: number): number;
  function bnot(val: number): number;
  function lshift(val: number, amt: number): number;
  function rshift(val: number, amt: number): number;
}

declare namespace bizstring {
  function hex(num: number): string;
  function binary(num: number): string;
  function tolower(str: string): string;
  function toupper(str: string): string;
}

declare namespace gui {
  function drawText(x: number, y: number, message: string, forecolor?: unknown, anchor?: string): void;
  function clearGraphics(surfaceName?: string): void;
  function drawBox(x: number, y: number, x2: number, y2: number, line?: unknown, background?: unknown, surfaceName?: string): void;
}

declare namespace forms {
  function newform(width?: number, height?: number, title?: string, onClose?: () => void): number;
  function button(formHandle: number, caption: string, clickEvent: () => void, x?: number, y?: number, width?: number, height?: number): number;
  function destroy(handle: number): boolean;
  function destroyall(): void;
}

declare namespace gameinfo {
  function getromname(): string;
  function getromhash(): string;
  function indb(): boolean;
}

declare namespace userdata {
  function set(key: string, value: unknown): void;
  function get(key: string): unknown;
  function clear(): void;
}

declare namespace tastudio {
  function engaged(): boolean;
}
