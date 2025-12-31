using System.Reflection;
using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Monsters;
using StardewValley.TerrainFeatures;
using StardewValley.Tools;

namespace MapClickTeleport
{
    /// <summary>
    /// Overpowered features for cheating/testing purposes.
    /// Uses Harmony patches for instant mine/cut/kill functionality.
    /// </summary>
    public static class OPFeatures
    {
        // Feature toggles
        public static bool InstantMineRock { get; set; } = false;
        public static bool InstantCutTree { get; set; } = false;
        public static bool InstantKillMonster { get; set; } = false;
        public static bool OneHitKill { get; set; } = false;
        public static bool InfiniteStamina { get; set; } = false;
        public static bool InfiniteHealth { get; set; } = false;
        public static bool NoClip { get; set; } = false;
        public static bool InstantCatch { get; set; } = false;
        public static bool AutoPickup { get; set; } = false;
        public static float AutoPickupRadius { get; set; } = 5f;
        public static bool PVPEnabled { get; set; } = false;
        public static int PVPDamage { get; set; } = 20;
        public static bool UseMonsterMethod { get; set; } = true; // Use invisible monster for real synced damage

        private static IMonitor? _monitor;
        private static int _lastSwingTick = 0;



        /// <summary>
        /// Apply all Harmony patches for OP features
        /// </summary>
        public static void ApplyPatches(Harmony harmony, IMonitor monitor)
        {
            _monitor = monitor;

            

            try
            {
                // Patch for instant rock mining (Object.performToolAction)
                harmony.Patch(
                    original: AccessTools.Method(typeof(StardewValley.Object), nameof(StardewValley.Object.performToolAction)),
                    prefix: new HarmonyMethod(typeof(OPFeatures), nameof(Object_PerformToolAction_Prefix))
                );

                // Patch for instant tree cutting (Tree.performToolAction)
                harmony.Patch(
                    original: AccessTools.Method(typeof(Tree), nameof(Tree.performToolAction)),
                    prefix: new HarmonyMethod(typeof(OPFeatures), nameof(Tree_PerformToolAction_Prefix))
                );

                // Patch for one-hit kill monsters (all Monster subclasses)
                // Some monsters override takeDamage, so we need to patch each type individually
                Type[] paramTypes = new[] { typeof(int), typeof(int), typeof(int), typeof(bool), typeof(double), typeof(Farmer) };
                var monsterTypes = typeof(Monster).Assembly
                    .GetTypes()
                    .Where(t => typeof(Monster).IsAssignableFrom(t) && !t.IsAbstract);

                foreach (var monsterType in monsterTypes)
                {
                    var method = monsterType.GetMethod(
                        "takeDamage",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly,
                        null,
                        paramTypes,
                        null
                    );

                    if (method != null)
                    {
                        harmony.Patch(
                            original: method,
                            prefix: new HarmonyMethod(typeof(OPFeatures), nameof(Monster_TakeDamage_Prefix))
                        );
                        // _monitor?.Log($"Patched takeDamage for: {monsterType.Name}", LogLevel.Debug);
                    }
                }

                // Also patch the base Monster class for any that don't override
                harmony.Patch(
                    original: AccessTools.Method(typeof(Monster), nameof(Monster.takeDamage), paramTypes),
                    prefix: new HarmonyMethod(typeof(OPFeatures), nameof(Monster_TakeDamage_Prefix))
                );
                // _monitor?.Log("Patched takeDamage for base Monster class", LogLevel.Debug);

                // Patch for infinite stamina (Farmer.Stamina setter)
                harmony.Patch(
                    original: AccessTools.PropertySetter(typeof(Farmer), nameof(Farmer.Stamina)),
                    prefix: new HarmonyMethod(typeof(OPFeatures), nameof(Farmer_Stamina_Prefix))
                );

                // Patch for no clip (Farmer.MovePosition or collision checks)
                harmony.Patch(
                    original: AccessTools.Method(typeof(Farmer), nameof(Farmer.MovePosition)),
                    prefix: new HarmonyMethod(typeof(OPFeatures), nameof(Farmer_MovePosition_Prefix)),
                    postfix: new HarmonyMethod(typeof(OPFeatures), nameof(Farmer_MovePosition_Postfix))
                );

                // Patch for instant fishing (BobberBar constructor or update)
                var bobberBarType = Type.GetType("StardewValley.Menus.BobberBar, Stardew Valley");
                if (bobberBarType != null)
                {
                    harmony.Patch(
                        original: AccessTools.Method(bobberBarType, "update"),
                        prefix: new HarmonyMethod(typeof(OPFeatures), nameof(BobberBar_Update_Prefix))
                    );
                }

                // Patch for resource clumps (stumps, boulders)
                harmony.Patch(
                    original: AccessTools.Method(typeof(ResourceClump), nameof(ResourceClump.performToolAction)),
                    prefix: new HarmonyMethod(typeof(OPFeatures), nameof(ResourceClump_PerformToolAction_Prefix))
                );

                _monitor?.Log("OP Features patches applied successfully!", LogLevel.Debug);
            }
            catch (Exception ex)
            {
                _monitor?.Log($"Error applying OP patches: {ex.Message}", LogLevel.Error);
            }
        }

        /// <summary>
        /// Update tick - handles per-frame OP features
        /// </summary>
        public static void UpdateTick()
        {
            if (!Context.IsWorldReady) return;

            // Infinite Health (also handled by BuffTab.IsInvincible, but this is explicit)
            if (InfiniteHealth && Game1.player.health < Game1.player.maxHealth)
            {
                Game1.player.health = Game1.player.maxHealth;
            }

            // Instant kill monsters on contact
            if (InstantKillMonster)
            {
                KillMonstersNearPlayer();
            }

            // Auto pickup items
            if (AutoPickup)
            {
                AutoPickupNearbyItems();
            }

            // PVP - Damage other farmers when attacking
            if (PVPEnabled)
            {
                DamageNearbyFarmers();
            }
        }

        #region Harmony Patches

        /// <summary>
        /// Prefix for Object.performToolAction - makes rocks break instantly
        /// </summary>
        private static bool Object_PerformToolAction_Prefix(StardewValley.Object __instance, Tool t)
        {
            if (!InstantMineRock) return true;
            if (t is not Pickaxe) return true;
            
            // Check if this is a stone/ore
            if (__instance.Name.Contains("Stone") || __instance.Name.Contains("Ore") || 
                __instance.Name.Contains("Node") || IsOreNode(__instance.ItemId))
            {
                // Force instant destruction by setting MinutesUntilReady to minimum
                // and letting the tool action complete with max power
                __instance.MinutesUntilReady = 0;
                
                // Set fragility to indicate it should break (2 = destroy on any hit)
                __instance.Fragility = 2;
                
                // Let the original method handle drops and removal
                return true;
            }
            
            return true;
        }

        /// <summary>
        /// Prefix for Tree.performToolAction - makes trees fall instantly
        /// </summary>
        private static bool Tree_PerformToolAction_Prefix(Tree __instance, Tool t, int explosion, Vector2 tileLocation)
        {
            if (!InstantCutTree) return true;
            if (t is not Axe) return true;

            // Set tree health to 0 to make it fall on this hit
            __instance.health.Value = 0;
            
            return true; // Let original method handle the rest
        }

        /// <summary>
        /// Prefix for ResourceClump.performToolAction - makes stumps/boulders break instantly
        /// </summary>
        private static bool ResourceClump_PerformToolAction_Prefix(ResourceClump __instance, Tool t, int damage, Vector2 tileLocation)
        {
            if (!InstantCutTree && !InstantMineRock) return true;

            // Large stumps use axe
            if (t is Axe && InstantCutTree)
            {
                if (__instance.parentSheetIndex.Value == 600 || __instance.parentSheetIndex.Value == 602)
                {
                    __instance.health.Value = 0;
                }
            }
            
            // Boulders use pickaxe
            if (t is Pickaxe && InstantMineRock)
            {
                __instance.health.Value = 0;
            }

            return true;
        }

        /// <summary>
        /// Prefix for Monster.takeDamage - makes monsters die in one hit
        /// </summary>
        private static bool Monster_TakeDamage_Prefix(
            Monster __instance,      // The monster being hit
            ref int damage,          // ref lets us modify the damage
            ref int __result,        // ref lets us set return value if we skip original
            Farmer who)
        {
            // check function entrance:
            // Game1.addHUDMessage(new HUDMessage("Instant kill here triggered!", HUDMessage.newQuest_type));
            if (!OneHitKill) return true;          // Run original normally
            if (who != Game1.player) return true;  // Run original normally
            
            __instance.Health = 0; // just set the health to 0-> ok
            return true;  // Run original with our modified damage
        }

        private static void Monster_TakeDamage_Prefix2(Monster __instance, ref int damage, ref double addedPrecision)
        {
            if (!OneHitKill) return;
            
            damage = __instance.Health + __instance.resilience.Value + 100;
            addedPrecision = 1.0; // Guarantees no miss
        }

        /// <summary>
        /// Prefix for Farmer.Stamina setter - prevents stamina decrease
        /// </summary>
        private static bool Farmer_Stamina_Prefix(Farmer __instance, ref float value)
        {
            if (!InfiniteStamina) return true;
            if (__instance != Game1.player) return true;

            // Only prevent decreases, allow increases
            if (value < __instance.Stamina)
            {
                return false; // Skip the setter, keeping stamina unchanged
            }
            
            return true;
        }

        // Store original passability state for no-clip
        private static bool _originalIgnoreCollisions = false;

        /// <summary>
        /// Prefix for Farmer.MovePosition - enables no-clip
        /// </summary>
        private static void Farmer_MovePosition_Prefix(Farmer __instance, GameTime time, xTile.Dimensions.Rectangle viewport, GameLocation currentLocation)
        {
            if (!NoClip) return;
            if (__instance != Game1.player) return;

            _originalIgnoreCollisions = __instance.ignoreCollisions;
            __instance.ignoreCollisions = true;
        }

        /// <summary>
        /// Postfix for Farmer.MovePosition - restores collision state
        /// </summary>
        private static void Farmer_MovePosition_Postfix(Farmer __instance)
        {
            if (!NoClip) return;
            if (__instance != Game1.player) return;

            // Keep ignoreCollisions true while no-clip is enabled
            // (don't restore, keep it enabled)
        }

        /// <summary>
        /// Prefix for BobberBar.update - makes fish catch instantly
        /// </summary>
        private static bool BobberBar_Update_Prefix(object __instance, GameTime time)
        {
            if (!InstantCatch) return true;

            try
            {
                // Use reflection to set distanceFromCatching to 1 (caught)
                var distanceField = AccessTools.Field(__instance.GetType(), "distanceFromCatching");
                if (distanceField != null)
                {
                    distanceField.SetValue(__instance, 1f);
                }
            }
            catch
            {
                // Ignore errors, let original method run
            }

            return true;
        }

        #endregion

        #region Helper Methods

        private static bool IsOreNode(string itemId)
        {
            string[] oreIds = {
                "751", "290", "764", "765", "95", "843", "844", "845", "846", "847",
                "668", "670", "760", "762", "32", "34", "36", "38", "40", "42",
                "44", "46", "48", "50", "52", "54", "56", "58",
                "2", "4", "6", "8", "10", "12", "14",
                "75", "76", "77"
            };
            return oreIds.Contains(itemId);
        }

        private static void KillMonstersNearPlayer()
        {
            if (Game1.currentLocation == null) return;

            var playerPos = Game1.player.Position;
            float killRadius = 128f; // About 2 tiles (increased for better collision detection)

            foreach (var character in Game1.currentLocation.characters.ToList())
            {
                if (character is Monster monster && monster.Health > 0)
                {
                    float dist = Vector2.Distance(playerPos, monster.Position);
                    if (dist < killRadius)
                    {
                        monster.Health = 0;
                        monster.deathAnimation();
                        Game1.currentLocation.characters.Remove(monster);
                        
                        // Drop loot
                        var drops = monster.objectsToDrop;
                        foreach (var dropId in drops)
                        {
                            Game1.createItemDebris(
                                ItemRegistry.Create(dropId),
                                monster.Position,
                                Game1.random.Next(4),
                                Game1.currentLocation
                            );
                        }
                    }
                }
            }
        }

        private static void AutoPickupNearbyItems()
        {
            if (Game1.currentLocation == null) return;

            var playerPos = Game1.player.Position;
            float pickupRadius = AutoPickupRadius * 64f; // Convert tiles to pixels

            // Check debris (dropped items)
            foreach (var debris in Game1.currentLocation.debris.ToList())
            {
                if (debris.item != null)
                {
                    var debrisChunks = debris.Chunks;
                    if (debrisChunks.Count > 0)
                    {
                        var chunk = debrisChunks[0];
                        float dist = Vector2.Distance(playerPos, chunk.position.Value);

                        if (dist < pickupRadius && dist > 64f) // Don't interfere with normal pickup
                        {
                            // Move debris towards player
                            Vector2 dir = playerPos - chunk.position.Value;
                            dir.Normalize();
                            chunk.position.Value += dir * 20f;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// PVP: Damages other farmers when the player swings their weapon.
        /// Uses invisible monster method for REAL synced damage in multiplayer!
        /// </summary>
        private static void DamageNearbyFarmers()
        {
            if (!Context.IsMultiplayer || Game1.currentLocation == null) return;

            // Check if player is using a weapon (swinging)
            if (Game1.player.UsingTool && Game1.player.CurrentTool is MeleeWeapon weapon)
            {
                // Prevent spamming damage on every tick during swing
                int currentTick = Game1.ticks;
                if (currentTick - _lastSwingTick < 20) // Cooldown of ~0.33 seconds
                    return;

                _lastSwingTick = currentTick;

                var playerPos = Game1.player.Position;
                var facingDirection = Game1.player.FacingDirection;

                // Calculate attack range based on weapon (typically 64-128 pixels)
                float attackRange = 128f;

                // Get the direction the player is facing as a vector
                Vector2 attackDir = facingDirection switch
                {
                    0 => new Vector2(0, -1),  // North
                    1 => new Vector2(1, 0),   // East
                    2 => new Vector2(0, 1),   // South
                    3 => new Vector2(-1, 0),  // West
                    _ => Vector2.Zero
                };

                // Check all farmers in the location (including host and other clients)
                foreach (var farmer in Game1.currentLocation.farmers)
                {
                    // Don't damage ourselves
                    if (farmer == Game1.player)
                        continue;

                    // Check if farmer is in range
                    float dist = Vector2.Distance(playerPos, farmer.Position);
                    if (dist > attackRange)
                        continue;

                    // Check if farmer is roughly in front of us
                    Vector2 toFarmer = farmer.Position - playerPos;
                    toFarmer.Normalize();
                    float dotProduct = Vector2.Dot(attackDir, toFarmer);

                    // Only hit if farmer is in front (dot product > 0.3 means roughly 70 degree cone)
                    if (dotProduct > 0.3f)
                    {
                        // Calculate damage based on weapon stats + configured PVP damage
                        int damage = weapon.minDamage.Value + PVPDamage;

                        if (UseMonsterMethod)
                        {
                            // INVISIBLE MONSTER METHOD - REAL SYNCED DAMAGE!
                            SpawnInvisibleAttacker(farmer, damage);
                        }
                        else
                        {
                            // Direct damage method (client-side only)
                            farmer.takeDamage(damage, false, null);

                            // Visual/audio feedback
                            Game1.playSound("hitEnemy");

                            // Show damage number
                            Game1.currentLocation.debris.Add(new Debris(
                                damage,
                                new Vector2(farmer.Position.X, farmer.Position.Y - 32),
                                Color.Red,
                                1f,
                                farmer
                            ));
                        }

                        _monitor?.Log($"[PVP] Hit {farmer.Name} for {damage} damage (method: {(UseMonsterMethod ? "monster" : "direct")})", LogLevel.Debug);
                    }
                }
            }
        }

        /// <summary>
        /// Spawns an invisible monster that attacks the target farmer and immediately dies.
        /// This method should sync damage in multiplayer because monster attacks are server-authoritative!
        /// </summary>
        private static void SpawnInvisibleAttacker(Farmer target, int damage)
        {
            try
            {
                // Create a Green Slime (weak, common monster)
                var monster = new GreenSlime(target.Position, 0); // 0 = easy difficulty

                // Make it invisible and passive
                monster.Scale = 0.001f; // Nearly invisible (can't be 0 or damage might not work)
                monster.isGlider.Value = true; // Flies, doesn't collide with terrain
                monster.speed = 0; // Can't move
                monster.moveTowardPlayerThreshold.Value = -1; // NEVER move toward players

                // CRITICAL: Disable ALL attack behaviors
                monster.isHardModeMonster.Value = false;
                monster.focusedOnFarmers = false; // Don't auto-target
                monster.DamageToFarmer = 0; // Can't hurt anyone on collision

                // Make the spawner (you) immune to this specific monster
                // by marking it as not aggressive
                monster.wildernessFarmMonster = false;

                // Set extremely high health so it survives any attacks
                monster.Health = 999999;
                monster.MaxHealth = 999999;
                monster.resilience.Value = 100; // Reduced from 999 - too high might cause issues

                // Position at target's location (required for damage to sync!)
                monster.Position = target.Position;

                // Add to location (this might sync to host!)
                Game1.currentLocation.characters.Add(monster);

                _monitor?.Log($"[PVP] Spawned passive invisible slime at {target.Name}'s position", LogLevel.Debug);

                // Schedule attack and cleanup
                // Phase 1: Trigger collision/attack (10ms delay - very short!)
                Game1.delayedActions.Add(new DelayedAction(10, () =>
                {
                    try
                    {
                        if (monster != null && Game1.currentLocation.characters.Contains(monster))
                        {
                            // ONLY damage the target farmer, never anyone else!
                            if (target != null && target != Game1.player)
                            {
                                // Deal damage with the monster as the source
                                // Having the monster as the damage source should sync in multiplayer!
                                target.takeDamage(damage, overrideParry: false, damager: monster);

                                // Play hit sound for feedback
                                Game1.playSound("hitEnemy");

                                _monitor?.Log($"[PVP] Monster dealt {damage} damage to {target.Name}", LogLevel.Debug);
                            }

                            // Phase 2: Remove monster IMMEDIATELY after attack (30ms later)
                            Game1.delayedActions.Add(new DelayedAction(30, () =>
                            {
                                try
                                {
                                    if (monster != null && Game1.currentLocation.characters.Contains(monster))
                                    {
                                        // Remove without death animation to stay invisible
                                        Game1.currentLocation.characters.Remove(monster);
                                        _monitor?.Log($"[PVP] Removed invisible attacker", LogLevel.Debug);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    _monitor?.Log($"[PVP] Error removing monster: {ex.Message}", LogLevel.Warn);
                                }
                            }));
                        }
                    }
                    catch (Exception ex)
                    {
                        _monitor?.Log($"[PVP] Error in monster attack: {ex.Message}", LogLevel.Warn);
                        // Try to clean up anyway
                        try
                        {
                            if (monster != null) Game1.currentLocation.characters.Remove(monster);
                        }
                        catch { }
                    }
                }));
            }
            catch (Exception ex)
            {
                _monitor?.Log($"[PVP] Failed to spawn invisible attacker: {ex.Message}", LogLevel.Error);
            }
        }

        #endregion
    }
}

