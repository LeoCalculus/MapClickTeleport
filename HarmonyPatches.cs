using System.Collections.Generic;
using System.Linq;
using System.Text;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Monsters;

namespace MapClickTeleport
{
    /// <summary>
    /// Controls for hiding in-game HUD elements
    /// Uses Harmony patches on drawHUD for reliable hiding
    /// </summary>
    public static class HUDHider
    {
        // Toggle for hiding health/stamina bars (combined)
        public static bool HideHealthStamina { get; set; } = false;

        // Toggle for hiding the entire HUD
        public static bool HideEntireHUD { get; set; } = false;

        // Individual toggles for separate control
        public static bool HideHealthBar { get; set; } = false;
        public static bool HideStaminaBar { get; set; } = false;

        // Store original values to restore after draw


        public static void ApplyPatches(Harmony harmony)
        {
            harmony.Patch(
                original: AccessTools.Method(typeof(Game1), "drawHUD"),
                prefix: new HarmonyMethod(typeof(HUDHider), nameof(DrawHUD_Prefix))
            );

            // Patch DrawString to hide text in health/stamina areas
            harmony.Patch(
                original: AccessTools.Method(typeof(SpriteBatch), nameof(SpriteBatch.DrawString),
                    new[] { typeof(SpriteFont), typeof(string), typeof(Vector2), typeof(Color) }),
                prefix: new HarmonyMethod(typeof(HUDHider), nameof(DrawString_Prefix))
            );

            // Patch DrawString with more parameters
            harmony.Patch(
                original: AccessTools.Method(typeof(SpriteBatch), nameof(SpriteBatch.DrawString),
                    new[] { typeof(SpriteFont), typeof(string), typeof(Vector2), typeof(Color), typeof(float), typeof(Vector2), typeof(float), typeof(SpriteEffects), typeof(float) }),
                prefix: new HarmonyMethod(typeof(HUDHider), nameof(DrawString_Full_Prefix))
            );

            // Patch StringBuilder DrawString overloads (game sometimes uses these for HUD text)
            harmony.Patch(
                original: AccessTools.Method(typeof(SpriteBatch), nameof(SpriteBatch.DrawString),
                    new[] { typeof(SpriteFont), typeof(StringBuilder), typeof(Vector2), typeof(Color) }),
                prefix: new HarmonyMethod(typeof(HUDHider), nameof(DrawString_SB_Prefix))
            );

            harmony.Patch(
                original: AccessTools.Method(typeof(SpriteBatch), nameof(SpriteBatch.DrawString),
                    new[] { typeof(SpriteFont), typeof(StringBuilder), typeof(Vector2), typeof(Color), typeof(float), typeof(Vector2), typeof(float), typeof(SpriteEffects), typeof(float) }),
                prefix: new HarmonyMethod(typeof(HUDHider), nameof(DrawString_SB_Full_Prefix))
            );

            // Patch ALL SpriteBatch.Draw overloads

            // 1. Draw(Texture2D, Vector2, Rectangle?, Color, float, Vector2, float, SpriteEffects, float)
            harmony.Patch(
                original: AccessTools.Method(typeof(SpriteBatch), nameof(SpriteBatch.Draw),
                    new[] { typeof(Texture2D), typeof(Vector2), typeof(Rectangle?), typeof(Color), typeof(float), typeof(Vector2), typeof(float), typeof(SpriteEffects), typeof(float) }),
                prefix: new HarmonyMethod(typeof(HUDHider), nameof(Draw_Full_Prefix))
            );

            // 2. Draw(Texture2D, Vector2, Rectangle?, Color)
            harmony.Patch(
                original: AccessTools.Method(typeof(SpriteBatch), nameof(SpriteBatch.Draw),
                    new[] { typeof(Texture2D), typeof(Vector2), typeof(Rectangle?), typeof(Color) }),
                prefix: new HarmonyMethod(typeof(HUDHider), nameof(Draw_Vec_Rect_Color_Prefix))
            );

            // 3. Draw(Texture2D, Vector2, Color)
            harmony.Patch(
                original: AccessTools.Method(typeof(SpriteBatch), nameof(SpriteBatch.Draw),
                    new[] { typeof(Texture2D), typeof(Vector2), typeof(Color) }),
                prefix: new HarmonyMethod(typeof(HUDHider), nameof(Draw_Vec_Color_Prefix))
            );

            // 4. Draw(Texture2D, Rectangle, Rectangle?, Color)
            harmony.Patch(
                original: AccessTools.Method(typeof(SpriteBatch), nameof(SpriteBatch.Draw),
                    new[] { typeof(Texture2D), typeof(Rectangle), typeof(Rectangle?), typeof(Color) }),
                prefix: new HarmonyMethod(typeof(HUDHider), nameof(Draw_Rect_Rect_Color_Prefix))
            );

            // 5. Draw(Texture2D, Rectangle, Color)
            harmony.Patch(
                original: AccessTools.Method(typeof(SpriteBatch), nameof(SpriteBatch.Draw),
                    new[] { typeof(Texture2D), typeof(Rectangle), typeof(Color) }),
                prefix: new HarmonyMethod(typeof(HUDHider), nameof(Draw_Rect_Color_Prefix))
            );

            // 6. Draw(Texture2D, Rectangle, Rectangle?, Color, float, Vector2, SpriteEffects, float)
            harmony.Patch(
                original: AccessTools.Method(typeof(SpriteBatch), nameof(SpriteBatch.Draw),
                    new[] { typeof(Texture2D), typeof(Rectangle), typeof(Rectangle?), typeof(Color), typeof(float), typeof(Vector2), typeof(SpriteEffects), typeof(float) }),
                prefix: new HarmonyMethod(typeof(HUDHider), nameof(Draw_Rect_Full_Prefix))
            );

            // 7. Draw(Texture2D, Vector2, Rectangle?, Color, float, Vector2, Vector2, SpriteEffects, float)
            harmony.Patch(
                original: AccessTools.Method(typeof(SpriteBatch), nameof(SpriteBatch.Draw),
                    new[] { typeof(Texture2D), typeof(Vector2), typeof(Rectangle?), typeof(Color), typeof(float), typeof(Vector2), typeof(Vector2), typeof(SpriteEffects), typeof(float) }),
                prefix: new HarmonyMethod(typeof(HUDHider), nameof(Draw_Vec_Scale_Prefix))
            );
        }

        private static bool DrawHUD_Prefix()
        {
            if (HideEntireHUD)
                return false;

            if (HideHealthBar)
            {
                Game1.showingHealth = false;
                Game1.showingHealthBar = false;
            }

            return true;
        }

        // Check if source rectangle is stamina bar sprite
        private static bool IsStaminaSprite(Rectangle? sourceRect)
        {
            if (!sourceRect.HasValue)
                return false;

            var r = sourceRect.Value;

            // Stamina bar top (E icon): 256, 408
            if (r.X == 256 && r.Y == 408)
                return true;

            // Stamina bar middle: 256, 424
            if (r.X == 256 && r.Y == 424)
                return true;

            // Stamina bar bottom: 256, 448
            if (r.X == 256 && r.Y == 448)
                return true;

            // Exhausted icon: 191, 406
            if (r.X == 191 && r.Y == 406)
                return true;

            return false;
        }

        // Check if source rectangle is health bar sprite
        private static bool IsHealthSprite(Rectangle? sourceRect)
        {
            if (!sourceRect.HasValue)
                return false;

            var r = sourceRect.Value;

            // Health bar sprites in Cursors.xnb
            // Heart icon area: 268, 408 (health icon)
            if (r.X == 268 && r.Y == 408)
                return true;

            // Health bar top: 268, 408
            if (r.X >= 268 && r.X <= 280 && r.Y >= 408 && r.Y <= 432)
                return true;

            // Health bar middle/fill area
            if (r.X >= 268 && r.X <= 280 && r.Y >= 424 && r.Y <= 456)
                return true;

            return false;
        }

        private static bool IsInHealthBarArea(Vector2 pos)
        {
            int w = Game1.uiViewport.Width;

            // Health bar is typically to the left of stamina bar, in the bottom right area
            return pos.X > w - 280 && pos.X < w - 100;
        }

        private static bool IsInHealthBarArea(Rectangle rect)
        {
            int w = Game1.uiViewport.Width;
            return rect.X > w - 280 && rect.X < w - 100;
        }

        private static bool IsInStaminaBarArea(Vector2 pos)
        {
            int w = Game1.uiViewport.Width;
            int h = Game1.uiViewport.Height;

            // Stamina bar is in the rightmost ~70 pixels, bottom ~500 pixels
            return pos.X > w - 180 && pos.Y < h; // originally 70 for x
        }

        private static bool IsInStaminaBarArea(Rectangle rect)
        {
            int w = Game1.uiViewport.Width;
            int h = Game1.uiViewport.Height;

            return rect.X > w - 180 && rect.Y < h;
        }

        private static bool ShouldBlock(Texture2D texture, Rectangle? sourceRect, Vector2 destPos)
        {
            // Check health bar hiding
            if (HideHealthBar)
            {
                if (IsInHealthBarArea(destPos) && IsHealthSprite(sourceRect))
                    return true;
            }

            // Check stamina bar hiding
            if (HideStaminaBar)
            {
                if (IsInStaminaBarArea(destPos))
                {
                    if (IsStaminaSprite(sourceRect))
                        return true;
                    if (texture == Game1.staminaRect)
                        return true;
                }
            }

            return false;
        }

        private static bool ShouldBlockRect(Texture2D texture, Rectangle? sourceRect, Rectangle destRect)
        {
            // Check health bar hiding
            if (HideHealthBar)
            {
                if (IsInHealthBarArea(destRect) && IsHealthSprite(sourceRect))
                    return true;
            }

            // Check stamina bar hiding
            if (HideStaminaBar)
            {
                if (IsInStaminaBarArea(destRect))
                {
                    if (IsStaminaSprite(sourceRect))
                        return true;
                    if (texture == Game1.staminaRect)
                        return true;
                }
            }

            return false;
        }

        // All prefixes call the same logic
        private static bool Draw_Full_Prefix(Texture2D texture, Vector2 position, Rectangle? sourceRectangle)
            => !ShouldBlock(texture, sourceRectangle, position);

        private static bool Draw_Vec_Rect_Color_Prefix(Texture2D texture, Vector2 position, Rectangle? sourceRectangle)
            => !ShouldBlock(texture, sourceRectangle, position);

        private static bool Draw_Vec_Color_Prefix(Texture2D texture, Vector2 position)
            => !ShouldBlock(texture, null, position);

        private static bool Draw_Rect_Rect_Color_Prefix(Texture2D texture, Rectangle destinationRectangle, Rectangle? sourceRectangle)
            => !ShouldBlockRect(texture, sourceRectangle, destinationRectangle);

        private static bool Draw_Rect_Color_Prefix(Texture2D texture, Rectangle destinationRectangle)
            => !ShouldBlockRect(texture, null, destinationRectangle);

        private static bool Draw_Rect_Full_Prefix(Texture2D texture, Rectangle destinationRectangle, Rectangle? sourceRectangle)
            => !ShouldBlockRect(texture, sourceRectangle, destinationRectangle);

        private static bool Draw_Vec_Scale_Prefix(Texture2D texture, Vector2 position, Rectangle? sourceRectangle)
            => !ShouldBlock(texture, sourceRectangle, position);

        // DrawString prefixes to hide text in health/stamina areas
        private static bool DrawString_Prefix(SpriteFont spriteFont, string text, Vector2 position, Color color)
            => !ShouldBlockText(position, text);

        private static bool DrawString_Full_Prefix(SpriteFont spriteFont, string text, Vector2 position, Color color)
            => !ShouldBlockText(position, text);

        // StringBuilder overloads
        private static bool DrawString_SB_Prefix(SpriteFont spriteFont, StringBuilder text, Vector2 position, Color color)
            => !ShouldBlockText(position, text?.ToString());

        private static bool DrawString_SB_Full_Prefix(SpriteFont spriteFont, StringBuilder text, Vector2 position, Color color)
            => !ShouldBlockText(position, text?.ToString());

        private static bool ShouldBlockText(Vector2 position, string? text = null)
        {
            int w = Game1.uiViewport.Width;
            int h = Game1.uiViewport.Height;

            // Block text in health bar area (numbers like "100/100")
            if (HideHealthBar)
            {
                // Health text is in the bottom right area, to the left of stamina bar
                // Expanded area to catch all health-related text
                if (position.X > w - 320 && position.X < w - 80 && position.Y > h - 300)
                    return true;
            }

            // Block text in stamina bar area
            if (HideStaminaBar)
            {
                // Stamina bar extends from the right edge, quite tall
                // Text can appear anywhere along the bar height
                if (position.X > w - 100 && position.Y > h - 600)
                    return true;

                // Also block stamina percentage/numbers that may appear near the bar
                if (position.X > w - 150 && position.Y > h - 200)
                {
                    // Additional check: if text contains numbers, likely stamina display
                    if (text != null && ContainsDigits(text))
                        return true;
                }
            }

            return false;
        }

        private static bool ContainsDigits(string text)
        {
            foreach (char c in text)
            {
                if (char.IsDigit(c))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Check if mouse is in hidden HUD area (to suppress tooltips)
        /// </summary>
        public static bool IsMouseInHiddenArea()
        {
            int mouseX = Game1.getMouseX();
            int mouseY = Game1.getMouseY();
            int w = Game1.uiViewport.Width;
            int h = Game1.uiViewport.Height;

            if (HideHealthBar)
            {
                if (mouseX > w - 300 && mouseX < w - 100 && mouseY > h - 200)
                    return true;
            }

            if (HideStaminaBar)
            {
                if (mouseX > w - 150 && mouseY > h - 200)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Call this every frame to apply HUD hiding (patches handle most work now)
        /// </summary>
        public static void UpdateHUD()
        {
            // Patches handle the actual hiding
        }

        /// <summary>
        /// Restore HUD to normal state
        /// </summary>
        public static void RestoreHUD()
        {
            if (HideEntireHUD)
            {
                Game1.displayHUD = true;
            }
        }
    }

    /// <summary>
    /// Harmony patches for monster drop modifications.
    /// Patches GameLocation.monsterDrop to modify drop chance and amount.
    /// </summary>
    public static class MonsterDropPatches
    {
        public static void ApplyPatches(Harmony harmony)
        {
            // Patch monsterDrop to modify drop behavior
            harmony.Patch(
                original: AccessTools.Method(typeof(GameLocation), nameof(GameLocation.monsterDrop)),
                prefix: new HarmonyMethod(typeof(MonsterDropPatches), nameof(MonsterDrop_Prefix)),
                postfix: new HarmonyMethod(typeof(MonsterDropPatches), nameof(MonsterDrop_Postfix))
            );

            // Patch MineShaft.populateLevel for spawn rate in mines
            var mineShaftType = AccessTools.TypeByName("StardewValley.Locations.MineShaft");
            if (mineShaftType != null)
            {
                var populateMethod = AccessTools.Method(mineShaftType, "populateLevel");
                if (populateMethod != null)
                {
                    harmony.Patch(
                        original: populateMethod,
                        postfix: new HarmonyMethod(typeof(MonsterDropPatches), nameof(PopulateLevel_Postfix))
                    );
                }
            }
        }

        /// <summary>
        /// Prefix patch for monsterDrop - modifies drop chance by manipulating luck temporarily
        /// </summary>
        private static void MonsterDrop_Prefix(Monster monster, int x, int y, Farmer who, ref double __state)
        {
            // Store original daily luck to restore later
            __state = Game1.player.team.sharedDailyLuck.Value;

            // Increase luck to boost drop chance if multiplier > 1
            float dropMult = ImGuiMenu.MonsterDropChanceMultiplier;
            if (dropMult > 1f)
            {
                // Boost daily luck to increase drop probability
                Game1.player.team.sharedDailyLuck.Value += (dropMult - 1f) * 0.1;
            }
        }

        /// <summary>
        /// Postfix patch for monsterDrop - adds bonus items and restores luck
        /// </summary>
        private static void MonsterDrop_Postfix(Monster monster, int x, int y, Farmer who, double __state)
        {
            // Restore original luck
            Game1.player.team.sharedDailyLuck.Value = __state;

            // Add bonus drops based on MonsterDropAmountBonus
            int bonusDrops = ImGuiMenu.MonsterDropAmountBonus;
            if (bonusDrops > 0 && monster != null)
            {
                // Get the monster's drop list and spawn extra items
                var location = monster.currentLocation ?? Game1.currentLocation;
                if (location != null)
                {
                    // Spawn additional copies of common monster drops
                    for (int i = 0; i < bonusDrops; i++)
                    {
                        // Try to spawn monster-specific loot
                        var extraLoot = monster.objectsToDrop;
                        if (extraLoot != null && extraLoot.Count > 0)
                        {
                            foreach (var itemId in extraLoot)
                            {
                                if (Game1.random.NextDouble() < 0.5) // 50% chance per bonus
                                {
                                    try
                                    {
                                        // Skip invalid/special item IDs (negative numbers, etc.)
                                        if (string.IsNullOrEmpty(itemId)) continue;
                                        if (int.TryParse(itemId, out int numericId) && numericId < 0) continue;

                                        var item = ItemRegistry.Create(itemId, allowNull: true);
                                        if (item != null)
                                        {
                                            Game1.createItemDebris(
                                                item,
                                                new Vector2(x, y),
                                                Game1.random.Next(4),
                                                location
                                            );
                                        }
                                    }
                                    catch
                                    {
                                        // Skip items that fail to create
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Postfix for MineShaft.populateLevel - spawns extra monsters based on spawn rate multiplier
        /// </summary>
        private static void PopulateLevel_Postfix(GameLocation __instance)
        {
            float spawnMult = ImGuiMenu.MonsterSpawnRateMultiplier;
            if (spawnMult <= 1f) return;

            // Count existing monsters and spawn extras
            var existingMonsters = __instance.characters.OfType<Monster>().ToList();
            int extraSpawns = (int)((spawnMult - 1f) * existingMonsters.Count);

            for (int i = 0; i < extraSpawns && existingMonsters.Count > 0; i++)
            {
                // Clone a random existing monster type at a nearby position
                var templateMonster = existingMonsters[Game1.random.Next(existingMonsters.Count)];
                var newPos = templateMonster.Position + new Vector2(
                    Game1.random.Next(-128, 129),
                    Game1.random.Next(-128, 129)
                );

                // Check if position is valid using simple passability check
                var tilePos = new Vector2((int)(newPos.X / 64), (int)(newPos.Y / 64));
                if (__instance.isTilePassable(new xTile.Dimensions.Location((int)tilePos.X, (int)tilePos.Y), Game1.viewport))
                {
                    try
                    {
                        // Create monster of same type
                        var monsterType = templateMonster.GetType();
                        var newMonster = (Monster?)Activator.CreateInstance(monsterType, newPos);
                        if (newMonster != null)
                        {
                            __instance.characters.Add(newMonster);
                        }
                    }
                    catch
                    {
                        // Some monsters may not have simple constructors, skip them
                    }
                }
            }
        }
    }

    /// <summary>
    /// Harmony patches for fishing modifications.
    /// Patches GameLocation.getFish to control fish vs trash chance.
    /// </summary>
    public static class FishingPatches
    {
        // List of trash item IDs to detect and potentially replace
        private static readonly HashSet<string> TrashItemIds = new()
        {
            "(O)168", "(O)169", "(O)170", "(O)171", "(O)172", // Trash, Driftwood, Broken Glasses, Broken CD, Soggy Newspaper
            "168", "169", "170", "171", "172"
        };

        // Track recursion to prevent infinite loops
        private static bool _isRetrying = false;

        public static void ApplyPatches(Harmony harmony)
        {
            // Patch getFish to modify catch results
            harmony.Patch(
                original: AccessTools.Method(typeof(GameLocation), nameof(GameLocation.getFish)),
                postfix: new HarmonyMethod(typeof(FishingPatches), nameof(GetFish_Postfix))
            );
        }

        /// <summary>
        /// Postfix for getFish - replaces trash with fish based on FishCatchChance setting
        /// </summary>
        private static void GetFish_Postfix(GameLocation __instance, ref Item __result,
            float millisecondsAfterNibble, string bait, int waterDepth, Farmer who,
            double baitPotency, Vector2 bobberTile)
        {
            int fishChance = ImGuiMenu.FishCatchChance;
            if (fishChance <= 0) return; // Use default game behavior
            if (__result == null) return;
            if (_isRetrying) return; // Prevent recursion

            // Check if the result is trash
            string itemId = __result.QualifiedItemId ?? __result.ItemId ?? "";
            bool isTrash = TrashItemIds.Contains(itemId) ||
                           TrashItemIds.Contains(__result.ItemId ?? "");

            if (isTrash)
            {
                // Roll to see if we should replace trash with fish
                if (Game1.random.Next(100) < fishChance)
                {
                    // Try to get a real fish by calling getFish again with boosted luck
                    var newFish = TryGetLocationFish(__instance, millisecondsAfterNibble, bait, waterDepth, who, baitPotency, bobberTile);
                    if (newFish != null)
                    {
                        __result = newFish;
                    }
                }
            }
        }

        /// <summary>
        /// Attempts to get a fish from the location by retrying with boosted luck
        /// </summary>
        private static Item? TryGetLocationFish(GameLocation location, float millisecondsAfterNibble,
            string bait, int waterDepth, Farmer who, double baitPotency, Vector2 bobberTile)
        {
            try
            {
                _isRetrying = true;

                // Save original luck
                double originalLuck = Game1.player.team.sharedDailyLuck.Value;
                int originalFishingLevel = who.FishingLevel;

                // Boost luck and fishing level significantly to avoid trash
                Game1.player.team.sharedDailyLuck.Value = 0.125; // Max daily luck

                // Try multiple times to get a non-trash fish
                for (int attempt = 0; attempt < 10; attempt++)
                {
                    var fish = location.getFish(millisecondsAfterNibble, bait, waterDepth + 5, who, baitPotency + 1.0, bobberTile);

                    if (fish != null)
                    {
                        string fishItemId = fish.QualifiedItemId ?? fish.ItemId ?? "";
                        bool fishIsTrash = TrashItemIds.Contains(fishItemId) ||
                                          TrashItemIds.Contains(fish.ItemId ?? "");

                        if (!fishIsTrash)
                        {
                            // Restore original values
                            Game1.player.team.sharedDailyLuck.Value = originalLuck;
                            _isRetrying = false;
                            return fish;
                        }
                    }
                }

                // Restore original values
                Game1.player.team.sharedDailyLuck.Value = originalLuck;
                _isRetrying = false;
                return null;
            }
            catch
            {
                _isRetrying = false;
                return null;
            }
        }
    }

    /// <summary>
    /// Harmony patches for mining modifications.
    /// Patches GameLocation.OnStoneDestroyed to add extra mineral drops.
    /// </summary>
    public static class MiningPatches
    {
        // Mineral item IDs that can drop from rocks
        private static readonly Dictionary<string, string[]> StoneToMineralMap = new()
        {
            // Iridium nodes
            { "765", new[] { "(O)386" } }, // Iridium Node -> Iridium Ore
            // Gold nodes
            { "764", new[] { "(O)384" } }, // Gold Node -> Gold Ore
            // Iron nodes
            { "290", new[] { "(O)380" } }, // Iron Node -> Iron Ore
            // Copper nodes
            { "751", new[] { "(O)378" } }, // Copper Node -> Copper Ore
            // Gem nodes
            { "2", new[] { "(O)72", "(O)60", "(O)62", "(O)64", "(O)66", "(O)68", "(O)70" } }, // Diamond, Emerald, Aquamarine, Ruby, Topaz, Jade, Fire Quartz
            { "4", new[] { "(O)72", "(O)60", "(O)62", "(O)64", "(O)66", "(O)68", "(O)70" } },
            { "6", new[] { "(O)72", "(O)60", "(O)62", "(O)64", "(O)66", "(O)68", "(O)70" } },
            // Mystic Stone
            { "46", new[] { "(O)386", "(O)74" } }, // Iridium Ore, Prismatic Shard
            // Radioactive nodes
            { "95", new[] { "(O)909", "(O)910" } }, // Radioactive Ore, Radioactive Bar
        };

        // Common stone IDs that drop regular stone/geodes
        private static readonly HashSet<string> CommonStoneIds = new()
        {
            "343", "450", "668", "670", "845", "846", "847" // Various stone types
        };

        public static void ApplyPatches(Harmony harmony)
        {
            // Patch OnStoneDestroyed to add extra mineral drops
            harmony.Patch(
                original: AccessTools.Method(typeof(GameLocation), nameof(GameLocation.OnStoneDestroyed)),
                postfix: new HarmonyMethod(typeof(MiningPatches), nameof(OnStoneDestroyed_Postfix))
            );
        }

        /// <summary>
        /// Postfix for OnStoneDestroyed - adds bonus mineral drops based on MineralDropBonus setting
        /// </summary>
        private static void OnStoneDestroyed_Postfix(GameLocation __instance, string stoneId, int x, int y, Farmer who)
        {
            int bonusDrops = ImGuiMenu.MineralDropBonus;
            if (bonusDrops <= 0) return;
            if (who == null || __instance == null) return;

            try
            {
                Vector2 tilePos = new Vector2(x, y);

                // Check if this stone type has specific minerals
                if (StoneToMineralMap.TryGetValue(stoneId, out string[]? minerals) && minerals != null)
                {
                    // Drop bonus minerals
                    for (int i = 0; i < bonusDrops; i++)
                    {
                        string mineralId = minerals[Game1.random.Next(minerals.Length)];
                        var item = ItemRegistry.Create(mineralId, allowNull: true);
                        if (item != null)
                        {
                            Game1.createItemDebris(item, tilePos * 64f, Game1.random.Next(4), __instance);
                        }
                    }
                }
                else if (CommonStoneIds.Contains(stoneId))
                {
                    // For common stones, drop stone or coal
                    for (int i = 0; i < bonusDrops; i++)
                    {
                        string dropId = Game1.random.NextDouble() < 0.1 ? "(O)382" : "(O)390"; // Coal or Stone
                        var item = ItemRegistry.Create(dropId, allowNull: true);
                        if (item != null)
                        {
                            Game1.createItemDebris(item, tilePos * 64f, Game1.random.Next(4), __instance);
                        }
                    }
                }
                else
                {
                    // For unknown stones, try to drop stone
                    for (int i = 0; i < bonusDrops; i++)
                    {
                        var item = ItemRegistry.Create("(O)390", allowNull: true); // Stone
                        if (item != null)
                        {
                            Game1.createItemDebris(item, tilePos * 64f, Game1.random.Next(4), __instance);
                        }
                    }
                }
            }
            catch
            {
                // Silently fail if something goes wrong
            }
        }
    }
}
