# Rocket's Medals

A RimWorld 1.6 mod that adds award medals to the game. Craft medals at a smithy, then award them to colonists in a full Ideology-style ceremony. A leader gives a speech, the colony watches, the medal is pinned, and a letter and tale record the moment. Medals stack visually on the pawn's chest, carry small thematic stat bonuses, and over enough awards earn a colonist the `Decorated` trait. Other mods can ship their own medals without writing any C#.

## What you get in-game

### Awarding medals

Medals are physical apparel items, crafted at an Electric or Fueled Smithy by a colonist with the required Crafting skill. Each crafted medal is a real thing in your stockpile until it is awarded.

Selecting a medal exposes two gizmos on the command bar:

- **Award Ceremony**: opens the ritual flow (Ideology required; disabled if no eligible presenter is in the colony).
- **Write / Edit Citation**: opens a dictation dialog so you can write the citation up front. Citations bump ceremony quality and appear on the medal's record, the award letter, and the floating inspect string forever after.

By default, a medal must be awarded via a ceremony before it can be worn (`Require Ceremony` setting). Without a ceremony the apparel selector silently refuses it and a "needs ceremony" message is shown. If you turn that setting off, the first pawn to wear a medal is treated as the awardee and the medal biocodes to them on the spot, complete with a positive mood memory.

### The award ceremony (Ideology DLC)

When you click **Award Ceremony**, the standard Begin Ritual dialog opens with the medal preview pinned in the left column and four quality factors on the right (Medal, Venue, Attendance, Citation). You pick a venue with the ritual targeter; any room works, and outdoor spots count as zero impressiveness.

1. The presenter role is locked to a colonist holding the **Ideoligion Leader** or **Moralist** precept role. If the colony has neither, the gizmo is disabled with an explanatory tooltip.
2. The awardee can be any free colonist; you choose who is being honored.
3. Other colonists are recruited as spectators. The presenter walks to a spot behind the medal, the awardee walks in front, and the presenter delivers a short speech using the medal's icon as the speech symbol and a custom rule pack (`ROCKET_Speech_AwardMedal_Rules`).
4. The medal is bestowed: spawned medal is de-spawned, the awardee wears it, the medal is locked to the pawn (toggleable), and `awardedBy` / `awardedTick` are recorded.
5. A letter announces the ceremony, the attendees, the citation, and the quality grade. A `ROCKET_AwardedMedalTale` is recorded so the event can show up in colonist art.
6. A small awarded-medal popup confirms the result.

#### Ceremony quality

Ceremony quality is computed from three factors and stored on the medal:

- **Attendance**: up to +40% based on the fraction of free colonists who attended.
- **Venue impressiveness**: up to +40% from the room's vanilla impressiveness stat (zero outdoors).
- **Citation**: a flat +20% if a citation was written before the bestowal stage.

Quality maps to four named tiers (Poor, Decent, Grand, Legendary), which feed both the awardee's mood thought and the wording of the announcement letter.

If you start a ceremony without a citation written, the mod can pop a dictation dialog mid-ritual when the presenter begins the bestowal (toggleable in settings). Citations written this way still apply the +20% quality bonus.

### Mood and trait effects

- **Awardee thought**: `ROCKET_AwardedMedal_Thought`, 15-day memory, +4 / +8 / +14 / +20 mood depending on ceremony quality.
- **Spectator thought**: `ROCKET_WitnessedMedalCeremony_Thought`, 5-day memory, +3 mood for everyone in attendance.
- **Royal honor**: medals with `honorAwarded > 0` in their `MedalExtension` grant Empire favor on award (Royalty DLC required).
- **Decorated trait**: once Dynamic Traits is on, a colonist who reaches 3 awarded medals gains the `ROCKET_Decorated` trait at *decorated* (degree 0). At 5 medals it upgrades to *honored* (degree 1), and at 7 to *exalted* (degree 2). Each tier increases Social Impact and reduces the pawn's mental break threshold; *exalted* also boosts Negotiation Ability.
- **Per-medal trait swaps**: medals can list `addsTraits` and `removesTraits` in their mod extension, each with a chance roll. The Purple Heart may shake the Delicate trait or grant Tough; the Bronze and Silver Stars can move a pawn up the Nerves ladder; the Distinguished Service Medal carries a small chance of awakening Greedy or Jealous tendencies, etc.

All dynamic trait effects fire only if **Dynamic Traits** is enabled in settings, and conflicts with existing traits are respected so nothing gets into an impossible state.

### Locking, biocoding, and stripping

Medals have a `Pawn_ApparelTracker.IsLocked` patch: a freshly awarded medal is locked to the wearer, which blocks the outfit AI from ever taking it off. You can toggle the lock per-medal from the pawn's Medals tab.

Medals are also `CompBiocodable`. Bestowal codes the medal to the awardee permanently; coded medals cannot be worn by anyone else and survive death and stripping:

- **Corpse strip**: locked medals are re-worn by the corpse after a strip job, so they stay with the body.
- **Pawn strip**: same behavior on a live pawn. Locked medals snap back on.
- **Outfit AI**: `ApparelScoreRaw` returns -10000 for medals so the optimizer never auto-equips one in place of clothing.
- **CanWearTogether**: two medals never count as conflicting with each other; you can stack as many as you crafted.

### Medal rendering on pawns

Medals render on a dedicated `ROCKET_MedalLayer` (drawOrder 350) on the torso. The renderer:

- Centers medals on the chest with a per-body-type scale (Hulk/Fat get +15%, Thin/Female get a small shrink) and a global slider.
- Lays them out in rows of 3 (or 4 for Hulk/Fat), bottom-row centered, oldest at the top.
- Hides medals when the pawn is facing north so they don't poke through the back of the head.
- Caps how many medals are drawn (`MaxDisplayedMedals` setting); extras are still worn and counted, just not drawn.
- Forces the apparel graphic record through `Graphic_Single` with `CutoutComplex` so the colored mask textures render correctly with arbitrary `DrawColor` / `DrawColorTwo`.

The whole rendering pass can be turned off with **Draw Medals on Pawns** if you only want the social/roleplay layer.

### UI surfaces

- **Medals main button**: a small minimized button at the top of the screen opens the **Medal Catalog**, listing every medal def in the game (including modded ones). Locked entries show greyscale icons with the missing research project; unlocked entries show the stat offsets and dynamic-trait chances.
- **Medals ITab**: a new tab on every colonist (and on human corpses) lists all medals they have been awarded, with icons, presenter, date, ceremony quality, and citation. Lock state is toggleable here.
- **Medal Record ITab**: selecting a medal as a thing shows a card with the icon, name, presenter, date, ceremony quality, and citation.
- **Inspect string**: the floating string under a medal shows the same award info.

### Begin Ritual dialog tweak

The medal icon and label are drawn in the left column of the Begin Ritual dialog when the target is a `RocketMedal`, so you can see what you're about to award without scrolling.

## Settings

Settings are split across two tabs in the mod options:

| Tab | What's there |
|---|---|
| **General** | Require Ceremony (medals must be awarded via ritual, not just worn). Lock Medals upon Award (default lock-to-pawn after bestowal). Prompt for Citation during Ceremony (dictation popup mid-ritual). Dynamic Traits (master toggle for `MedalExtension` trait swaps and the `Decorated` ladder). |
| **Display** | Show Medal Catalog Button. Draw Medals on Pawns. Worn medal size slider (10%–200%). Number of medals drawn per pawn. |

## Bundled medals

| Tier | Cost | Skill | Research |
|---|---|---|---|
| Simple | 10 Silver + 10 Devilstrand | Crafting 5 | Smithing + Noble Apparel |
| Intermediate | 20 Silver + 20 Devilstrand | Crafting 6 | Smithing + Noble Apparel |
| Advanced | 10 Gold + 5 Hyperweave | Crafting 7 | Smithing + Royal Apparel |
| Legendary | 25 Gold + 50 Plasteel + 5 Hyperweave | Crafting 8 | Smithing + Royal Apparel |

| Medal | Tier | Effect |
|---|---|---|
| Medical Service Medal | Simple | +5% Surgery Success, +8% Tend Quality. |
| Research Service Medal | Simple | +5% Research Speed, +5% Drug Cooking Speed. |
| Labor Service Medal | Simple | +6% Global Work Speed. |
| Desert Campaign Medal | Simple | +2C max comfortable temperature. |
| Space Campaign Medal | Simple | +0.02 Vacuum Resistance (Odyssey). |
| Insectoid Campaign Medal | Simple | +0.02 Toxic Resistance, +0.01 Toxic Environment. |
| Polar Campaign Medal | Simple | -2C min comfortable temperature, +5 Carrying Capacity. |
| Mechanoid Campaign Medal | Simple | -5% Psychic Sensitivity, +1% Shooting Accuracy. |
| Purple Heart | Intermediate | +2% Pain Shock Threshold. May shake *Delicate* / grant *Tough*. |
| Bronze Star | Intermediate | +2% Pain Shock Threshold. May upgrade *Nerves* (Volatile → Steady). |
| Distinguished Service Medal | Advanced | +3% General Labor Speed, +3% Social Impact. Small chance to awaken *Greedy* or *Jealous*. |
| Silver Star | Advanced | +3% Pain Shock Threshold. Stronger chance to upgrade *Nerves*. |
| Relic Hunter Medallion | Legendary | +10% Entity Study Rate (Anomaly), +5% Meditation Focus Gain (Royalty). +4 Empire honor. |
| Distinguished Flying Cross | Legendary | +30% Social Impact, -5% Psychic Sensitivity. +4 Empire honor. |
| Gold Cross | Legendary | +10% Social Impact, +5% Pain Shock Threshold. +6 Empire honor. Strong chance to upgrade *Nerves* (Volatile → Iron-Willed). |

All medals add +0.1 PawnBeauty and weigh nearly nothing. Medals are not auto-selected by outfits, do not count as clothing for nudity, and can be worn together without conflict.

---

## For modders: building a medal pack

A medal is just a `ThingDef` whose `thingClass` is `MedalMod.RocketMedal` (or a subclass), inheriting from one of the bundled abstract parents so it picks up the right layer, comps, ITab, and category. Optionally add a `RecipeDef` so colonists can craft it, and a `MedalExtension` so it grants honor or fiddles with traits at award time. No C# required. Ship the pack from any mod that loads after Rocket's Medals; just declare a dependency in your About.xml so the player loads them in the right order.

### Step 1: pick a tier and declare the medal

The bundled abstract parents (`ROCKET_SimpleMedalBase`, `ROCKET_IntermediateMedalBase`, `ROCKET_AdvancedMedalBase`, `ROCKET_LegendaryMedalBase`) all inherit from `ROCKET_MedalBase`, which sets:

- `thingClass = MedalMod.RocketMedal`
- The medal apparel layer, torso body part, and category
- `CompProperties_Forbiddable` and `CompProperties_Biocodable`
- The `ITab_MedalRecord` inspector tab
- Beauty, hit points, work-to-make, equip delay, and zero deterioration

You only need to override the texture, label, description, and any stat offsets you want:

```xml
<ThingDef ParentName="ROCKET_AdvancedMedalBase">
    <defName>MyMod_Medal_NightWatch</defName>
    <label>night watch medal</label>
    <description>Awarded for vigilance during the long hours when others sleep.</description>
    <graphicData>
        <texPath>Things/MyMod/Medals/NightWatch</texPath>
    </graphicData>
    <apparel>
        <wornGraphicPath>Things/MyMod/Medals/NightWatch</wornGraphicPath>
    </apparel>
    <equippedStatOffsets>
        <ShootingAccuracyPawn>0.02</ShootingAccuracyPawn>
        <RestRateMultiplier>0.05</RestRateMultiplier>
    </equippedStatOffsets>
    <uiOrder>30</uiOrder>
</ThingDef>
```

The graphic must be a single texture with optional masks (`CutoutComplex`). The same path is used for both the world-spawn icon and the worn graphic on the pawn, so keep them visually compatible with the medal layout (small, centered, transparent background).

### Step 2 (optional): add a recipe

Inherit one of the tier recipe parents to pick up the right cost, skill, and research gates:

```xml
<RecipeDef ParentName="ROCKET_AdvancedMedalRecipeBase">
    <defName>MyMod_Recipe_NightWatch</defName>
    <label>craft night watch medal</label>
    <jobString>Crafting night watch medal.</jobString>
    <products>
        <MyMod_Medal_NightWatch>1</MyMod_Medal_NightWatch>
    </products>
</RecipeDef>
```

| Recipe parent | Skill | Ingredients | Research |
|---|---|---|---|
| `ROCKET_SimpleMedalRecipeBase` | Crafting 5 | 10 Silver + 10 Devilstrand | Smithing + NobleApparel |
| `ROCKET_IntermediateMedalRecipeBase` | Crafting 6 | 20 Silver + 20 Devilstrand | Smithing + NobleApparel |
| `ROCKET_AdvancedMedalRecipeBase` | Crafting 7 | 10 Gold + 5 Hyperweave | Smithing + RoyalApparel |
| `ROCKET_LegendaryMedalRecipeBase` | Crafting 8 | 25 Gold + 50 Plasteel + 5 Hyperweave | Smithing + RoyalApparel |

Skip this step entirely if your medals are meant to be awarded by quest rewards or scripted events instead of being craftable.

### Step 3 (optional): MedalExtension for honor and trait effects

Attach a `MedalExtension` mod extension to grant Empire favor on award or to roll for trait additions/removals during the ceremony:

```xml
<modExtensions>
    <li Class="MedalMod.MedalExtension">
        <honorAwarded>4</honorAwarded>
        <removesTraits>
            <li>
                <trait>Nerves</trait>
                <degree>-1</degree>
                <chance>0.5</chance>
            </li>
        </removesTraits>
        <addsTraits>
            <li>
                <trait>Nerves</trait>
                <degree>2</degree>
                <chance>0.1</chance>
            </li>
        </addsTraits>
    </li>
</modExtensions>
```

#### MedalExtension fields

| Field | Default | Meaning |
|---|---|---|
| `honorAwarded` | `0` | Empire favor granted on bestowal. Only fires if Royalty is loaded and the Empire faction exists. |
| `addsTraits` | `null` | List of `MedalDynamicTrait` entries to attempt to grant. Conflicts with existing traits are respected; duplicates are skipped. |
| `removesTraits` | `null` | List of `MedalDynamicTrait` entries to attempt to remove. The exact `def + degree` must already be on the pawn. |

#### MedalDynamicTrait fields

| Field | Default | Meaning |
|---|---|---|
| `trait` | required | The `TraitDef` to add or remove. Vanilla and modded traits both work. |
| `degree` | `0` | The trait degree. For multi-degree traits like `Nerves`, this is the level you want to land on (or peel off). |
| `chance` | `1.0` | Per-award probability the entry fires. Use this for "redemptive" medals where the effect is flavorful but not guaranteed. |

All `MedalExtension` effects fire after the bestowal apply step, so the medal is already worn when the rolls happen. If the player has Dynamic Traits turned off in settings, all `addsTraits` / `removesTraits` and the `Decorated` trait ladder are skipped; `honorAwarded` still applies.

### What the player can override

- The two display toggles (catalog button, drawing on pawns) and the worn-medal size/count sliders are global and apply to your medals.
- **Require Ceremony** applies globally, so if the player turns it off your medals also auto-biocode to the first wearer.
- **Lock Medals upon Award** applies globally. Once awarded, your medals stay on the pawn unless the player unlocks them on the Medals tab.
- **Dynamic Traits** is the player's escape hatch for the trait side-effects on your medals. The medal still gets awarded; just the `addsTraits` / `removesTraits` rolls (and the `Decorated` ladder) are skipped.

### Custom medal classes

If you need behavior beyond what the def covers (a medal that ticks, periodically affects its wearer, or hooks new gizmos), subclass `MedalMod.RocketMedal` and point your `thingClass` at it. The bundled patches all use `IsAssignableFrom`, so subclasses inherit the locking, biocoding, rendering, and outfit-AI behavior automatically.

---

## Project layout

```
About/                      Mod metadata; depends on Harmony.
1.6/
  Assemblies/               Shipped DLL.
  Defs/
    Category_Def.xml            ROCKET_Apparel_Medals category.
    Interactions_Medals.xml     Award speech interaction, rule pack, and ROCKET_AwardedMedalTale.
    MainButton_Def.xml          The Medals catalog main button.
    Recipe_Def.xml              Tier recipe parents and bundled medal recipes.
    Ritual_Def.xml              Award ceremony pattern, behavior, outcome, and presenter duty.
    Thing_Def.xml               Apparel layer, base classes, tier parents, and the bundled medals.
    Thought_Def.xml             Awardee/witness ceremony thoughts.
    Trait_Def.xml               ROCKET_Decorated trait at decorated/honored/exalted.
  Patches/                  Currently empty placeholder.
Source/                      C# source (csproj + .cs files).
Languages/English/Keyed/     Translatable strings.
Textures/                    Medal textures, UI buttons, and catalog icons.
```

## Building from source

1. Open `Source/MedalMod/MedalMod.csproj` in Rider or Visual Studio.
2. The csproj points at RimWorld via a property at the top; edit it if your install isn't at the default Steam location.
3. Build. Output goes to `1.6/Assemblies/`.

Target framework `net48`, language version `latest`.

## Dependencies

- [Harmony](https://steamcommunity.com/sharedfiles/filedetails/?id=2009463077)
- [Ideology DLC] (optional): required only for the award ceremony. Without it the rest of the mod functions normally; the **Award Ceremony** gizmo is shown but disabled, and medals can still be biocoded by being worn (if **Require Ceremony** is off).
- [Royalty DLC] (optional): required for the `honorAwarded` field on `MedalExtension`. Without it, honor grants are silently skipped.
