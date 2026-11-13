using AlwaysWinFights;
using BepInEx;
using BepInEx.Configuration;
using GK2.Framework;
using UnityEngine;

namespace AlwaysWinFightsFrameworkBridge
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency(MainPlugin.PluginGuid, BepInDependency.DependencyFlags.HardDependency)]
    [BepInDependency("ru.superman4eg.gk2.framework", BepInDependency.DependencyFlags.HardDependency)] // Твой проверенный ID зависимости!
    public sealed class FrameworkBridgePlugin : BaseUnityPlugin
    {
        // Уникальный GUID для моста, чтобы убрать зацикливание Chainloader
        public const string PluginGuid = "com.sondju.AlwaysWinFights.framework";
        // Убрали приписку Integration, чтобы текст не мельчал в меню игры
        public const string PluginName = "Always Win Fights";
        public const string PluginVersion = "1.2.0";

        private void Awake()
        {
            MainPlugin main = MainPlugin.Instance;
            if (main == null)
            {
                Logger.LogError("Main mod instance is unavailable.");
                return;
            }

            // Передаем основной экземпляр и его конфиг
            FrameworkApi.RegisterMod(new FrameworkBridge(main), main.Config);
            Logger.LogInfo("Always Win Fights - Framework Integration Bridge loaded successfully!");
        }

        private sealed class FrameworkBridge : Gk2ModBase
        {
            private readonly MainPlugin main;

            private readonly Gk2ModMetadata metadata = new Gk2ModMetadata(
                PluginGuid, // Передаем GUID моста
                PluginName,
                "sondju",
                PluginVersion,
                "Optional GK2 Mod Framework integration for Always Win Fights.",
                supportsRuntimeToggle: true,
                requiresKnownBuild: false,
                frameworkManagesEnabledState: false); // Управление тумблером идет через оригинальный .cfg

            internal FrameworkBridge(MainPlugin main) { this.main = main; }
            public override Gk2ModMetadata Metadata => metadata;
            public override System.Collections.Generic.IReadOnlyList<Gk2ModDependency> Dependencies => null;

            public override void OnRegister(Gk2ModContext context)
            {
                // Привязываем те же самые Section и Key, чтобы фреймворк отображал конфиги основного автономного мода
                context.Settings.AddToggle(
                    "1. General", "EnableMod", true, "Enable Mod", "Enable/Disable mod completely."
                );

                context.Settings.AddToggle(
                    "2. Battle", "AlwaysWin", true, "Always Win",
                    "If true, finishes the battle with a win instantly when combat waves start." +
                    " If you not need win always, disable this and use key from KeypressWin."
                );

                context.Settings.AddKeybind(
                    "2. Battle", "KeypressWin", new KeyboardShortcut(KeyCode.Delete), "Keypress Win",
                    "Key or combination for win battle. Use it if you have bug with standard win battle."
                );

                context.Settings.AddReadOnly(
                    "Status", "Integration", "Framework Integration",
                    "Shows whether the optional bridge is active.", () => main != null ? "Active" : "Unavailable");
            }

            public override void OnEnable() { }
            public override void OnDisable() { }
            public override void OnGameStarted() { }
            public override void OnReturnedToMainMenu() { }
        }
    }
}