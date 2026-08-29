# Memory reading internals

How `MemScanner` (in `assets/MemScanner.cs`) finds and reads live data. Read this
when a reader returns nothing/garbage, or when you need to adapt the skeleton.

## Why signature scanning, not hardcoded addresses

Every game patch shifts where data lives, so absolute addresses break constantly.
Instead we scan the game's code (`.text` section) for a short byte pattern of a
machine instruction that *references* the data, then decode the address out of
that instruction. The instruction bytes are stable across patches far more often
than addresses are. This is exactly what FFXIVClientStructs' `[StaticAddress]`
attributes encode.

## The steps

1. **Open the process** with `OpenProcess(0x0010, …)` (`PROCESS_VM_READ`).
   Requires running as Administrator (add a `requireAdministrator` app.manifest).

2. **Locate `.text`.** Read the first 0x800 bytes (PE header), scan for the
   `.text` section marker (`0x747865742E`), and pull its virtual address offset
   and size. `.text` base = module base + that offset.

3. **Read `.text` into a local `byte[]`** and scan it for the signature. `null`
   entries in the `byte?[]` sig are wildcards (the `??` in a CS pattern).

4. **Resolve the RIP-relative address.** The matched instruction is usually
   `lea reg, [rip+disp32]` or `mov reg, [rip+disp32]`. The 4-byte `disp32` sits at
   a known offset inside the pattern (the wildcard bytes). The target address is:

   ```
   target = textBase + matchIndex + dispIndex + 4 + disp32
   ```

   `dispIndex + 4` is where the *next* instruction begins, which is what RIP
   points to when the CPU adds the displacement. This equals the second argument
   of `[StaticAddress(sig, dispIndex)]` in FFXIVClientStructs.

5. **Follow pointers.** The resolved address is typically a *static field holding
   a pointer* to the manager instance, so dereference once to get the instance,
   then add field offsets. `Follow(start, offsets…)` derefs at each step.

## Gotcha 1: short signatures have multiple matches

A 9-byte pattern like `48 8D 0D ?? ?? ?? ?? 81 C2` appears many times in `.text`.
The first match is usually the wrong instruction, resolving to a bogus address
whose "struct" is garbage — the reader silently finds nothing.

**Fix:** enumerate every match (`ResolveRipAll`), and for each candidate (and its
one-deref value) score it against the expected struct shape; keep the best. A good
validator reads a few entries of the target array and counts how many look
plausible (type in the expected small range, size within bounds, sub-pointers in
a sane heap range, e.g. `> 0x10000`). Example locate loop:

```csharp
private IntPtr Locate()
{
    IntPtr best = IntPtr.Zero; var bestScore = 0;
    foreach (var target in ResolveRipAll(sig))
        foreach (var cand in new[] { target, (IntPtr)Read<ulong>(target) })
        {
            var score = ScoreAsExpectedStruct(cand);
            if (score > bestScore) { bestScore = score; best = cand; }
        }
    if (best == IntPtr.Zero) throw new InvalidOperationException("找不到目標（特徵碼或 offset 需更新）");
    return best;
}
```

Trying both `target` and `deref(target)` also removes the guesswork about whether
the static holds the instance inline or a pointer to it.

## Gotcha 2: `T*` fields are pointers

In FFXIVClientStructs a field typed `T*` (e.g. `InventoryContainer* Inventories`)
means the struct stores an 8-byte pointer at that offset, not the data inline.
`instance + offset` gives you the *pointer's storage*; read the 8 bytes to get the
real address, then index. Only fields typed as an inline value or a
`FixedSizeArray<T>` are embedded directly. Getting this wrong reads adjacent
garbage and the reader finds nothing.

## Reading a struct block efficiently

For an array of N structs each `stride` bytes: read the whole
`N * stride` block in one `ReadProcessMemory` call, then parse fields out of the
local `byte[]` with `BitConverter` / `MemoryMarshal`. Far fewer syscalls than
reading field-by-field. Skip entries whose key field (e.g. ItemId) is 0.

## Detecting change cheaply

For a polling UI, keep the previous snapshot and skip re-rendering when the new
read equals it (`SequenceEqual` on a record list, or compare a trailing sentinel
byte). Avoids rebuilding the UI 60×/minute for unchanged data.
