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
        var winningOverrides = state.LoadOrder.PriorityOrder.Where(x => x.ModKey.FileName.String != "ZMR NPC Overhaul.esl").Npc().WinningOverrides().Where(x => OverhaulFormIDs.Contains(x.FormKey)).ToList();
        var masterFileNames  = NpcOverhaul.MasterReferences.Select(x => x.Master.FileName).ToList();
        var MasterFiles = state.LoadOrder.PriorityOrder.Reverse().Where(x => masterFileNames.Contains(x.ModKey.FileName)).ToList();
        var NPCMasters = MasterFiles.Select(x => x.Mod).NotNull().SelectMany(x => x.Npcs).Where(x => OverhaulFormIDs.Contains(x.FormKey)).ToList();

        var allOverrides = state.LoadOrder.PriorityOrder.Reverse().Select(x => x.Mod).NotNull().SelectMany(x => x.Npcs).Where(x => OverhaulFormIDs.Contains(x.FormKey)).ToList();

        var npcLoadOrder = state.LoadOrder.PriorityOrder.Where(x => x.ModKey.FileName.String != "ZMR NPC Overhaul.esl").Select(x => x.Mod).NotNull().Where(x => x.Npcs.Any()).ToArray();

        var totalNpcs = NpcOverhaul.Npcs.Count();
        var processedNpcs = 0;

        var npcToReferenceCount = new Dictionary<Mutagen.Bethesda.Plugins.FormKey, int>();

        foreach (var npc in NpcOverhaul.Npcs)
        {
            npcToReferenceCount[npc.FormKey] = 0;
        }

        void CountReferences(INpcGetter[] npcs)
        {
            foreach (var npc in npcs)
            {
                foreach (var plugin in npcLoadOrder)
                {
                    if (plugin.Npcs.Select(x => x.FormKey).Contains(npc.FormKey))
                    {
                        npcToReferenceCount[npc.FormKey]++;
                    }
                }
            }
        }

        var tasks = new List<Task>();
        // Divide NpcOverhaul.Npcs into 30 separate chunk arrays and process them in parallel.
        var chunkSize = (int)Math.Ceiling((double)NpcOverhaul.Npcs.Count() / 30);
        var npcChunks = NpcOverhaul.Npcs.Chunk(chunkSize).ToArray();
        for (int i = 0; i < npcChunks.Length; i++)
        {
            var chunk = npcChunks[i];
            tasks.Add(Task.Run(() => CountReferences(chunk)));
        }

        Task.WaitAll(tasks);

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

            Console.WriteLine($"Processing NPC {processedNpcs + 1}/{totalNpcs}: {npc.FormKey} - {npc.EditorID}");
            processedNpcs++;

            var recordReferenceCount = npcToReferenceCount[npc.FormKey];

            var patchNpc = state.PatchMod.Npcs.GetOrAddAsOverride(winningOverride);

            patchNpc.TintLayers.Clear();
            patchNpc.TintLayers.AddRange(npc.TintLayers.Select(x => x.DeepCopy()));

            patchNpc.HeadParts.Clear();
            patchNpc.HeadParts.AddRange(npc.HeadParts);

            patchNpc.FaceMorph = npc.FaceMorph?.DeepCopy();

            patchNpc.TextureLighting = npc.TextureLighting;
            patchNpc.Height = npc.Height;
            patchNpc.Weight = npc.Weight;

            if (npc.FaceParts is not null)
            {
                patchNpc.FaceParts?.Clear();
                patchNpc.FaceParts = npc.FaceParts.DeepCopy();
            }
            else
            {
                patchNpc.FaceParts = null;
            }

            if (!npc.HeadTexture.IsNull)
            {
                patchNpc.HeadTexture.FormKey = npc.HeadTexture.FormKey;
            }
            else
            {
                patchNpc.HeadTexture.Clear();
            }

            if (!npc.HairColor.IsNull)
            {
                patchNpc.HairColor.FormKey = npc.HairColor.FormKey;
            }
            else
            {
                patchNpc.HairColor.Clear();
            }

            if (!npc.WornArmor.IsNull)
            {
                patchNpc.WornArmor.FormKey = npc.WornArmor.FormKey;
            }
            else
            {
                patchNpc.WornArmor.Clear();
            }

            // BUG: If winning overrides is length of 1, meaning that it's the master mod
            // and my patch, then the Name and ShortName fields will be null.
            // Not sure why this happens, but copy from ZMR NPC Overhaul.esl to my patch  to fix this issue.
            if (recordReferenceCount == 1)
            {
                if (npc.Name?.String is not null)
                {
                    patchNpc.Name = npc.Name.DeepCopy();
                }

                if (npc.ShortName?.String is not null)
                {
                    patchNpc.ShortName = npc.ShortName.DeepCopy();
                }
            }

    
            if (npc.DefaultOutfit.FormKey != patchNpc.DefaultOutfit.FormKey)
            {
                patchNpc.DefaultOutfit.FormKey = npc.DefaultOutfit.FormKey;
            }

            if (npc.SleepingOutfit.FormKey != patchNpc.SleepingOutfit.FormKey)
            {
                patchNpc.SleepingOutfit.FormKey = npc.SleepingOutfit.FormKey;
            }
        }
    }
}