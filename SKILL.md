---
name: eorzea-reader
description: >-
  Build tools that read Final Fantasy XIV live game state from process memory
  and read static data from the game's files. Covers external ReadProcessMemory
  reading, locating data via FFXIVClientStructs signatures and struct offsets,
  and reading names/icons with Lumina. Use when the user wants to read FFXIV
  inventory, gear, party, character, or any in-game data from memory, port such
  a reader into a Blazor Hybrid (WPF + BlazorWebView) UI, or debug a signature /
  offset that stopped working after a patch.
---

# Reading FFXIV memory and game files

This skill captures a working, validated approach for reading FFXIV data from an
external .NET process. Two data sources:

1. **Live state** (inventory contents, equipped gear, current position…) — lives
   in the running `ffxiv_dx11.exe` process. Read it with `ReadProcessMemory`.
2. **Static data** (item names, icons, dye colors, recipe tables…) — lives in the
   game's `sqpack` files on disk. Read it with **Lumina**.

You almost never reverse-engineer anything yourself: the community project
**FFXIVClientStructs** already maintains the signatures and struct layouts, and
Lumina already parses the game files. Your job is to copy those into a small
reader and wire up a UI.

## Why external reading, not a Dalamud plugin

Dalamud injects into the game process and hands you typed FFXIVClientStructs
objects directly — much less work than signature scanning. This skill takes the
harder external path (`ReadProcessMemory` from a separate process) on purpose,
for when you want:

- **A standalone app with your own stack** — your own `.exe` and UI (WPF, Blazor,
  web…), not an ImGui overlay confined to Dalamud's plugin repo, API, and update
  cadence.
- **No injection, no XIVLauncher/Dalamud dependency** — the tool only *reads*
  memory; it loads no code into the game and works against a vanilla client the
  user hasn't modded.
- **A read-only scope** — you just observe state (inventory, gear…); you don't
  need to call game functions or hook events.

Reach for **Dalamud instead** when you need to *act* in-game (call functions, hook
events, draw in-game overlays) or want the easiest path with typed access and
offsets that update for you. Both read the same structs — Dalamud is easier,
external is more self-contained. Note both are against the game's ToS (see below).

## Host

The reader needs any host running full .NET. A **plain console app is enough** to
read and print state, and is the fastest way to prove the reader works — start
there. A UI is a separate, optional layer on top; use whatever stack you like.
One that happens to work well is Blazor Hybrid (WPF + BlazorWebView), since Razor
components run on full .NET and can call `ReadProcessMemory` directly — the
optional `references/blazor-hybrid-ui.md` walks through it if you want a UI.

## The workflow

1. **Copy the reusable skeleton.** `assets/WinApi.cs` (P/Invoke) and
   `assets/MemScanner.cs` (open process, scan `.text`, resolve RIP-relative
   addresses, follow pointer chains) are generic — drop them in unchanged.

2. **Get the signature + offsets from FFXIVClientStructs**, not by hand:
   - Repo: https://github.com/aers/FFXIVClientStructs
   - API docs (searchable): https://ffxiv.wildwolf.dev/
   - Find the manager struct (e.g. `InventoryManager`). Copy its
     `[StaticAddress("...")]` byte pattern and the `[FieldOffset(0x..)]` of the
     fields you need. Copy nested struct layouts too (`InventoryContainer`,
     `InventoryItem`, …).
   - See `references/clientstructs-mapping.md` for how a CS attribute maps to
     `MemScanner` calls line by line.

3. **Write a small reader** that subclasses `MemScanner`: resolve the signature
   once in the constructor, then read + parse the struct block in a `Read()`
   method. Filter/index as the struct dictates.

4. **Add names/icons with Lumina** — `references/game-files-lumina.md`. Item name
   is one line: `sItem.GetRow(id).Name`. Icons decode to a `data:image/bmp` URI.

5. **(Optional) Wire a UI.** The reader doesn't care what renders it — any
   full-.NET UI works. `references/blazor-hybrid-ui.md` is one worked example
   (Blazor Hybrid), not a requirement.

## Two gotchas that will bite you (they bit us)

These are the non-obvious failures; check them first when a reader returns
nothing or garbage.

- **Short signatures match in multiple places.** A pattern like
  `48 8D 0D ?? ?? ?? ?? 81 C2` (9 bytes) occurs many times in `.text`. Taking the
  *first* match gives the wrong address. Enumerate **all** matches and validate
  each against the expected struct shape, then keep the winner. `MemScanner` has
  `ResolveRipAll` for this; see `references/memory-reading.md`.

- **A `T*` field in FFXIVClientStructs is a pointer — you must dereference it.**
  e.g. `[FieldOffset(0x1E08)] public InventoryContainer* Inventories;` means
  `instance + 0x1E08` holds a *pointer to* the array, not the array. Read the
  8-byte pointer first, then index. Treating it as an inline struct reads garbage
  and finds nothing.

## Legality / scope note

External memory reading of a live game can violate the game's Terms of Service
and may be blocked by anti-cheat. This skill is for local, personal, read-only
tooling and learning. Do not build cheating, automation that plays the game, or
anything that modifies game memory unless the user has a clear legitimate reason.

## Reference files

- `references/memory-reading.md` — MemScanner internals: `.text` extraction,
  RIP-relative resolution, multi-match validation, pointer following.
- `references/clientstructs-mapping.md` — turning a CS `[StaticAddress]` /
  `[FieldOffset]` into reader code; a worked InventoryManager example.
- `references/game-files-lumina.md` — Lumina setup, item names, icon→BMP URIs,
  dye colors, language selection.
- `references/blazor-hybrid-ui.md` — *(optional — one UI choice)* WPF +
  BlazorWebView project layout, DI registration, per-second refresh component.
- `assets/WinApi.cs`, `assets/MemScanner.cs` — copy-in templates.
