# eorzea-reader

**English** | [中文](#中文)

A portable **skill** for AI coding agents (Claude Code, Codex, and the like) —
for building tools that read **Final Fantasy XIV** data: live state from process
memory, and static data from the game's files. It's just Markdown plus a couple
of C# templates, so any agent that loads skills can use it.

It packages a working, patch-resilient approach:

- **Live state** (inventory, gear, party, character…) via external
  `ReadProcessMemory`, located with **FFXIVClientStructs** signatures and struct
  offsets — no hand reverse-engineering.
- **Static data** (item names, icons, dye colors…) via **Lumina** reading the
  game's `sqpack` files.

The skill also records the two non-obvious failures that cost the most time:
short signatures matching in multiple places (validate every match), and
FFXIVClientStructs `T*` fields being pointers you must dereference.

**Why external reading, not a Dalamud plugin?** Dalamud injects into the game and
gives you typed objects with far less work — use it when you need to act in-game
or want the easy path. This external approach is for a **standalone, read-only**
app with your own UI and stack, that injects no code and needs no XIVLauncher/
Dalamud. (Both are against the game's ToS — see below.)

## What's inside

```
eorzea-reader/
├── SKILL.md                          # entry point: workflow + the two gotchas
├── assets/
│   ├── WinApi.cs                     # copy-in P/Invoke (OpenProcess/ReadProcessMemory)
│   └── MemScanner.cs                 # copy-in skeleton: .text scan, RIP resolve, pointer follow
└── references/
    ├── memory-reading.md             # MemScanner internals + validation strategy
    ├── clientstructs-mapping.md      # CS [StaticAddress]/[FieldOffset] → reader code
    ├── game-files-lumina.md          # Lumina: names, icon→BMP data URI, dyes, language
    └── blazor-hybrid-ui.md           # WPF + BlazorWebView project layout & polling component
```

## Install

**Claude Code** — clone into a skills directory as a folder named `eorzea-reader`:

```bash
# personal (all projects)
git clone https://github.com/<you>/eorzea-reader.git ~/.claude/skills/eorzea-reader

# or per-project
git clone https://github.com/<you>/eorzea-reader.git .claude/skills/eorzea-reader
```

It's discovered on next start. Invoke with `/eorzea-reader`, or just ask about
reading FFXIV memory/game data and it loads automatically.

**Other agents (Codex, etc.)** — it's plain Markdown, so point your agent at
`SKILL.md` (and the `references/` files it links), or drop the folder wherever
that tool looks for skills.

## Credits

Built entirely on the work of:

- [FFXIVClientStructs](https://github.com/aers/FFXIVClientStructs) — the
  reverse-engineered signatures and struct layouts.
- [Lumina](https://github.com/NotAdam/Lumina) — reading FFXIV game files.

## Legal / scope

For **local, personal, read-only** tooling and learning. External memory reading
of a live game can violate its Terms of Service and may be blocked by anti-cheat.
Do not use this to cheat, automate gameplay, or modify game memory.

FINAL FANTASY XIV © SQUARE ENIX. This project is unaffiliated fan tooling.

## License

MIT — see [LICENSE](LICENSE).

---

# 中文

[English](#eorzea-reader) | **中文**

一個給 AI 編碼 agent（Claude Code、Codex 等）用的 **skill**，教你寫工具去讀
**Final Fantasy XIV** 的資料：一種是遊戲執行中的即時狀態，一種是遊戲檔案裡的靜態資料。
內容就是 Markdown 加幾個 C# 範本，任何吃 skill 的 agent 都能用。

它把一套實際能用、又扛得住改版的做法收在一起：

- **即時狀態**（背包、裝備、隊伍、角色⋯⋯）：在遊戲外部用 `ReadProcessMemory` 讀它的記憶體，
  位址靠 **FFXIVClientStructs** 的特徵碼和 struct offset 定位，不用自己逆向。
- **靜態資料**（物品名稱、圖示、染料顏色⋯⋯）：用 **Lumina** 讀遊戲的 `sqpack` 檔。

Skill 裡也寫下兩個最花時間的坑：短特徵碼會在 `.text` 多處命中（每個都要驗證再挑），
還有 FFXIVClientStructs 的 `T*` 欄位是指標、要多解一層才拿得到資料。

**為什麼走外部讀取，不做 Dalamud 外掛？** Dalamud 是注入到遊戲裡、直接給你型別化的物件，
省事很多 —— 要在遊戲內做事、或想走輕鬆路線就用它。這套外部做法是為了做一個**獨立、唯讀**、
有自己 UI 和技術棧的程式：不注入任何程式碼，也不需要 XIVLauncher／Dalamud。
（兩種做法都違反遊戲服務條款，見下方。）

## 內容

```
eorzea-reader/
├── SKILL.md                          # 入口：開發流程 ＋ 兩個雷
├── assets/
│   ├── WinApi.cs                     # 可直接複用的 P/Invoke（OpenProcess/ReadProcessMemory）
│   └── MemScanner.cs                 # 可直接複用的骨架：掃 .text、解 RIP 位址、跟隨指標
└── references/
    ├── memory-reading.md             # MemScanner 內部原理 ＋ 多重命中驗證
    ├── clientstructs-mapping.md      # CS [StaticAddress]/[FieldOffset] → reader 程式碼
    ├── game-files-lumina.md          # Lumina：名稱、圖示→BMP、染料、語言
    └── blazor-hybrid-ui.md           # WPF + BlazorWebView 專案佈局與輪詢元件
```

## 安裝

**Claude Code** —— 把這個 repo 複製到 skills 目錄，資料夾名稱用 `eorzea-reader`：

```bash
# 個人（所有專案共用）
git clone https://github.com/<你的帳號>/eorzea-reader.git ~/.claude/skills/eorzea-reader

# 或只給單一專案
git clone https://github.com/<你的帳號>/eorzea-reader.git .claude/skills/eorzea-reader
```

下次啟動就會載入。打 `/eorzea-reader` 叫它，或直接問關於讀 FFXIV
記憶體／遊戲資料的問題，它會自己載入。

**其他 agent（Codex 等）** —— 內容都是純 Markdown，把 agent 指向 `SKILL.md`
（以及它連到的 `references/`）即可，或把資料夾放到那個工具找 skill 的位置。

## 致謝

這個 skill 完全建立在這兩個專案之上：

- [FFXIVClientStructs](https://github.com/aers/FFXIVClientStructs)：逆向好的特徵碼與 struct 佈局。
- [Lumina](https://github.com/NotAdam/Lumina)：讀 FFXIV 遊戲檔案。

## 法律與使用範圍

這是給**本機、個人、唯讀**的工具和學習用的。對執行中的遊戲做外部記憶體讀取，
可能違反遊戲的服務條款，也可能被反作弊擋下。請不要拿它作弊、自動化代打，
或去改遊戲記憶體。

FINAL FANTASY XIV © SQUARE ENIX，本專案為非官方的同好工具。

## 授權

MIT，見 [LICENSE](LICENSE)。
