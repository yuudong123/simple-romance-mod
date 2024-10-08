using System;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace SimpleRomance
{
    public class SimpleRomanceSettings : ModSettings
    {
        public float romanceAgeThreshold = 16f;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref romanceAgeThreshold, "romanceAgeThreshold", 16f);
        }
    }

    public class SimpleRomanceMod : Mod
    {
        public static SimpleRomanceSettings settings;

        public SimpleRomanceMod(ModContentPack content) : base(content)
        {
            settings = GetSettings<SimpleRomanceSettings>();
            var harmony = new Harmony("com.yuudong123.simpleromance");
            harmony.PatchAll();
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard listingStandard = new Listing_Standard();
            listingStandard.Begin(inRect);

            listingStandard.Label("Romance Minimum Age (min 1):");
            float tempAge = settings.romanceAgeThreshold;
            string buffer = tempAge.ToString();

            listingStandard.TextFieldNumeric(ref tempAge, ref buffer, 1f);

            if (tempAge < 1f) tempAge = 1f;
            settings.romanceAgeThreshold = tempAge;

            if (listingStandard.ButtonText("Save Settings"))
            {
                WriteSettings();
            }

            listingStandard.End();
        }

        public override string SettingsCategory() => "SimpleRomance";

        public override void WriteSettings()
        {
            base.WriteSettings();
        }
    }

    [HarmonyPatch(typeof(InteractionWorker_RomanceAttempt), "SuccessChance")]
    internal static class Patch_SuccessChance
    {
        static bool Prefix(ref float __result, Pawn initiator, Pawn recipient, float baseChance = 0.6f)
        {
            float b = initiator.GetStatValue(StatDefOf.PawnBeauty);
            float beauty = b >= 0 ? b/2 + 1 : -1f/b;
            float opinion = Mathf.InverseLerp(5f, 100f, recipient.relations.OpinionOf(initiator)) * 0.7f;
            HediffWithTarget hediffWithTarget = (HediffWithTarget)recipient.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.PsychicLove);
            float magic = hediffWithTarget != null && hediffWithTarget.target == initiator ? 10f : 1f;
            __result = Mathf.Clamp01(beauty * opinion * magic);
            return false;
        }
    }

    [HarmonyPatch(typeof(SocialCardUtility), "CanDrawTryRomance")]
    internal static class Patch_CanDrawTryRomance
    {
        static bool Prefix(ref bool __result, Pawn pawn)
        {
            float romanceAge = SimpleRomanceMod.settings.romanceAgeThreshold;

            if (ModsConfig.BiotechActive && pawn.ageTracker.AgeBiologicalYearsFloat >= romanceAge && pawn.Spawned)
            {
                __result = pawn.IsFreeColonist;
                return false;
            }

            __result = false;
            return false;
        }
    }

    [HarmonyPatch(typeof(RelationsUtility), "RomanceEligible")]
    internal static class Patch_RomanceEligible
    {
        static bool Prefix(ref AcceptanceReport __result, Pawn pawn, bool initiator, bool forOpinionExplanation)
        {
            float romanceAge = SimpleRomanceMod.settings.romanceAgeThreshold;

            if (pawn.ageTracker.AgeBiologicalYearsFloat < romanceAge)
            {
                __result = false;
                return false;
            }

            if (pawn.IsPrisoner)
            {
                if (!initiator || forOpinionExplanation)
                {
                    __result = AcceptanceReport.WasRejected;
                    return false;
                }

                __result = "CantRomanceInitiateMessagePrisoner".Translate(pawn).CapitalizeFirst();
                return false;
            }

            if (pawn.Downed && !forOpinionExplanation)
            {
                __result = initiator ? "CantRomanceInitiateMessageDowned".Translate(pawn).CapitalizeFirst() : "CantRomanceTargetDowned".Translate();
                return false;
            }

            Pawn_StoryTracker story = pawn.story;
            if (story != null && story.traits?.HasTrait(TraitDefOf.Asexual) == true)
            {
                if (!initiator || forOpinionExplanation)
                {
                    __result = AcceptanceReport.WasRejected;
                    return false;
                }

                __result = "CantRomanceInitiateMessageAsexual".Translate(pawn).CapitalizeFirst();
                return false;
            }

            if (initiator && !pawn.health.capacities.CapableOf(PawnCapacityDefOf.Talking))
            {
                if (!forOpinionExplanation)
                {
                    __result = "CantRomanceInitiateMessageTalk".Translate(pawn).CapitalizeFirst();
                    return false;
                }

                __result = AcceptanceReport.WasRejected;
                return false;
            }

            if (pawn.Drafted && !forOpinionExplanation)
            {
                __result = initiator ? "CantRomanceInitiateMessageDrafted".Translate(pawn).CapitalizeFirst() : "CantRomanceTargetDrafted".Translate();
                return false;
            }

            if (initiator && pawn.IsSlave)
            {
                if (!forOpinionExplanation)
                {
                    __result = "CantRomanceInitiateMessageSlave".Translate(pawn).CapitalizeFirst();
                    return false;
                }

                __result = AcceptanceReport.WasRejected;
                return false;
            }

            if (pawn.MentalState != null)
            {
                __result = (initiator && !forOpinionExplanation) ? "CantRomanceInitiateMessageMentalState".Translate(pawn).CapitalizeFirst() : "CantRomanceTargetMentalState".Translate();
                return false;
            }

            __result = true;
            return false;
        }
    }

    [HarmonyPatch(typeof(RelationsUtility), "RomanceEligiblePair")]
    internal static class Patch_RomanceEligiblePair
    {
        static bool Prefix(ref AcceptanceReport __result, Pawn initiator, Pawn target, bool forOpinionExplanation)
        {
            float romanceAge = SimpleRomanceMod.settings.romanceAgeThreshold;

            if (initiator == target)
            {
                __result = false;
                return false;
            }

            DirectPawnRelation directPawnRelation = LovePartnerRelationUtility.ExistingLoveRealtionshipBetween(initiator, target, allowDead: false);
            if (directPawnRelation != null)
            {
                string genderSpecificLabel = directPawnRelation.def.GetGenderSpecificLabel(target);
                __result = "RomanceChanceExistingRelation".Translate(initiator.Named("PAWN"), genderSpecificLabel.Named("RELATION"));
                return false;
            }

            if (!RomanceEligible(initiator, initiator: true, forOpinionExplanation))
            {
                __result = false;
                return false;
            }

            if (forOpinionExplanation && target.ageTracker.AgeBiologicalYearsFloat < romanceAge)
            {
                __result = "CantRomanceTargetYoung".Translate();
                return false;
            }

            if (forOpinionExplanation && target.IsPrisoner)
            {
                __result = "CantRomanceTargetPrisoner".Translate();
                return false;
            }

            if (!AttractedToGender(initiator, target.gender) || !AttractedToGender(target, initiator.gender))
            {
                if (!forOpinionExplanation)
                {
                    __result = AcceptanceReport.WasRejected;
                    return false;
                }

                __result = "CantRomanceTargetSexuality".Translate();
                return false;
            }

            AcceptanceReport acceptanceReport = RomanceEligible(target, initiator: false, forOpinionExplanation);
            if (!acceptanceReport)
            {
                __result = acceptanceReport;
                return false;
            }

            if (target.relations.OpinionOf(initiator) <= 5)
            {
                __result = "CantRomanceTargetOpinion".Translate();
                return false;
            }

            if (!forOpinionExplanation && InteractionWorker_RomanceAttempt.SuccessChance(initiator, target, 1f) <= 0f)
            {
                __result = "CantRomanceTargetZeroChance".Translate();
                return false;
            }

            if ((!forOpinionExplanation && !initiator.CanReach(target, PathEndMode.Touch, Danger.Deadly)) || target.IsForbidden(initiator))
            {
                __result = "CantRomanceTargetUnreachable".Translate();
                return false;
            }

            if (initiator.relations.IsTryRomanceOnCooldown)
            {
                __result = "RomanceOnCooldown".Translate();
                return false;
            }

            __result = true;
            return false;
        }

        private static AcceptanceReport RomanceEligible(Pawn pawn, bool initiator, bool forOpinionExplanation)
        {
            float romanceAge = SimpleRomanceMod.settings.romanceAgeThreshold;

            if (pawn.ageTracker.AgeBiologicalYearsFloat < romanceAge)
            {
                return false;
            }

            if (pawn.IsPrisoner)
            {
                if (!initiator || forOpinionExplanation)
                {
                    return AcceptanceReport.WasRejected;
                }

                return "CantRomanceInitiateMessagePrisoner".Translate(pawn).CapitalizeFirst();
            }

            if (pawn.Downed && !forOpinionExplanation)
            {
                return initiator ? "CantRomanceInitiateMessageDowned".Translate(pawn).CapitalizeFirst() : "CantRomanceTargetDowned".Translate();
            }

            Pawn_StoryTracker story = pawn.story;
            if (story != null && story.traits?.HasTrait(TraitDefOf.Asexual) == true)
            {
                if (!initiator || forOpinionExplanation)
                {
                    return AcceptanceReport.WasRejected;
                }

                return "CantRomanceInitiateMessageAsexual".Translate(pawn).CapitalizeFirst();
            }

            if (initiator && !pawn.health.capacities.CapableOf(PawnCapacityDefOf.Talking))
            {
                if (!forOpinionExplanation)
                {
                    return "CantRomanceInitiateMessageTalk".Translate(pawn).CapitalizeFirst();
                }

                return AcceptanceReport.WasRejected;
            }

            if (pawn.Drafted && !forOpinionExplanation)
            {
                return initiator ? "CantRomanceInitiateMessageDrafted".Translate(pawn).CapitalizeFirst() : "CantRomanceTargetDrafted".Translate();
            }

            if (initiator && pawn.IsSlave)
            {
                if (!forOpinionExplanation)
                {
                    return "CantRomanceInitiateMessageSlave".Translate(pawn).CapitalizeFirst();
                }

                return AcceptanceReport.WasRejected;
            }

            if (pawn.MentalState != null)
            {
                return (initiator && !forOpinionExplanation) ? "CantRomanceInitiateMessageMentalState".Translate(pawn).CapitalizeFirst() : "CantRomanceTargetMentalState".Translate();
            }

            return true;
        }

        private static bool AttractedToGender(Pawn pawn, Gender gender)
        {
            Pawn_StoryTracker story = pawn.story;
            if (story != null && story.traits?.HasTrait(TraitDefOf.Asexual) == true)
            {
                return false;
            }

            Pawn_StoryTracker story2 = pawn.story;
            if (story2 != null && story2.traits?.HasTrait(TraitDefOf.Bisexual) == true)
            {
                return true;
            }

            Pawn_StoryTracker story3 = pawn.story;
            if (story3 != null && story3.traits?.HasTrait(TraitDefOf.Gay) == true)
            {
                return pawn.gender == gender;
            }

            return pawn.gender != gender;
        }
    }
}
