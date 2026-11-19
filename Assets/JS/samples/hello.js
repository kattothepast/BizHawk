// Minimal BizHawk JS sample — print + framecount + cooperative frameadvance
print("hello from JS");
print("frame=" + emu.framecount());

while (true) {
  print("tick @" + emu.framecount());
  await emu.frameadvance();
}
