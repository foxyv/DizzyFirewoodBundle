# Dizzy Firewood Bundle

A [Sailwind](https://store.steampowered.com/app/1764530/Sailwind/) mod for [BepInEx 5](https://thunderstore.io/c/sailwind/p/BepInEx/BepInExPack/) that bundles loose firewood, candles, sausages, bananas, apples, oranges and fishing hooks together.

## Features

### Firewood
- Hold a log and right-click another log to tie them into a bundle. A bundle of two or more is carried with two hands.
- Right-click a bundle to take one log off.
- Click a stove with a bundle to feed it one log. The rest stay in your hands.
- Look at a loose log in an open crate and press **G** to pick up a bundle of the crate's loose logs.
- Press **G** on a loose log lying around to pick up a bundle of the loose logs within 3 m.

### Candles
- Hold a candle and right-click another candle to tie them into a bundle with a red ribbon and a bow.
- Click a candle lantern with a bundle to load one candle. A lantern that is already full is skipped.
- Right-click a bundle to take one candle off. Press **G** on a loose candle in an open crate to pick up a bundle.

### Tie colors
- Hold or look at a firewood or candle bundle and press **\\** to change its tie color.
- Candles: red, blue, green, gold, white, black, purple, pink and orange. Firewood: its natural brown, then the same nine. Apple and orange bags: the same nine, starting from black.

### Sausages
- Hold a smoked or dried sausage and right-click another of the same kind to stack them.
- Only sausages that won't spoil can stack, and only with the same kind. Smoked sausages don't stack with salted ones.
- To eat, cook, salt or slice a sausage, take it off the stack first with a right-click. It keeps the stack's cook level and preservation.
- Press **]** while holding or looking at a stack to change its shape:
  - **Auto:** a block about as close to square as the count allows, tied with brown cord.
  - **1 to 5:** that many sausages wide. **1** is a single tower.
  - **Tree:** crossing layers that narrow toward the top.
  - **Pile:** a loose heap. New sausages land on top without moving the others.
- Press **G** on a loose sausage in an open crate to pick up a stack.

### Hanging sausages
- Click an empty lamp hook with a smoked or dried sausage to hang it. Sausages that would spoil can't be hung.
- Right-click the hanging bundle with a sausage or a stack to add more. A stack moves as many as fit. Kinds don't mix.
- Sausages hang in bunches around a central string, each on its own short white string. When a bunch is full, a new one starts lower down.
- Right-click the bundle to take one sausage off. Taking the last one frees the hook.
- Left-click lifts the whole bundle, which can be hung on another empty lamp hook.

### Banana bunches
- Click an empty lamp hook with a dried banana to hang it. Fresh bananas can't be hung.
- Right-click the bunch with a dried banana to add it. Bananas grow from a stalk in hands, like a bunch on the tree turned upside down.
- Right-click the bunch to take one banana off. Taking the last one frees the hook.
- Left-click lifts the whole bunch, which can be hung on another empty lamp hook.
- Hold a bunch and right-click a loose dried banana to add it to the bunch.
- Press **G** on a dried banana in an open crate to pick up a bunch of the crate's dried bananas.
- Press **G** on a loose dried banana lying around to pick up a bunch of the dried bananas within 3 m.

### Apple bags
- Hold a dried apple and right-click another to put them in a net bag, or click an empty lamp hook with a dried apple to start a hanging bag. Fresh apples can't go in.
- Right-click the bag with a dried apple to add it, or hold the bag and right-click loose dried apples.
- Apples settle in layers at the bottom of the net, and the net is tied at the neck. A bag hangs from a lamp hook or sits on the deck.
- Right-click the bag with empty hands to take an apple out. Press **\\** to change the net's color.
- Press **G** on a dried apple in an open crate, or on a loose one lying around, to pick up a bag of the dried apples there.

### Orange bags
- Dried oranges go in net bags exactly like apples: right-click two together, or click an empty lamp hook with one.
- Add, take out, hang, recolor and press **G** on oranges the same way. Apples and oranges never share a bag.
- A bag keeps at least three rows of fruit, and stands upright when set down.

### Fishing hooks
- Hold a fishing hook and right-click another hook to string them on a line.
- A line can hang from a lamp hook. An empty fishing rod takes one hook from a line.
- Press **G** on a loose hook in an open crate to string the crate's loose hooks.

### Topping up
- Right-clicking a nearly full bundle, stack or line with one in your hands moves just enough over to fill it. The rest stay in your hands.

## Install

1. Install [BepInEx 5](https://thunderstore.io/c/sailwind/p/BepInEx/BepInExPack/) for Sailwind.
2. Download `Dizzy.FirewoodBundle-<version>.zip` from [Releases](https://github.com/foxyv/DizzyFirewoodBundle/releases).
3. Extract the `Dizzy.FirewoodBundle` folder into `Sailwind\BepInEx\plugins\`.
4. Launch the game.

## Configuration

Settings are in `BepInEx\config\com.dizzy.sailwind.firewoodbundle.cfg`. The file is created the first time the game runs with the mod.

| Section | Setting | Default | What it does |
|---|---|---|---|
| General | Stack Firewood | true | Turns firewood bundles on or off. |
| General | MaxPieces | 100 | Most logs in a bundle. |
| General | Tie Color Key | Backslash | Key that changes a bundle's tie color. |
| Candles | Enabled | true | Turns candle bundles on or off. |
| Candles | MaxCandles | 24 | Most candles in a bundle, up to 999. |
| Candles | Spacing | 0.9 | Gap between candles in a bundle. |
| Sausages | Enabled | true | Turns sausage stacks on or off. |
| Sausages | MaxSausages | 20 | Most sausages in a stack, up to 999. |
| Sausages | Width Key | RightBracket | Key that changes a stack's shape. |
| Sausages | Spacing | 0.75 | How tightly sausages pack. Lower is tighter. |
| Sausages | Pile Steepness | 1 | How steep a Tree or Pile grows. 0.5 is flat, 2 is steep. |
| Hanging Sausages | Enabled | true | Turns hanging sausages on or off. |
| Hanging Sausages | MaxSausages | 48 | Most sausages in a hanging bundle, up to 999. |
| Hanging Sausages | Bunch Size | 12 | Sausages around the string in one bunch. |
| Hanging Sausages | Bunch Radius | 0.75 | How far sausages hang from the central string. |
| Hanging Sausages | Bunch Drop | 2 | How far below the last bunch a new one starts. |
| Hanging Sausages | String Length | 1 | Length of each sausage's string. |
| Hanging Sausages | Flare | 0.75 | How far sausages lean out. 1 just clears the bunch below. 0 hangs straight down. |
| Banana Bunches | Enabled | true | Turns banana bunches on or off. |
| Banana Bunches | MaxBananas | 45 | Most bananas in a bunch, up to 999. |
| Banana Bunches | Hand Size | 8 | Bananas around the stalk in one hand. |
| Banana Bunches | Hand Drop | 1 | How far below the last hand a new one starts. |
| Banana Bunches | Splay | 35 | Degrees the top hand points out from straight down. Lower hands point steeper. |
| Apple Bags | Enabled | true | Turns apple bags on or off. |
| Apple Bags | MaxApples | 24 | Most apples in a bag, up to 999. |
| Apple Bags | Layer Size | 7 | Apples in the bottom layer. Each layer above holds one fewer. |
| Orange Bags | Enabled | true | Turns orange bags on or off. |
| Orange Bags | MaxOranges | 24 | Most oranges in a bag, up to 999. |
| Orange Bags | Layer Size | 7 | Oranges in the bottom layer. Each layer above holds one fewer. |
| Fruit Bags | Net Thickness | 1.5 | How thick the strings of apple and orange bag nets are. |
| Hooks | Enabled | true | Turns hook lines on or off. |
| Hooks | MaxHooks | 20 | Most hooks on one line. |
| Hooks | Hook Messiness | 1 | How far each hook is shifted and tilted off a neat line. |
| Hooks | Hook Flare | 1 | How far hook points swing out from the string. |
| Hooks | Spacing | 1 | Gap between hooks on a line. |
| Hooks | Line Length | 1 | How far hooks hang below the string. |

## Saves

Bundles, stacks and lines are saved in fields the game already saves for each item, so no extra save files are written.

If you remove the mod, each saved bundle, stack or line loads as a single piece and the rest are lost. Take them apart first if you want to keep everything.

## Building

Requirements:
- .NET SDK
- a local Sailwind install. The project references the game's `Assembly-CSharp.dll`.

The project expects Sailwind at `D:\SteamLibrary\steamapps\common\Sailwind`. Pass `-p:SailwindDir=...` if yours is somewhere else.

```powershell
dotnet build src\Dizzy.FirewoodBundle\Dizzy.FirewoodBundle.csproj -c Release -p:DeployOnBuild=false
```

`-p:DeployOnBuild=false` keeps the build from copying the DLL into the game folder.

To package a release zip into `dist\`, run:

```powershell
.\scripts\package-release.ps1 -Version 0.9.0
```

## License

See [LICENSE](LICENSE).
