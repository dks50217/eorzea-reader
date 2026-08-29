# Mapping FFXIVClientStructs to reader code

FFXIVClientStructs (CS) is the source of truth for signatures and struct layouts.
Never reverse-engineer by hand if CS already has the struct.

- Repo: https://github.com/aers/FFXIVClientStructs
- Searchable API docs: https://ffxiv.wildwolf.dev/
- Struct files live under `FFXIVClientStructs/FFXIV/Client/...`. Read the raw `.cs`
  to get exact attribute strings and `[FieldOffset]` values.

## The two attributes you copy

**`[StaticAddress("bytes", dispIndex)]`** on an `Instance()` method — this is the
singleton locator. `"bytes"` is your `byte?[]` sig (turn each `??` into `null`).
`dispIndex` is where the RIP disp32 starts inside the pattern; it equals the index
of your first `null`. Feed the sig to `MemScanner.ResolveRipAll`.

**`[FieldOffset(0x..)] Type Name;`** inside the struct — the offset of each field.
Copy the ones you need. Watch the type:
- inline value (`uint`, `short`, `InventoryType`) → read directly at `base+offset`.
- `T*` → pointer, dereference once (see gotcha 2 in `memory-reading.md`).
- `FixedSizeArray<T>` → inline array embedded at that offset.

`struct` size (from `[StructLayout(Size = 0x..)]` or the docs) is the stride when
the field is an array.

## Worked example: InventoryManager → bag contents

From CS at the time of writing:

```
InventoryManager:
  [StaticAddress("48 8D 0D ?? ?? ?? ?? 81 C2", 3)] Instance()
  [FieldOffset(0x1E08)] public InventoryContainer* Inventories;   // POINTER

InventoryContainer (size 0x20):
  [FieldOffset(0x08)] InventoryItem* Items;
  [FieldOffset(0x10)] InventoryType Type;   // int
  [FieldOffset(0x14)] int Size;
  [FieldOffset(0x18)] bool IsLoaded;

InventoryItem (size 0x48):
  [FieldOffset(0x0C)] short Slot;
  [FieldOffset(0x10)] uint ItemId;
  [FieldOffset(0x14)] int Quantity;
  [FieldOffset(0x1C)] ItemFlags Flags;   // byte; HighQuality = 1
  [FieldOffset(0x37)] FixedSizeArray2<byte> Stains;  // 2 dye ids
  [FieldOffset(0x3C)] uint GlamourId;

InventoryType: Inventory1..4 = 0..3  (the four player bags)
```

Which becomes:

```csharp
var instance      = Locate();                                  // sig → validated instance
var containersBase = (IntPtr)Read<ulong>(instance + 0x1E08);   // deref the T* Inventories
for (var idx = 0; idx < 100; idx++)                            // scan array, filter by Type
{
    var c        = Read(containersBase + idx * 0x20, 0x20);
    var itemsPtr = (IntPtr)BitConverter.ToUInt64(c, 0x08);
    var type     = BitConverter.ToInt32(c, 0x10);
    var size     = BitConverter.ToInt32(c, 0x14);
    if (type is < 0 or > 3 || itemsPtr == IntPtr.Zero || size is <= 0 or > 200) continue;

    var block = Read(itemsPtr, size * 0x48);                   // one read for the whole container
    for (var s = 0; s < size; s++)
    {
        var b  = s * 0x48;
        var id = BitConverter.ToUInt32(block, b + 0x10);
        if (id == 0) continue;
        // Slot=Int16@0x0C, Qty=Int32@0x14, HQ=(block[b+0x1C]&1), dyes=block[b+0x37..0x38], glamour=UInt32@0x3C
    }
}
```

## Adapting to other data

Same recipe, different struct. To read a different manager:

1. Find its struct file in CS, copy the `[StaticAddress]` sig and needed offsets.
2. Subclass `MemScanner`, resolve+validate the sig in the constructor.
3. Deref any `T*` fields; read inline fields directly.

Common starting points: `InventoryManager` (bags, retainer, equipped),
`PlayerState` / `UIState` (character, currencies, unlocks), and the object table
for entities in the zone. The exact names/offsets change — always read current CS,
not this doc, for the values.

## When it breaks after a patch

A signature that stops resolving, or offsets that read garbage, means CS was
updated. Pull the latest CS values and replace your sig/offsets. If a short sig
resolves to the wrong place, you likely need the multi-match validation, not a
new sig.
