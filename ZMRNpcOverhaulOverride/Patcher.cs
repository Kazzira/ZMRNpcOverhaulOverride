using Mutagen.Bethesda;
using Mutagen.Bethesda.Synthesis;
using Mutagen.Bethesda.Skyrim;
using Noggog;

namespace ZMRNpcOverhaulOverride;

// I used the AI Overhual patcher as a reference as I did not know how to do this partial override stuff.

public class Patcher(IPatcherState<ISkyrimMod, ISkyrimModGetter> state)
{
    public void Run()
    {
        var NpcOverhaul = state.LoadOrder.PriorityOrder.FirstOrDefault(x => x.ModKey.FileName.String == "ZMR NPC Overhaul.esl")?.Mod;

        if (NpcOverhaul is null)
        {
            Console.WriteLine("ZMR NPC Overhaul.esl not found in load order.");
            return;
        }

        var OverhaulFormIDs = NpcOverhaul.Npcs.Select(x => x.FormKey).ToList();
        var winningOverrides = state.LoadOrder.PriorityOrder.Npc().WinningOverrides().Where(x => OverhaulFormIDs.Contains(x.FormKey)).ToList();
        var masterFileNames  = NpcOverhaul.MasterReferences.Select(x => x.Master.FileName).ToList();
        var MasterFiles = state.LoadOrder.PriorityOrder.Reverse().Where(x => masterFileNames.Contains(x.ModKey.FileName)).ToList();
        var NPCMasters = MasterFiles.Select(x => x.Mod).NotNull().SelectMany(x => x.Npcs).Where(x => OverhaulFormIDs.Contains(x.FormKey)).ToList();

        var allOverrides = state.LoadOrder.PriorityOrder.Reverse().Select(x => x.Mod).NotNull().SelectMany(x => x.Npcs).Where(x => OverhaulFormIDs.Contains(x.FormKey)).ToList();

        foreach (var npc in NpcOverhaul.Npcs)
        {
            var winningOverride = winningOverrides.First(x => x.FormKey == npc.FormKey);
            var masters = NPCMasters.Where(x => x.FormKey == npc.FormKey).ToList();
            var winningMaster = masters.FirstOrDefault();
            
            if (winningMaster is null)
            {
                winningMaster = state.LoadOrder.PriorityOrder.Select(x => x.Mod).NotNull().SelectMany(x => x.Npcs).First(x => x.FormKey == npc.FormKey);
            }
            
            var overrides = allOverrides.Where(x => x.FormKey == npc.FormKey).ToList();

            var patchNpc = state.PatchMod.Npcs.GetOrAddAsOverride(winningOverride);

            patchNpc.TintLayers.Clear();
            patchNpc.TintLayers.AddRange(npc.TintLayers.Select(x => x.DeepCopy()));

            patchNpc.HeadParts.Clear();
            patchNpc.HeadParts.AddRange(npc.HeadParts);

            patchNpc.FaceMorph = npc.FaceMorph?.DeepCopy();

            patchNpc.TextureLighting = npc.TextureLighting;
            patchNpc.Height = npc.Height;

            if (npc.FaceParts is not null)
            {
                patchNpc.FaceParts?.Clear();
                patchNpc.FaceParts = npc.FaceParts.DeepCopy();
            }

            patchNpc.HeadTexture.FormKey = npc.HeadTexture.FormKey;

            patchNpc.HairColor.FormKey = npc.HairColor.FormKey;

            // For the outfits, keep the overhaul if present, however if there exists an override with Zod Mod Resources.esm,
            // then use that instead.
    
            if (npc.DefaultOutfit.FormKey.ModKey.FileName.String == "Zod Mod Resources.esm" && patchNpc.DefaultOutfit.FormKey.ModKey.FileName.String != "Zod Mod Resources.esm")
            {
                patchNpc.DefaultOutfit.FormKey = npc.DefaultOutfit.FormKey;
            }

            if (patchNpc.SleepingOutfit.FormKey.ModKey.FileName.String == "Zod Mod Resources.esm" && npc.SleepingOutfit.FormKey.ModKey.FileName.String != "Zod Mod Resources.esm")
            {
                patchNpc.SleepingOutfit.FormKey = npc.SleepingOutfit.FormKey;
            }
        }
    }
}