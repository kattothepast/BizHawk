// TypeScript sample — transpiled on load by EmuHawk (no system tsc required)
declare const print: (...args: unknown[]) => void;
declare const emu: { framecount(): number; frameadvance(): Promise<void> };

print("hello from TypeScript");
print(`frame=${emu.framecount()}`);

while (true) {
  print(`tick @${emu.framecount()}`);
  await emu.frameadvance();
}
