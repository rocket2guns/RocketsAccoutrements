using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
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
    }
    
    public class MedalMod : Mod
    {
        public static MedalModSettings Settings;

        // Constructor runs when the mod is loaded
        public MedalMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<MedalModSettings>();
        }

        public override string SettingsCategory() => "Rocket's Accoutrements";

        // Draws the actual menu
        public override void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(inRect);

            // --- SECTION: MEDALS ---
            listing.Gap(32f);
            Text.Font = GameFont.Medium;
            listing.Label("Medals");
            Text.Font = GameFont.Small;
            listing.Gap(6f);
            listing.CheckboxLabeled(
                "Medals Require Ceremony",  
                ref Settings.MedalsRequireCeremony, 
                "If enabled, medals can only be awarded to colonists via an award ceremony. If disabled, medals will be considered awarded by the first person to wear them."
            );
            listing.CheckboxLabeled(
                "Lock Medals on Pawns", 
                ref Settings.LockMedalsOnPawns, 
                "If enabled, once a medal is awarded to a pawn, it cannot be removed. If disabled, medals will be removed when a pawn is stripped or drops them voluntarily. They will remain biocoded."
            );
            listing.CheckboxLabeled(
                "Draw Medals on Pawns", 
                ref Settings.DrawMedalsOnPawns, 
                "If enabled, medals will be rendered on pawns. Disabling this will stop medals being drawn on pawns. Use this to turn their rendering off."
            );
            listing.CheckboxLabeled(
                "Prompt for Citation during Ritual", 
                ref Settings.PromptForCitationDuringRitual, 
                "If enabled, a dialog will appear during award ceremonies to allow the user to enter a citation for the medal if one has not already been added to the medal. If disable a window will not appear, but citations can still be added prior to ceremony commencing."
            );
            listing.Label($"Worn Size: {Settings.MedalScale.ToStringPercent()}");
            Settings.MedalScale = listing.Slider(Settings.MedalScale, 0.1f, 2.0f);
            listing.Label($"Maximum Rows: {Settings.MaxDisplayedMedals.ToStringCached()}");
            listing.IntAdjuster(ref Settings.MaxDisplayedMedals, 1);

            // --- SECTION: RANKS ---
            listing.Gap(32f);
            Text.Font = GameFont.Medium;
            listing.Label("Ranks");
            Text.Font = GameFont.Small;
            listing.Gap(6f);
            listing.CheckboxLabeled(
                "Ranks Ignore Outfit Policies", 
                ref Settings.RanksIgnorePolicies, 
                "If enabled, pawns will never automatically remove ranks when changing outfits. This allows you to force wear a rank item through clothing priorities."
            );
            listing.End();
            base.DoSettingsWindowContents(inRect);
        }
    }
    
    public class MedalModSettings : ModSettings
    {
        // Default it to true so your intended behavior is the standard
        public bool RanksIgnorePolicies = true;
        public bool MedalsRequireCeremony = true;
        public bool LockMedalsOnPawns = false;
        public bool DrawMedalsOnPawns = true;
        public bool PromptForCitationDuringRitual = true;
        public float MedalScale = 0.8f;
        public int MaxDisplayedMedals = 9;

        // This method saves and loads the setting
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref RanksIgnorePolicies, "RanksIgnorePolicies", true);
            Scribe_Values.Look(ref MedalsRequireCeremony, "MedalsRequireCeremony", true);
            Scribe_Values.Look(ref LockMedalsOnPawns, "LockMedalsOnPawns", false);
            Scribe_Values.Look(ref DrawMedalsOnPawns, "DrawMedalsOnPawns", true);
            Scribe_Values.Look(ref MaxDisplayedMedals, "MaxDisplayedMedals", 9);
            Scribe_Values.Look(ref PromptForCitationDuringRitual, "PromptForCitationDuringRitual", true);
            Scribe_Values.Look(ref MedalScale, "MedalScale", 0.8f);
        }
    }
    
    public static class CeremonyQuality
    {
        // Returns 0-3 matching the thought stage index
        public static int GetStageIndex(int attendees, int totalColonists)
        {
            if (totalColonists <= 0) return 0;
            var ratio = (float)attendees / totalColonists;

            if (ratio >= 0.8f) return 3;  // Legendary: 80%+ of colony attended
            if (ratio >= 0.5f) return 2;  // Grand: half the colony
            if (ratio >= 0.25f) return 1; // Decent: at least a quarter
            return 0;                      // Poor: barely anyone
        }

        public static string GetQualityLabel(int stageIndex)
        {
            switch (stageIndex)
            {
                case 3: return "legendary";
                case 2: return "grand";
                case 1: return "decent";
                default: return "poor";
            }
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
        public RitualOutcomeEffectWorkerAwardMedal() => InitializeSafety();

        public RitualOutcomeEffectWorkerAwardMedal(RitualOutcomeEffectDef def) : base(def) => InitializeSafety();

        private void InitializeSafety()
        {
            this.def ??= DefDatabase<RitualOutcomeEffectDef>.GetNamedSilentFail("ROCKET_AwardMedalOutcome");
            if (this.def is { comps: null }) this.def.comps = new();
        }

        public override void Apply(float progress, Dictionary<Pawn, int> totalPresence, LordJob_Ritual jobRitual)
        {
            if (jobRitual.selectedTarget.Thing is not RocketMedal medal) return;
            var awardee = jobRitual.assignments.FirstAssignedPawn("awardee");
            var presenter = jobRitual.assignments.FirstAssignedPawn("leader");
            if (awardee == null || presenter == null) return;

            if (medal.citation.NullOrEmpty())
                Find.WindowStack.Add(new Dialog_WriteCitation(medal));

            if (medal.Spawned) medal.DeSpawn();
            awardee.apparel.Wear(medal, false, false);

            var attendees = totalPresence.Count;
            var totalColonists = jobRitual.Map.mapPawns.FreeColonistsSpawnedCount;
            var stageIndex = CeremonyQuality.GetStageIndex(attendees, totalColonists);
            var qualityLabel = CeremonyQuality.GetQualityLabel(stageIndex);

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
            if (!skipReason) reason = "Must be the Leader or Moral Guide.";
            return false;
        }
    }
    
    public class JobGiver_AwardSpeech : JobGiver_GiveSpeechFacingTarget
    {
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
                    AccessTools.Field(typeof(InteractionDef), "symbolTex").SetValue(interactDef, medalTexture);
            }
            job.interaction = interactDef;
            job.speechSoundMale = this.soundDefMale ?? SoundDefOf.Speech_Leader_Male;
            job.speechSoundFemale = this.soundDefFemale ?? SoundDefOf.Speech_Leader_Female;
            return job;
        }
    }
    
    public class RocketRank : Apparel
    {
        
    }
    
    public class Dialog_MedalAwarded : Window
    {
        private readonly RocketMedal medal;
        private readonly Pawn awardee;
        private readonly Pawn presenter;

        public Dialog_MedalAwarded(RocketMedal medal, Pawn awardee, Pawn presenter)
        {
            this.medal = medal;
            this.awardee = awardee;
            this.presenter = presenter;
            forcePause = true;
            doCloseX = true;
            absorbInputAroundWindow = true;
        }

        public override Vector2 InitialSize => new(500f, 400f);

        public override void DoWindowContents(Rect inRect)
        {
            // Medal icon, large and centered
            var iconRect = new Rect(inRect.x + (inRect.width - 128f) / 2f, inRect.y + 10f, 128f, 128f);
            Widgets.ThingIcon(iconRect, medal);

            // Medal name
            var nameRect = new Rect(inRect.x, iconRect.yMax + 10f, inRect.width, 30f);
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(nameRect, medal.LabelCap);

            // Awarded to / by
            var detailRect = new Rect(inRect.x, nameRect.yMax + 6f, inRect.width, 24f);
            Text.Font = GameFont.Small;
            Widgets.Label(detailRect, $"Awarded to {awardee.NameFullColored} by {presenter.NameFullColored}");

            // Citation
            if (!medal.citation.NullOrEmpty())
            {
                var citationRect = new Rect(inRect.x + 20f, detailRect.yMax + 16f, inRect.width - 40f, 80f);
                GUI.color = new Color(0.9f, 0.85f, 0.4f);
                Text.Font = GameFont.Small;
                Widgets.Label(citationRect, $"\"{medal.citation}\"");
                GUI.color = Color.white;
            }

            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;

            // Close button
            if (Widgets.ButtonText(new Rect(inRect.x + (inRect.width - 120f) / 2f, inRect.yMax - 45f, 120f, 35f), "Close"))
                Close();
        }
    }
    
    public class Dialog_WriteCitation : Window
    {
        private readonly RocketMedal medal;
        private string draft;
        private const int MaxLength = 300;

        public Dialog_WriteCitation(RocketMedal medal)
        {
            this.medal = medal;
            this.draft = medal.citation ?? "";
            
            forcePause = true;
            doCloseX = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = false;
        }

        public override Vector2 InitialSize => new(500f, 350f);

        public override void DoWindowContents(Rect inRect)
        {
            var listing = new Listing_Standard();
            listing.Begin(inRect);

            // Header
            Text.Font = GameFont.Medium;
            listing.Label(medal.LabelCap);
            Text.Font = GameFont.Small;
            listing.Gap(4f);

            // Instruction
            GUI.color = Color.gray;
            listing.Label("Write the citation to be engraved on this medal. This will be permanently sealed once the medal is awarded.");
            GUI.color = Color.white;
            listing.Gap(8f);

            // Text area
            var textRect = listing.GetRect(150f);
            draft = Widgets.TextArea(textRect, draft);
            if (draft.Length > MaxLength)
                draft = draft.Substring(0, MaxLength);

            // Character count
            listing.Gap(4f);
            var countColor = draft.Length > MaxLength - 30 ? Color.yellow : Color.gray;
            GUI.color = countColor;
            listing.Label($"{draft.Length} / {MaxLength}");
            GUI.color = Color.white;

            listing.Gap(12f);

            // Buttons
            var buttonRect = listing.GetRect(35f);
            var halfWidth = (buttonRect.width - 10f) / 2f;

            // Save
            if (Widgets.ButtonText(new Rect(buttonRect.x, buttonRect.y, halfWidth, 35f), "Confirm"))
            {
                medal.citation = draft.NullOrEmpty() ? null : draft.Trim();
                Close();
            }

            // Clear
            if (Widgets.ButtonText(new Rect(buttonRect.x + halfWidth + 10f, buttonRect.y, halfWidth, 35f), "Clear"))
                draft = "";

            listing.End();
        }
    }
    
    public class RocketMedal : Apparel
    {
        public CompBiocodable BiocodeComp => field ??= this.GetComp<CompBiocodable>();
        private Pawn _cachedPawn = null;
        
        public string citation;
        public int ceremonyQuality = -1; // -1 = not yet awarded
        
        public bool CitationLocked => BiocodeComp is { Biocoded: true };
        
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref citation, "citation");
            Scribe_Values.Look(ref ceremonyQuality, "ceremonyQuality", -1);
        }

        public override string GetInspectString()
        {
            var sb = new StringBuilder(base.GetInspectString());
            if (!citation.NullOrEmpty())
            {
                if (sb.Length > 0) sb.AppendLine();
                sb.Append('\'');
                sb.Append(citation);
                sb.Append('\'');
            }
            return sb.ToString();
        }

        private void OpenCitationDialog()
        {
            if (CitationLocked) return;
            Find.WindowStack.Add(new Dialog_WriteCitation(this));
        }

        public override string LabelNoCount
        {
            get
            {
                if (BiocodeComp is not { Biocoded: true, CodedPawn: not null }) return base.LabelNoCount;
                if (field != null && _cachedPawn == BiocodeComp.CodedPawn) return field;
                
                _cachedPawn = BiocodeComp.CodedPawn;

                var cleanLabel = GenLabel.ThingLabel(this.def, this.Stuff, 1);
                if (AllComps != null)
                {
                    foreach (var comp in AllComps)
                    {
                        if (comp is CompBiocodable) continue; 
                        cleanLabel = comp.TransformLabel(cleanLabel);
                    }
                }
                var sb = new StringBuilder();
                sb.Append(_cachedPawn.LabelShort);
                sb.Append("'s");
                sb.Append(' ');
                sb.Append(cleanLabel);
                field = sb.ToString();
                return field;
            }
        } = null;
        
        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (var gizmo in base.GetGizmos())
                yield return gizmo;

            var awardCeremonyBtn = new Command_Action
            {
                defaultLabel = "Award Ceremony",
                defaultDesc = "Gather the colony to officially bestow this medal upon a colonist.",
                icon = this.def.uiIcon, 
                action = StartMedalRitual
            };
            
            if (!ModsConfig.IdeologyActive)
                awardCeremonyBtn.Disable("Ideology DLC must be active to award medals.");
            
            if (BiocodeComp.Biocoded)
                awardCeremonyBtn.Disable("Medal has already been awarded.");

            // Check if the player actually has a leader in their colony to enable the button
            if (!ColonyHasPresenter(out _)) 
                awardCeremonyBtn.Disable("Requires a viable presenter.");
            
            yield return awardCeremonyBtn;
            
            var citationBtn = new Command_Action
            {
                defaultLabel = citation.NullOrEmpty() ? "Write Citation" : "Edit Citation",
                defaultDesc = CitationLocked
                    ? "This medal's citation has been sealed by the award ceremony."
                    : "Write or edit the citation engraved on this medal.",
                icon = MedalTextures.CitationIcon,
                action = OpenCitationDialog
            };

            if (CitationLocked)
                citationBtn.Disable("Citation is sealed after the award ceremony.");
            
            yield return citationBtn;
        }

        private bool ColonyHasPresenter(out Pawn result)
        {
            result = null;
            if (this.Map == null) return false;
            foreach (var p in this.Map.mapPawns.FreeColonistsSpawned)
            {
                var role = p.Ideo?.GetRole(p);
                if (role != null && (role.def == PreceptDefOf.IdeoRole_Leader || role.def == PreceptDefOf.IdeoRole_Moralist))
                {
                    result = p;
                    return true; 
                }
            }
            return false;
        }

        private void StartMedalRitual()
        {
            if (!ColonyHasPresenter(out Pawn presenter)) return;
            var pattern = DefDatabase<RitualPatternDef>.GetNamedSilentFail("ROCKET_AwardMedalPattern");
            var dummyPreceptDef = DefDatabase<PreceptDef>.GetNamedSilentFail("ROCKET_MedalCeremonyPrecept");

            if (pattern == null || dummyPreceptDef == null)
            {
                Log.Error("[MedalMod] Missing Ritual Defs! Check your XML names.");
                return;
            }

            var safeMap = this.Map ?? this.MapHeld;
            if (safeMap == null) return;

            var ritualTarget = new TargetInfo(this);

            var fakeRitual = (Precept_Ritual)PreceptMaker.MakePrecept(dummyPreceptDef);
            fakeRitual.ideo = presenter.Ideo;
            fakeRitual.sourcePattern = pattern;
            var nameValue = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(LabelShort);
            var ceremonyName = $"{nameValue} Award Ceremony";
            fakeRitual.SetName("Award Ceremony");
            
            fakeRitual.behavior = pattern.ritualBehavior.GetInstance();
            fakeRitual.behavior.def = pattern.ritualBehavior;
            fakeRitual.outcomeEffect = pattern.ritualOutcomeEffect.GetInstance();
            fakeRitual.outcomeEffect.def = pattern.ritualOutcomeEffect;
            fakeRitual.outcomeEffect.compDatas ??= new();
            
            Dialog_BeginRitual.ActionCallback startAction = delegate (RitualRoleAssignments assignments)
            {
                LordJob_Ritual lordJob = new LordJob_Ritual(
                    selectedTarget: ritualTarget,
                    ritual: fakeRitual,
                    obligation: null,
                    allStages: pattern.ritualBehavior.stages,
                    assignments: assignments,
                    organizer: null,
                    spotOverride: null
                );
                LordMaker.MakeNewLord(Faction.OfPlayer, lordJob, safeMap, assignments.Participants);
                return true; 
            };

            var outcomeDef = pattern.ritualOutcomeEffect;

            Find.WindowStack.Add(new Dialog_BeginRitual(
                ritualLabel: ceremonyName,
                ritual: fakeRitual,       
                target: ritualTarget,     
                map: safeMap,             
                action: startAction,      
                organizer: null,
                obligation: null,         
                filter: null,
                okButtonText: "Begin Ceremony",
                requiredPawns: null,
                forcedForRole: null,
                outcomeDef        
            ));
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

        public static MethodBase TargetMethod() => AccessTools.Method(typeof(PlayLogEntry_InteractionWithMany), "GenerateGrammarRequest");
    
        public static void Postfix(Verse.LogEntry __instance, ref GrammarRequest __result)
        {
            if (__instance is PlayLogEntry_InteractionWithMany manyLog)
            {
                // Instantly access the private fields with zero reflection cost
                var intDef = IntDefRef(manyLog);
                if (intDef is { defName: "ROCKET_Speech_AwardMedal" })
                {
                    var initiator = InitiatorRef(manyLog);
                    var lordJob = initiator?.GetLord()?.LordJob as LordJob_Ritual;
                    var awardee = lordJob?.assignments.FirstAssignedPawn("awardee");
                    if (awardee == null) return;
                    var rules = GrammarUtility.RulesForPawn("RECIPIENT", awardee, __result.Constants);
                    __result.Rules.AddRange(rules);
                }
            }
        }
    }
    
    [HarmonyPatch(typeof(Pawn_ApparelTracker), nameof(Pawn_ApparelTracker.IsLocked))]
    public static class PatchRankAlwaysForced
    {
        public static void Postfix(Apparel __0, ref bool __result)
        {
            if (__0 is not RocketMedal || !MedalMod.Settings.LockMedalsOnPawns) return;
            __result = true;
        }
    }
    
    [HarmonyPatch(typeof(JobGiver_OptimizeApparel), nameof(JobGiver_OptimizeApparel.ApparelScoreRaw))]
    public static class PatchMedalNoAutoEquip
    {
        public static void Postfix(Pawn pawn, Apparel ap, ref float __result)
        {
            if (ap is RocketMedal || (MedalMod.Settings.RanksIgnorePolicies && ap is RocketRank)) 
                __result = -10000f;
        }
    }
    
    [HarmonyPatch(typeof(Pawn_ApparelTracker), nameof(Pawn_ApparelTracker.Wear))]
    public static class PatchMedalBiocodeManual
    {
        public static bool CheckRitualStatus(Pawn pawn)
        {
            if (pawn.GetLord()?.LordJob is LordJob_Ritual ritual)
            {
                var role = ritual.RoleFor(pawn);
                Log.Message($"{pawn.LabelShort} is part of the {ritual.RitualLabel} ritual.");
                if (role != null)
                    return true;
            }

            return false;
        }
        
        public static bool Prefix(Pawn_ApparelTracker __instance, Apparel newApparel)
        {
            if (newApparel is not RocketMedal medal) return true;
            var comp = medal.BiocodeComp;
            if (comp == null) return true;

            if (MedalMod.Settings.MedalsRequireCeremony && !CheckRitualStatus(__instance.pawn) && !comp.Biocoded)
            {
                Messages.Message($"Medals can only be awarded via ceremonies", MessageTypeDefOf.RejectInput, false);
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
            var totalMedals = 0;
            var myIndex = -1;

            for (var i = 0; i < worn.Count; i++)
            {
                if (worn[i] is RocketMedal)
                {
                    if (worn[i] == apparelNode)
                        myIndex = totalMedals;
            
                    totalMedals++;
                }
            }
            if (myIndex == -1) return;

            if (myIndex >= MedalMod.Settings.MaxDisplayedMedals)
            {
                __result = false;
                return;
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
    
    [HarmonyPatch(typeof(ThingFilter), nameof(ThingFilter.Allows), new[] { typeof(ThingDef) })]
    public static class PatchRankPolicyBypass
    {
        public static void Postfix(ThingDef def, ref bool __result)
        {
            if (MedalMod.Settings.RanksIgnorePolicies && typeof(RocketRank).IsAssignableFrom(def.thingClass)) 
                __result = true;
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

            var currentIndex = 0;
            foreach (var a in worn)
            {
                if (a is not RocketMedal) continue;
                
                if (a == apparelNode.apparel)
                {
                    var reverseIndex = (totalMedals - 1) - currentIndex; 

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
                    
                    break; 
                }
                currentIndex++;
            }
        }
    }
}