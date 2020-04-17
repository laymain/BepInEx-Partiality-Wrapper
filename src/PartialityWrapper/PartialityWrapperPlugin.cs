using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using Mono.Cecil;
using MonoMod;
using MonoMod.RuntimeDetour.HookGen;
using Partiality.Modloader;

namespace PartialityWrapper
{
    [BepInPlugin("com.laymain.unity.modding.bepinex.partialitywrapper", nameof(PartialityWrapper), "3.0.0")]
    public class PartialityWrapperPlugin : BaseUnityPlugin
    {
        private static readonly string ManagedFolder = Paths.ManagedPath;
        private static readonly string PluginFolder = Path.Combine(Paths.PluginPath, nameof(PartialityWrapper));
        private static readonly string ModsFolder = Path.Combine(Paths.GameRootPath, "Mods");

        void Awake()
        {
            try
            {
                LoadDependencies();
                LoadHooks();
                LoadMods();
            }
            catch (Exception e)
            {
                Log.Error("An unexpected error has occurred", e);
            }
        }

        private void LoadHooks()
        {
            string asmPath = Path.Combine(ManagedFolder, "Assembly-CSharp.dll");
            string hooksPath = Path.Combine(PluginFolder, "HOOKS-Assembly-CSharp.dll");

            if (File.Exists(hooksPath))
            {
                // if HOOKS file is older than the Assembly-Csharp file...
                if (File.GetLastWriteTime(hooksPath) < File.GetLastWriteTime(asmPath))
                {
                    Log.Info("HOOKS file is out of date, generating a new one...");
                    File.Delete(hooksPath);
                }
                else
                {
                    return;
                }
            }

            using (var modder = new MonoModder
            {
                InputPath = asmPath,
                OutputPath = hooksPath,
                PublicEverything = true,
                DependencyDirs = new List<string> {ManagedFolder, PluginFolder}
            })
            {
                modder.Read();
                modder.MapDependencies();
                Log.Info($"Generating {new FileInfo(hooksPath).Name}...");
                var generator = new HookGenerator(modder, Path.GetFileName(hooksPath));
                using (ModuleDefinition module = generator.OutputModule)
                {
                    generator.HookPrivate = true;
                    generator.Generate();
                    module.Write(hooksPath);
                }
            }
            Assembly.Load(File.ReadAllBytes(hooksPath));
        }

        private static void LoadDependencies()
        {
            Log.Info("Loading dependencies...");
            IEnumerable<string> dependencies = (
                from filepath in Directory.GetFiles(PluginFolder, "*.dll")
                where filepath.EndsWith(".dll") || filepath.EndsWith(".exe")
                select filepath
            ).AsEnumerable();
            foreach (string filepath in dependencies)
            {
                Log.Info($"\t{new FileInfo(filepath).Name}");
                Assembly.Load(File.ReadAllBytes(filepath));
            }

            Log.Info("Dependencies loaded");
        }

        private static void LoadMods()
        {
            Log.Info("Loading mods...");
            var assemblies = new List<Assembly>();
            // First load assemblies without getting types to avoid referencing issues
            foreach (string filepath in Directory.GetFiles(ModsFolder, "*.dll", SearchOption.AllDirectories))
            {
                string filename = new FileInfo(filepath).Name;
                try
                {
                    assemblies.Add(Assembly.Load(File.ReadAllBytes(filepath)));
                }
                catch (Exception e)
                {
                    Log.Error($"Could not load assembly {filename}", e);
                }
            }
            // Then look for mods in loaded assemblies
            foreach (Assembly assembly in assemblies)
            {
                IEnumerable<PartialityMod> mods = (
                    from type in assembly.GetTypes()
                    where type.IsSubclassOf(typeof(PartialityMod))
                    select (PartialityMod) Activator.CreateInstance(type)
                ).OrderBy(mod => mod.loadPriority).AsEnumerable();
                foreach (PartialityMod mod in mods)
                {
                    string label = $"{mod.ModID}@{mod.Version}";
                    try
                    {
                        mod.EnableMod();
                        Log.Info($"\t{label}");
                    }
                    catch (Exception e)
                    {
                        Log.Error($"Could not enable {label}", e);
                    }
                }
            }

            Log.Info("Mods loaded");
        }
    }
}
