# Almafa64's [Bopl Battle](https://store.steampowered.com/app/1686940/Bopl_Battle/) Mods

All of my mods for [Bopl Battle](https://store.steampowered.com/app/1686940/Bopl_Battle/) using [BepInEx](https://github.com/BepInEx/BepInEx).

- [Almafa64's Bopl Battle Mods](#almafa64s-bopl-battle-mods)
  - [Mods info](#mods-info)
  - [Installation](#installation)
      - [After BepInEx installed](#after-bepinex-installed)
  - [Building](#building)

## Mods info
All mods need **BepInEx** Version **5.4.22**
- **BoplBattleTemplate**: Base of all my mods. More advanced version of [shad0w_dev's](https://discord.com/channels/1175164882388275310/1177300281705365676/1177333041048334336) (thanks shad0w_dev)
- **BoplTranslator** (1.1.0): Custom translation support.
- **BoplModSyncer** (1.0.0): Checks if every client has mods (maybe downloads missing).
- **AtomGrenade** (1.0.1): Set power in config. Changes texture if power is bigger than normal.
- **NoMapBounds** (1.0.0): Removes map bounds.
- **NoCloneCap** (1.0.0): Removes max clone count
- **WhoseThisTeleport** (1.0.0): Shows whose teleport it is and with what button was it called
- **NoAutoShrink** (1.0.0): Stops the game from auto shrinking big platforms

## Installation
Need help to install BepInEx?<br>
Click [this link](https://docs.bepinex.dev/articles/user_guide/installation/index.html) to get started!

#### After BepInEx installed
1. Get mods from [release page](https://github.com/almafa64/almafa64-bopl-mods/releases)
2. Follow the Installation instruction on mod release page<br>

## Building
1. Clone repo
1. Start setup.cmd and follow the instructions
1. **Important**: The solution uses DLLs from the **installed** BepInEx, so install it before step 5
1. Start Almafa64BoplMods.sln
1. Build the mod you would like
1. Mod DLL is at &lt;Mod name&gt;\\bin\\&lt;Release/Debug&gt;\\net46\\&lt;Mod name&gt;.dll (it will be copied to GameFolder\\BepInEx\\plugins\\&lt;Mod name&gt;)