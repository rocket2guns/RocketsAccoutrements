using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;
using UnityEngine;
using Verse.AI;
using Verse.AI.Group;
using Verse.Grammar;

namespace MedalMod
{
    [StaticConstructorOnStartup]
    public static class MedalTextures
    {
        public static readonly Texture2D CitationIcon = 
            ContentFinder<Texture2D>.Get("UI/ButtonCitation");
        
        public static readonly Texture2D LockedIcon = ContentFinder<Texture2D>.Get("UI/Locked");
        public static readonly Texture2D UnlockedIcon = ContentFinder<Texture2D>.Get("UI/Unlocked");
        public static readonly Texture2D HonorIcon = ContentFinder<Texture2D>.Get("UI/Icons/RoyalFavor");
    }
    
    public class MainButtonWorker_MedalCatalog : MainButtonWorker
    {
        public override bool Visible => MedalMod.Settings.ShowMedalCatalog;

        public override void Activate()
        {
            Find.WindowStack.Add(new Dialog_MedalCatalog());
        }
    }
    

    public class MedalDynamicTrait
    {
        public TraitDef trait;
        public int degree = 0;
        public float chance = 1.0f;

        /// <summary>
        /// Readable label for messages, pulling from the specific degree data.
        /// Falls back to the def's label if the degree isn't found.
        /// </summary>
        public string Label
        {
            get
            {
                var data = trait?.DataAtDegree(degree);
                return data?.label ?? trait?.defName ?? "unknown";
            }
        }
    }

    public class MedalExtension : DefModExtension
    {
        public int honorAwarded = 0;
        public List<MedalDynamicTrait> addsTraits;
        public List<MedalDynamicTrait> removesTraits;
    }
    
    public class MedalMod : Mod
    {
        public static MedalModSettings Settings;

        // Constructor runs when the mod is loaded
        public MedalMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<MedalModSettings>();
        }

        public override string SettingsCategory() => "Rocket's Medals";

        private enum SettingsTab { General, Display }
        private static SettingsTab currentTab = SettingsTab.General;
        private static readonly List<TabRecord> tabBuf = new();

        public override void DoSettingsWindowContents(Rect inRect)
        {
            const float tabBarHeight = 32f;
            var contentRect = new Rect(inRect.x, inRect.y + tabBarHeight, inRect.width, inRect.height - tabBarHeight);

            tabBuf.Clear();
            tabBuf.Add(new TabRecord("General", () => currentTab = SettingsTab.General, currentTab == SettingsTab.General));
            tabBuf.Add(new TabRecord("Display", () => currentTab = SettingsTab.Display, currentTab == SettingsTab.Display));

            Widgets.DrawMenuSection(contentRect);
            TabDrawer.DrawTabs(contentRect, tabBuf);

            var inner = contentRect.ContractedBy(12f);
            switch (currentTab)
            {
                case SettingsTab.General: DrawGeneralTab(inner); break;
                case SettingsTab.Display: DrawDisplayTab(inner); break;
            }
        }

        private static void DrawGeneralTab(Rect rect)
        {
            var listing = new Listing_Standard();
            listing.Begin(rect);
            Text.Font = GameFont.Small;

            listing.CheckboxLabeled(
                "Require Ceremony",
                ref Settings.MedalsRequireCeremony,
                "If enabled, medals can only be awarded to colonists via an award ceremony. If disabled, medals will be considered awarded by the first person to wear them."
            );
            listing.CheckboxLabeled(
                "Lock Medals upon Award",
                ref Settings.LockMedalsUponAward,
                "If enabled, once a medal is awarded to a pawn, it will default to being locked to the pawn inventory. This can be toggled individually on the dedicated tab. This setting does not affect biocoding."
            );
            listing.CheckboxLabeled(
                "Prompt for Citation during Ceremony",
                ref Settings.PromptForCitationDuringRitual,
                "If enabled, a dialog will appear during award ceremonies to allow the user to enter a citation for the medal if one has not already been added to the medal. If disable a window will not appear, but citations can still be added prior to ceremony commencing."
            );
            listing.CheckboxLabeled(
                "Dynamic Traits",
                ref Settings.MedalDynamicTraits,
                "If enabled, medals can have a dynamic effect on traits such as gaining a decorated trait, or greedy, or losing wimp when receiving bravery awards."
            );

            listing.End();
        }

        private static void DrawDisplayTab(Rect rect)
        {
            var listing = new Listing_Standard();
            listing.Begin(rect);
            Text.Font = GameFont.Small;

            listing.CheckboxLabeled(
                "Show Medal Catalog Button",
                ref Settings.ShowMedalCatalog,
                "If enabled, a menu button will show to open a catalog of all medals in the game, including those added by mods. The catalog shows stats and effects of each medal."
            );
            listing.CheckboxLabeled(
                "Draw Medals on Pawns",
                ref Settings.DrawMedalsOnPawns,
                "If enabled, medals will be rendered on pawns. Disabling this will stop medals being drawn on pawns. Use this to turn their rendering off."
            );

            listing.Gap(10f);
            listing.GapLine();
            SubHeader(listing, "Worn Medals");

            listing.Label($"Worn size: {Settings.MedalScale.ToStringPercent()}");
            Settings.MedalScale = listing.Slider(Settings.MedalScale, 0.1f, 2.0f);
            listing.Label($"Displayed medals: {Settings.MaxDisplayedMedals.ToStringCached()}");
            listing.IntAdjuster(ref Settings.MaxDisplayedMedals, 1);

            listing.End();
        }

        private static void SubHeader(Listing_Standard listing, string text)
        {
            Text.Font = GameFont.Medium;
            listing.Label(text);
            Text.Font = GameFont.Small;
            listing.Gap(4f);
        }
        
        
        public static bool CheckRitualStatus(Pawn pawn)
        {
            if (pawn.GetLord()?.LordJob is not LordJob_Ritual ritual) return false;
            var role = ritual.RoleFor(pawn);
            return role != null;
        }
    }
    
    public class MedalModSettings : ModSettings
    {
        // Default it to true so your intended behavior is the standard
        public bool MedalsRequireCeremony = true;
        public bool LockMedalsUponAward = true;
        public bool DrawMedalsOnPawns = true;
        public bool MedalDynamicTraits = true;
        public bool PromptForCitationDuringRitual = true;
        public float MedalScale = 0.8f;
        public int MaxDisplayedMedals = 9;
        public bool ShowMedalCatalog = true;

        // This method saves and loads the setting
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref MedalsRequireCeremony, "MedalsRequireCeremony", true);
            Scribe_Values.Look(ref LockMedalsUponAward, "LockMedalsUponAward", true);
            Scribe_Values.Look(ref DrawMedalsOnPawns, "DrawMedalsOnPawns", true);
            Scribe_Values.Look(ref MaxDisplayedMedals, "MaxDisplayedMedals", 9);
            Scribe_Values.Look(ref PromptForCitationDuringRitual, "PromptForCitationDuringRitual", true);
            Scribe_Values.Look(ref MedalDynamicTraits, "MedalDynamicTraits", true);
            Scribe_Values.Look(ref MedalScale, "MedalScale", 0.8f);
            Scribe_Values.Look(ref ShowMedalCatalog, "ShowMedalCatalog", true);
        }
    }
    

    public static class CeremonyQuality
    {
        private const float AttendanceWeight = 0.4f;
        private const float RoomWeight = 0.4f;
        private const float CitationWeight = 0.2f;

        /// <summary>
        /// Returns a quality score from 0.0 to 1.0 based on attendance, room, and citation.
        /// </summary>
        public static float GetQualityScore(int attendees, int totalColonists, float roomImpressiveness, bool hasCitation)
        {
            // Attendance: 0-1 based on ratio, capped at 1.0
            var attendanceRatio = totalColonists > 0
                ? Mathf.Clamp01((float)attendees / totalColonists)
                : 0f;

            // Room impressiveness: mapped from 0-170 (max in vanilla) to 0-1
            // Somewhat impressive (25) = ~0.15, Very impressive (50) = ~0.29
            // Extremely impressive (85) = ~0.5, Unbelievably impressive (170) = 1.0
            var roomScore = Mathf.Clamp01(roomImpressiveness / 170f);

            // Citation: binary
            var citationScore = hasCitation ? 1f : 0f;

            return (attendanceRatio * AttendanceWeight)
                 + (roomScore * RoomWeight)
                 + (citationScore * CitationWeight);
        }

        /// <summary>
        /// Returns 0-3 stage index from a quality score.
        /// </summary>
        public static int GetStageIndex(float qualityScore)
        {
            if (qualityScore >= 0.8f) return 3;  // Legendary
            if (qualityScore >= 0.5f) return 2;  // Grand
            if (qualityScore >= 0.25f) return 1; // Decent
            return 0;                             // Poor
        }

        // Convenience overload for backward compat
        public static int GetStageIndex(int attendees, int totalColonists) => 
            GetStageIndex(GetQualityScore(attendees, totalColonists, 0f, false));

        public static string GetQualityLabel(int stageIndex) =>
            stageIndex switch
            {
                3 => "legendary",
                2 => "grand",
                1 => "decent",
                _ => "poor"
            };

        /// <summary>
        /// Gets room impressiveness at a target position. Returns 0 if outdoors.
        /// </summary>
        public static float GetRoomImpressiveness(TargetInfo target)
        {
            if (!target.HasThing && !target.Cell.IsValid) return 0f;
            var map = target.Map;
            if (map == null) return 0f;
            var cell = target.Cell;
            var room = cell.GetRoom(map);
            if (room == null || room.PsychologicallyOutdoors) return 0f;
            return room.GetStat(RoomStatDefOf.Impressiveness);
        }
    }
    
    public class RitualStageAction_StartBestowal : RitualStageAction
    {
        public override void Apply(LordJob_Ritual ritual)
        {
            var leader = ritual.PawnWithRole("leader");
            var awardee = ritual.PawnWithRole("awardee");
            if (leader != null && awardee != null)
            {
                if (MedalMod.Settings.PromptForCitationDuringRitual)
                    if (ritual.selectedTarget.Thing is RocketMedal medal && medal.citation.NullOrEmpty())
                        Find.WindowStack.Add(new Dialog_WriteCitation(medal));
                
                Messages.Message(
                    $"{leader.NameShortColored} has begun the official bestowal of a medal upon {awardee.NameShortColored}.", 
                    leader, // This makes the message clickable to jump to the leader
                    MessageTypeDefOf.PositiveEvent
                );
            }
            else
            {
                Log.Error("[MedalMod] RitualStageAction_StartBestowal: Leader or Awardee is null!");
            }
        }

        public override void ExposeData()
        {
        }
    }
    
    public class RitualOutcomeEffectWorkerAwardMedal : RitualOutcomeEffectWorker
    {
        [System.ThreadStatic]
        public static bool ApplyingCeremonyAward;

        public RitualOutcomeEffectWorkerAwardMedal() => InitializeSafety();

        public RitualOutcomeEffectWorkerAwardMedal(RitualOutcomeEffectDef def) : base(def) => InitializeSafety();

        private void InitializeSafety()
        {
            this.def ??= DefDatabase<RitualOutcomeEffectDef>.GetNamedSilentFail("ROCKET_AwardMedalOutcome");
            if (this.def is { comps: null }) this.def.comps = new();
        }

        private const int MEDALS_DECORATED = 3;
        private const int MEDALS_HONORED = 5;
        private const int MEDALS_EXHALTED = 7;

        public override void Apply(float progress, Dictionary<Pawn, int> totalPresence, LordJob_Ritual jobRitual)
        {
            ApplyingCeremonyAward = true;
            try { ApplyImpl(progress, totalPresence, jobRitual); }
            finally { ApplyingCeremonyAward = false; }
        }

        private void ApplyImpl(float progress, Dictionary<Pawn, int> totalPresence, LordJob_Ritual jobRitual)
        {
            if (jobRitual.selectedTarget.Thing is not RocketMedal medal) return;
            var awardee = jobRitual.assignments.FirstAssignedPawn("awardee");
            var presenter = jobRitual.assignments.FirstAssignedPawn("leader");
            if (awardee == null || presenter == null) return;

            if (medal.Spawned) medal.DeSpawn();
            awardee.apparel.Wear(medal, false, false);
            medal.isLocked = MedalMod.Settings.LockMedalsUponAward;
            medal.awardedBy = presenter;
            medal.awardedTick = Find.TickManager.TicksGame;

            var attendees = totalPresence.Count;
            var totalColonists = jobRitual.Map.mapPawns.FreeColonistsSpawnedCount;
            var roomImpressiveness = CeremonyQuality.GetRoomImpressiveness(jobRitual.selectedTarget);
            var hasCitation = !medal.citation.NullOrEmpty();

            var qualityScore = CeremonyQuality.GetQualityScore(
                attendees, totalColonists, roomImpressiveness, hasCitation);
            var stageIndex = CeremonyQuality.GetStageIndex(qualityScore);
            var qualityLabel = CeremonyQuality.GetQualityLabel(stageIndex);

            medal.ceremonyQuality = stageIndex;

            var awardedThought = DefDatabase<ThoughtDef>.GetNamed("ROCKET_AwardedMedal_Thought", false);
            var memory = (Thought_Memory)ThoughtMaker.MakeThought(awardedThought, stageIndex);
            awardee.needs?.mood?.thoughts.memories.TryGainMemory(memory);

            var spectatorThought = DefDatabase<ThoughtDef>.GetNamed("ROCKET_WitnessedMedalCeremony_Thought", false);
            if (spectatorThought != null)
            {
                foreach (var pawn in totalPresence.Keys)
                {
                    if (pawn == awardee) continue;
                    pawn.needs?.mood?.thoughts.memories.TryGainMemory(spectatorThought);
                }
            }
            
            var ext = medal.def.GetModExtension<MedalExtension>();
            if (ext is not null && ModsConfig.RoyaltyActive && Faction.OfEmpire != null) 
                awardee.royalty.GainFavor(Faction.OfEmpire, ext.honorAwarded);

            if (MedalMod.Settings.MedalDynamicTraits)
            {
                var medalCount = awardee.apparel.WornApparel.Count(a => a is RocketMedal);
                var decoratedDef = DefDatabase<TraitDef>.GetNamedSilentFail("ROCKET_Decorated");
                if (decoratedDef is not null)
                {
                    // Determine target degree based on medal count
                    var targetDegree = medalCount switch
                    {
                        >= MEDALS_EXHALTED => 2,
                        >= MEDALS_HONORED => 1,
                        >= MEDALS_DECORATED => 0,
                        _ => -1
                    };
                    if (targetDegree >= 0)
                    {
                        var existing = awardee.story.traits.GetTrait(decoratedDef);
                        if (existing is null)
                        {
                            // No decorated trait yet, grant it
                            awardee.story.traits.GainTrait(new Trait(decoratedDef, targetDegree));
                            var label = decoratedDef.DataAtDegree(targetDegree).label.CapitalizeFirst();
                            Messages.Message(
                                $"{awardee.NameShortColored} is now considered {label}.",
                                awardee,
                                MessageTypeDefOf.PositiveEvent
                            );
                        }
                        else if (targetDegree > existing.Degree)
                        {
                            // Already has the trait but at a lower tier, upgrade it
                            awardee.story.traits.RemoveTrait(existing);
                            awardee.story.traits.GainTrait(new Trait(decoratedDef, targetDegree));
                            var label = decoratedDef.DataAtDegree(targetDegree).label.CapitalizeFirst();
                            Messages.Message(
                                $"{awardee.NameShortColored} is now considered {label}.",
                                awardee,
                                MessageTypeDefOf.PositiveEvent
                            );
                        }
                    }
                }

                if (ext?.removesTraits != null)
                {
                    foreach (var entry in ext.removesTraits)
                    {
                        if (!Rand.Chance(entry.chance)) continue;

                        // Find the trait on the pawn that matches both def AND degree
                        var existing = awardee.story.traits.allTraits
                            .FirstOrDefault(t => t.def == entry.trait && t.Degree == entry.degree);

                        if (existing == null) continue;

                        awardee.story.traits.RemoveTrait(existing);
                        Messages.Message(
                            $"{awardee.NameShortColored} has overcome the {entry.Label} trait through distinguished service.",
                            awardee,
                            MessageTypeDefOf.PositiveEvent
                        );
                    }
                }

                if (ext?.addsTraits != null)
                {
                    foreach (var entry in ext.addsTraits)
                    {
                        if (!Rand.Chance(entry.chance)) continue;

                        // Skip if pawn already has this exact trait+degree
                        if (awardee.story.traits.allTraits
                            .Any(t => t.def == entry.trait && t.Degree == entry.degree))
                            continue;

                        // Check for conflicts with existing traits
                        var newTrait = new Trait(entry.trait, entry.degree);
                        if (awardee.story.traits.allTraits.Any(t => t.def.ConflictsWith(newTrait)))
                            continue;

                        awardee.story.traits.GainTrait(newTrait);
                        Messages.Message(
                            $"{awardee.NameShortColored} has gained the {entry.Label} trait through distinguished service.",
                            awardee,
                            MessageTypeDefOf.PositiveEvent
                        );
                    }
                }
            }

            var medalName = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(
                GenLabel.ThingLabel(medal.def, medal.Stuff, 1));

            var letterLabel = $"{medalName} Awarded";
            var letterText = $"{awardee.NameFullColored} has been officially awarded a {medalName} " +
                             $"by {presenter.NameFullColored}.\n\n" +
                             $"The {qualityLabel} ceremony was attended by {attendees} colonists, " +
                             $"and the deeds of {awardee.NameShortColored} have been recognized by the colony.";

            if (!medal.citation.NullOrEmpty())
                letterText += $"\n\nCitation: \"{medal.citation}\"";

            Find.LetterStack.ReceiveLetter(
                label: letterLabel,
                text: letterText,
                LetterDefOf.PositiveEvent,
                lookTargets: awardee
            );

            var medalTale = DefDatabase<TaleDef>.GetNamedSilentFail("ROCKET_AwardedMedalTale");
            if (medalTale != null)
                TaleRecorder.RecordTale(medalTale, presenter, awardee);

            Find.WindowStack.Add(new Dialog_MedalAwarded(medal, awardee, presenter));
        }
    }
    
    public class RitualRole_Presenter : RitualRoleColonist
    {
        public override bool AppliesToPawn(Pawn p, out string reason, TargetInfo selectedTarget, LordJob_Ritual ritual = null, RitualRoleAssignments assignments = null, Precept_Ritual precept = null, bool skipReason = false)
        {
            if (!base.AppliesToPawn(p, out reason, selectedTarget, ritual, assignments, precept, skipReason))
                return false;
            var role = p.Ideo?.GetRole(p);
            if (role != null && (role.def == PreceptDefOf.IdeoRole_Leader || role.def == PreceptDefOf.IdeoRole_Moralist))
            {
                reason = null;
                return true;
            }
            if (!skipReason) reason = "ROCKET_MustBeLeaderOrGuide".Translate();
            return false;
        }
    }
    
    public class JobGiver_AwardSpeech : JobGiver_GiveSpeechFacingTarget
    {
        private static readonly AccessTools.FieldRef<InteractionDef, Texture2D> SymbolTexRef =
            AccessTools.FieldRefAccess<InteractionDef, Texture2D>("symbolTex");

        protected override Job TryGiveJob(Pawn pawn)
        {
            LordJob_Ritual lordJob = pawn.GetLord()?.LordJob as LordJob_Ritual;
            if (lordJob == null) return null;
            lordJob.Ritual.outcomeEffect ??= DefDatabase<RitualOutcomeEffectDef>.GetNamedSilentFail("ROCKET_AwardMedalOutcome").GetInstance();
            lordJob.Ritual.outcomeEffect.compDatas ??= new();
            var awardee = lordJob.assignments.FirstAssignedPawn("awardee");
            if (awardee is not { Spawned: true }) return null;
            var targetB = (LocalTargetInfo)awardee;
            var job = JobMaker.MakeJob(JobDefOf.GiveSpeech, (LocalTargetInfo)pawn.Position, targetB);
            job.showSpeechBubbles = true;
            job.speechFaceSpectatorsIfPossible = this.faceSpectatorsIfPossible;
            var interactDef = DefDatabase<InteractionDef>.GetNamedSilentFail("ROCKET_Speech_AwardMedal");
            if (interactDef != null && lordJob.selectedTarget.Thing is RocketMedal medal)
            {
                var medalTexture = medal.def.uiIcon;
                if (medalTexture != null)
                    SymbolTexRef(interactDef) = medalTexture;
            }
            job.interaction = interactDef;
            job.speechSoundMale = this.soundDefMale ?? SoundDefOf.Speech_Leader_Male;
            job.speechSoundFemale = this.soundDefFemale ?? SoundDefOf.Speech_Leader_Female;
            return job;
        }
    }

    [HarmonyPatch]
    public static class PatchSpeechGrammar
    {
// Cache the private fields once when the game loads
        private static readonly AccessTools.FieldRef<PlayLogEntry_InteractionWithMany, InteractionDef> IntDefRef =
            AccessTools.FieldRefAccess<PlayLogEntry_InteractionWithMany, InteractionDef>("intDef");

        private static readonly AccessTools.FieldRef<PlayLogEntry_InteractionWithMany, Pawn> InitiatorRef =
            AccessTools.FieldRefAccess<PlayLogEntry_InteractionWithMany, Pawn>("initiator");

        // Lazy-cached so we can reference-compare instead of string-comparing defName on every social log entry
        private static InteractionDef _awardSpeechDef;

        public static MethodBase TargetMethod() => AccessTools.Method(typeof(PlayLogEntry_InteractionWithMany), "GenerateGrammarRequest");

        public static void Postfix(Verse.LogEntry __instance, ref GrammarRequest __result)
        {
            if (__instance is not PlayLogEntry_InteractionWithMany manyLog) return;
            var intDef = IntDefRef(manyLog);
            if (intDef == null) return;
            var awardDef = _awardSpeechDef ??= DefDatabase<InteractionDef>.GetNamedSilentFail("ROCKET_Speech_AwardMedal");
            if (awardDef == null || intDef != awardDef) return;

            var initiator = InitiatorRef(manyLog);
            var lordJob = initiator?.GetLord()?.LordJob as LordJob_Ritual;
            var awardee = lordJob?.assignments.FirstAssignedPawn("awardee");
            if (awardee == null) return;
            var rules = GrammarUtility.RulesForPawn("RECIPIENT", awardee, __result.Constants);
            __result.Rules.AddRange(rules);
        }
    }
    
    [HarmonyPatch(typeof(Pawn_ApparelTracker), nameof(Pawn_ApparelTracker.IsLocked))]
    public static class PatchRankAlwaysForced
    {
        public static void Postfix(Apparel __0, ref bool __result)
        {
            if (__0 is RocketMedal medal)
                __result = medal.isLocked;
        }
    }
    
    [HarmonyPatch(typeof(Corpse), nameof(Corpse.Strip))]
    public static class PatchCorpseStripMedals
    {
        public static void Prefix(Corpse __instance, out List<RocketMedal> __state)
        {
            __state = null;
            var pawn = __instance.InnerPawn;
            if (pawn?.apparel == null) return;

            __state = pawn.apparel.WornApparel
                .OfType<RocketMedal>()
                .Where(m => m.isLocked)
                .ToList();
        }

        public static void Postfix(Corpse __instance, List<RocketMedal> __state)
        {
            if (__state == null || __state.Count == 0) return;
            var pawn = __instance.InnerPawn;
            if (pawn?.apparel == null) return;

            foreach (var medal in __state)
            {
                if (medal.Destroyed) continue;
                if (pawn.apparel.WornApparel.Contains(medal)) continue;

                if (medal.Spawned) medal.DeSpawn();
                pawn.apparel.Wear(medal, false, true);
            }
        }
    }
    
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.Strip))]
    public static class PatchPawnStripMedals
    {
        public static void Prefix(Pawn __instance, out List<RocketMedal> __state)
        {
            __state = null;
            if (__instance.apparel == null) return;

            __state = __instance.apparel.WornApparel
                .OfType<RocketMedal>()
                .Where(m => m.isLocked)
                .ToList();
        }

        public static void Postfix(Pawn __instance, List<RocketMedal> __state)
        {
            if (__state == null || __state.Count == 0) return;
            if (__instance.apparel == null) return;

            foreach (var medal in __state)
            {
                if (medal.Destroyed) continue;
                if (__instance.apparel.WornApparel.Contains(medal)) continue;

                if (medal.Spawned) medal.DeSpawn();
                __instance.apparel.Wear(medal, false, true);
            }
        }
    }
    
    [HarmonyPatch(typeof(JobGiver_OptimizeApparel), nameof(JobGiver_OptimizeApparel.ApparelScoreRaw))]
    public static class PatchMedalNoAutoEquip
    {
        public static void Postfix(Pawn pawn, Apparel ap, ref float __result)
        {
            if (ap is RocketMedal)
                __result = -10000f;
        }
    }

    [HarmonyPatch(typeof(ApparelUtility), nameof(ApparelUtility.HasPartsToWear))]
    public static class Patch_ApparelUtility_HasPartsToWear
    {
        public static void Postfix(Pawn p, ThingDef apparel, ref bool __result)
        {
            if (!__result || p == null) return;
            if (!typeof(RocketMedal).IsAssignableFrom(apparel.thingClass)) return;

            if (RitualOutcomeEffectWorkerAwardMedal.ApplyingCeremonyAward) return;
            if (PawnOwnsBiocodedMedalDef(p, apparel)) return;

            if (MedalMod.Settings.MedalsRequireCeremony && !MedalMod.CheckRitualStatus(p))
                __result = false;
        }

        private static bool PawnOwnsBiocodedMedalDef(Pawn p, ThingDef apparelDef)
        {
            var worn = p.apparel?.WornApparel;
            if (worn != null)
            {
                for (var i = 0; i < worn.Count; i++)
                {
                    if (worn[i].def != apparelDef) continue;
                    if (worn[i] is RocketMedal m && m.BiocodeComp is { Biocoded: true } b && b.CodedPawn == p)
                        return true;
                }
            }
            var inv = p.inventory?.innerContainer;
            if (inv == null) return false;
            for (var i = 0; i < inv.Count; i++)
            {
                if (inv[i].def != apparelDef) continue;
                if (inv[i] is RocketMedal m && m.BiocodeComp is { Biocoded: true } b && b.CodedPawn == p)
                    return true;
            }
            return false;
        }
    }

    [HarmonyPatch(typeof(Pawn_ApparelTracker), nameof(Pawn_ApparelTracker.Wear))]
    public static class PatchMedalBiocodeManual
    {
        
        public static bool Prefix(Pawn_ApparelTracker __instance, Apparel newApparel)
        {
            if (newApparel is not RocketMedal medal) return true;
            var comp = medal.BiocodeComp;
            if (comp == null) return true;

            // The bestowal Apply path bypasses the ceremony rejection — see ApplyingCeremonyAward.
            if (!RitualOutcomeEffectWorkerAwardMedal.ApplyingCeremonyAward
                && MedalMod.Settings.MedalsRequireCeremony
                && !MedalMod.CheckRitualStatus(__instance.pawn)
                && !comp.Biocoded)
            {
                Messages.Message($"ROCKET_FailMedalNeedsCeremony".Translate(), MessageTypeDefOf.RejectInput, false);
                return false;
            }
            
            if (comp.Biocoded && comp.CodedPawn != __instance.pawn)
            {
                Messages.Message("MedalBiocodeReject".Translate(comp.CodedPawn.LabelShort), MessageTypeDefOf.RejectInput, false);
                return false; 
            }
            
            if (!comp.Biocoded)
            {
                comp.CodeFor(__instance.pawn);
                var cleanLabel = GenLabel.ThingLabel(medal.def, medal.Stuff, 1);
                var nameValue = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(cleanLabel);
                Log.Message($"[MedalMod] Biocoded {cleanLabel} to {__instance.pawn.LabelShort}");
                Messages.Message($"{__instance.pawn.NameShortColored} has been awarded the {nameValue}", __instance.pawn, MessageTypeDefOf.PositiveEvent);
                if (__instance.pawn.needs is { mood: not null })
                {
                    var awardedThought = DefDatabase<ThoughtDef>.GetNamed("ROCKET_AwardedMedal_Thought", false);
                    if (awardedThought != null) 
                        __instance.pawn.needs.mood.thoughts.memories.TryGainMemory(awardedThought);
                }
            }
            return true; 
        }
    }

    [StaticConstructorOnStartup]
    public static class MedalModInit
    {
        static MedalModInit()
        {
            new Harmony("com.rocket.medalmod").PatchAll();
            Log.Message("[MedalMod] Harmony patches applied successfully.");
        }
    }
    
    [HarmonyPatch(typeof(PawnRenderNodeWorker), nameof(PawnRenderNodeWorker.CanDrawNow))]
    public static class PatchMedalVisibility
    {
        public static void Postfix(PawnRenderNode node, PawnDrawParms parms, ref bool __result)
        {
            if (!__result) return;
            if (node is not PawnRenderNode_Apparel { apparel: RocketMedal apparelNode}) return;
            if (!MedalMod.Settings.DrawMedalsOnPawns)
            {
                __result = false;
                return;
            }

            if (parms.facing == Rot4.North)
            {
                __result = false;
                return;
            }
            
            var worn = parms.pawn.apparel.WornApparel; // This is a List<Apparel>
            var maxDisplayed = MedalMod.Settings.MaxDisplayedMedals;
            var myIndex = 0;

            for (var i = 0; i < worn.Count; i++)
            {
                if (worn[i] is not RocketMedal) continue;
                if (worn[i] == apparelNode)
                {
                    if (myIndex >= maxDisplayed) __result = false;
                    return;
                }
                myIndex++;
            }
        }
    }

    [HarmonyPatch(typeof(ApparelUtility), nameof(ApparelUtility.CanWearTogether))]
    public static class PatchMedalConflict
    {
        public static void Postfix(ThingDef A, ThingDef B, BodyDef body, ref bool __result)
        {
            if (__result) return;
            var aIsMedal = typeof(RocketMedal).IsAssignableFrom(A.thingClass);
            var bIsMedal = typeof(RocketMedal).IsAssignableFrom(B.thingClass);
            if (aIsMedal && bIsMedal) 
                __result = true;
        }
    }

    [HarmonyPatch(typeof(ApparelGraphicRecordGetter), nameof(ApparelGraphicRecordGetter.TryGetGraphicApparel))]
    public static class PatchMedalGraphicRecord
    {
        public static bool Prefix(Apparel apparel, BodyTypeDef bodyType, ref ApparelGraphicRecord rec, ref bool __result)
        {
            if (apparel is not RocketMedal medal) return true;
            var path = medal.def.apparel.wornGraphicPath;
                
            var color = medal.DrawColor;
            var colorTwo = medal.DrawColorTwo;

            var graphic = GraphicDatabase.Get<Graphic_Single>(
                path, 
                ShaderDatabase.CutoutComplex, 
                Vector2.one, 
                color, 
                colorTwo
            );
                
            rec = new ApparelGraphicRecord(graphic, apparel);
            __result = true; 
            return false;

        }
    }
    
    [HarmonyPatch(typeof(PawnRenderNodeWorker), nameof(PawnRenderNodeWorker.ScaleFor))]
    public static class PatchMedalScale
    {
        public static void Postfix(PawnRenderNode node, PawnDrawParms parms, ref Vector3 __result)
        {
            if (node is not PawnRenderNode_Apparel apparelNode || apparelNode.apparel is not RocketMedal) return;
            var baseScale = 0.3f; 
            var bodyModifier = GetScale(node, parms);
            __result *= (baseScale * bodyModifier);
        }
        
        public static float GetScale(PawnRenderNode node, PawnDrawParms parms)
        {
            if (node is not PawnRenderNode_Apparel apparelNode || apparelNode.apparel is not RocketMedal) return 0f;
            var bodyModifier = 1.0f; // Default for Male
            var bodyType = parms.pawn?.story?.bodyType;
            if (bodyType == null) return bodyModifier;
            if (bodyType == BodyTypeDefOf.Hulk) bodyModifier = 1.15f; 
            else if (bodyType == BodyTypeDefOf.Fat) bodyModifier = 1.15f;
            else if (bodyType == BodyTypeDefOf.Thin) bodyModifier = 0.9f;
            else if (bodyType == BodyTypeDefOf.Female) bodyModifier = 0.95f;
            bodyModifier *= MedalMod.Settings.MedalScale;
            return bodyModifier;
        }
    }
    
    [HarmonyPatch(typeof(PawnRenderNodeWorker), nameof(PawnRenderNodeWorker.OffsetFor))]
    public static class PatchMedalOffset
    {
        private const float BaseXOffset = 0.02f; 
        private const float RowZDrop = 0.1f; 
        
        public static void Postfix(PawnRenderNode node, PawnDrawParms parms, ref Vector3 __result)
        {
            if (node is not PawnRenderNode_Apparel apparelNode || apparelNode.apparel is not RocketMedal) 
                return;

            __result.z += -0.148f;
            var scale = PatchMedalScale.GetScale(node, parms);
            var indexOffset = 0.1f * scale;
            var bodyType = parms.pawn?.story?.bodyType;
            var shift = 0.02f * scale;
            var maxMedalsPerRow = 3; 

            if (bodyType != null)
            {
                if (bodyType == BodyTypeDefOf.Hulk || bodyType == BodyTypeDefOf.Fat)
                {
                    maxMedalsPerRow = 4; 
                }
                else if (bodyType == BodyTypeDefOf.Thin || bodyType == BodyTypeDefOf.Female)
                {
                    maxMedalsPerRow = 3; 
                }
            }
            __result.x += (parms.facing == Rot4.West || parms.facing == Rot4.East) ? 0 : shift;

            var worn = parms.pawn.apparel.WornApparel; // This is a List<Apparel>
            var totalMedals = 0;
            var myIndex = -1;

            for (var i = 0; i < worn.Count; i++)
            {
                if (worn[i] is RocketMedal)
                {
                    if (worn[i] == apparelNode.apparel)
                        myIndex = totalMedals;

                    totalMedals++;
                }
            }
            if (myIndex == -1) return;

            var reverseIndex = (totalMedals - 1) - myIndex;

            var row = reverseIndex / maxMedalsPerRow;
            var col = reverseIndex % maxMedalsPerRow;

            var totalRows = (totalMedals + maxMedalsPerRow - 1) / maxMedalsPerRow;
            var medalsInThisRow = maxMedalsPerRow;

            // If we are looking at the very bottom row, check if it's partially empty
            if (row == totalRows - 1 && totalMedals % maxMedalsPerRow != 0)
                medalsInThisRow = totalMedals % maxMedalsPerRow;

            // Shift the column to the right by half the missing width
            var missingMedals = maxMedalsPerRow - medalsInThisRow;
            var centeredCol = col + (missingMedals / 2f);

            // Apply X shift using our new 'centeredCol' instead of the raw 'col'
            var baseX = (parms.facing == Rot4.West || parms.facing == Rot4.East) ? 0 : BaseXOffset;
            var shiftX = baseX + (centeredCol * indexOffset);
            if (parms.facing == Rot4.West || parms.facing == Rot4.North)
                __result.x -= shiftX;
            else
                __result.x += shiftX;

            // Apply Z (Vertical) shift
            __result.z -= (row * RowZDrop * scale);
        }
    }
}