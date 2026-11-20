print("hello from JS");
print("frame=" + emu.framecount());

while (true) {
  print("tick @" + emu.framecount());
  await emu.frameadvance();
}
