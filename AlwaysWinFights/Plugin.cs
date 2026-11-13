using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Reflection;
using UnityEngine;
using Input = UnityEngine.Input;

namespace AlwaysWinFights
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class MainPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.sondju.AlwaysWinFights";
        public const string PluginName = "Always Win Fights";
        public const string PluginVersion = "1.2.0";

        public static MainPlugin Instance { get; private set; }
        public static ManualLogSource Log;

        // Конфиги BepInEx, которые также подхватит фреймворк при наличии
        public ConfigEntry<bool> EnableMod { get; private set; }
        public ConfigEntry<bool> AlwaysWin { get; private set; }
        public ConfigEntry<KeyboardShortcut> KeypressWin { get; private set; }

        private void Awake()
        {
            Instance = this;
            Log = Logger;

            EnableMod = Config.Bind("1. General", "EnableMod", true, "Enable/Disable mod completely.");

            AlwaysWin = Config.Bind("2. Battle", "AlwaysWin", true,
                "If true, finishes the battle with a win instantly when combat waves start." +
                " If you not need win always, disable this and use key from KeypressWin.");

            KeypressWin = Config.Bind( "2. Battle", "KeypressWin", new KeyboardShortcut(KeyCode.Delete),
                "Key or combination for win battle. Use it if you have bug with standard win battle.");

            var harmony = new Harmony(PluginGuid);

            try
            {
                var playMethod = typeof(FightingGameController).GetMethod("Play", BindingFlags.Public | BindingFlags.Instance,
                    null, CallingConventions.Any, new Type[] { }, null);

                if (playMethod != null)
                {
                    var postfixMethod = typeof(InstantWinOnActiveFightPatch).GetMethod("Postfix");
                    harmony.Patch(playMethod, postfix: new HarmonyMethod(postfixMethod));
                }

                var playWithIdMethod = typeof(FightingGameController).GetMethod("Play", BindingFlags.Public | BindingFlags.Instance,
                    null, CallingConventions.Any, new Type[] { typeof(string) }, null);

                if (playWithIdMethod != null)
                {
                    var postfixMethod = typeof(InstantWinOnActiveFightPatch).GetMethod("Postfix");
                    harmony.Patch(playWithIdMethod, postfix: new HarmonyMethod(postfixMethod));
                }

                var lostMethod = typeof(FightingGameController).GetMethod("FinishAsLost", BindingFlags.Public | BindingFlags.Instance);
                if (lostMethod != null)
                {
                    var prefixMethod = typeof(InstantLostToWinPatch).GetMethod("Prefix");
                    harmony.Patch(lostMethod, prefix: new HarmonyMethod(prefixMethod));
                }

                Log.LogInfo($"{PluginName} loaded successfully in standalone mode!");
            }
            catch (Exception ex)
            {
                Log.LogError($"Harmony not injected: {ex.Message}");
            }
        }

        public static class InstantWinOnActiveFightPatch
        {
            public static void Postfix(FightingGameController __instance)
            {
                // Сначала базовые проверки на включение самого мода и мгновенной победы
                if (Instance == null || !Instance.EnableMod.Value || !Instance.AlwaysWin.Value)
                    return;

                try
                {
                    // Проверка, что текущий бой не из вступления (его нужно проиграть)
                    var currentLevelField = AccessTools.Field(typeof(FightingGameController), "currentLevel");
                    if (currentLevelField != null)
                    {
                        var currentLevel = (FightingLevel)currentLevelField.GetValue(__instance);
                        if (currentLevel != null && currentLevel.id == "fighting_level_ev_3")
                            return;
                    }

                    // Завершение боя победой
                    __instance.FinishAsWon();
                }
                catch (Exception ex)
                {
                    Log.LogError("[AlwaysWinFights] Error win in: " + ex.Message);
                }
            }
        }

        public static class InstantLostToWinPatch
        {
            public static bool Prefix(FightingGameController __instance)
            {
                // 1. Сначала базовые проверки на включение самого мода
                if (Instance == null || !Instance.EnableMod.Value || !Instance.AlwaysWin.Value) return true;


                // Обернули логику рефлексии и триггера в try-catch для полной стабильности!
                try
                {
                    var currentLevelField = AccessTools.Field(typeof(FightingGameController), "currentLevel");
                    if (currentLevelField != null)
                    {
                        var currentLevel = (FightingLevel)currentLevelField.GetValue(__instance);
                        // Защита от сюжетного уровня (финал волн), чтобы не сломать кат-сцену
                        if (currentLevel != null && currentLevel.id == "fighting_level_ev_3")
                            return true;
                    }

                    __instance.FinishAsWon();
                    return false; // Успешно победили, прерываем ванильный метод проигрыша!
                }
                catch (Exception ex)
                {
                    Log.LogError("[AlwaysWinFights] Error in lost prefix hook: " + ex.Message);
                    return true; // В случае аварии мягко отдаем управление ванильной игре, чтобы не повесить бой!
                }
            }
        }

        // --- ЕЖЕКАДРОВЫЙ ПЕРЕХВАТ КНОПКИ ПОБЕДЫ ПОСРЕДИ БОЯ ---
        private void Update()
        {
            if (!EnableMod.Value) return;

            // БРОНЕБОЙНЫЙ ПЕРЕХВАТ: Проверяем клавишу на глобальном уровне BepInEx!
            KeyCode configuredKey = KeypressWin.Value.MainKey;

            if (configuredKey != KeyCode.None && Input.GetKeyDown(configuredKey))
            {
                // Если кнопка нажата — ищем, идет ли сейчас бой
                var fightController = FindAnyObjectByType<FightingGameController>();

                if (fightController != null)
                {
                    try
                    {
                        // Проверяем стейт боя через рефлексию
                        var stateField = AccessTools.Field(typeof(FightingGameController), "fightState");
                        if (stateField != null)
                        {
                            FightState currentState = (FightState)stateField.GetValue(fightController);

                            // Если бой активен — наглухо закрываем его победой!
                            if (currentState == FightState.ActiveFight)
                            {
                                Log.LogInfo("[AlwaysWinFights] GLOBAL HOTKEY DETECTED! Forcing victory.");
                                fightController.FinishAsWon();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.LogError("[AlwaysWinFights] Global hotkey victory crash: " + ex.Message);
                    }
                }
            }
        }
    }
}