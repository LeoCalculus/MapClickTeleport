using HarmonyLib;
using StardewValley;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

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
                Game1.showingHealth = false;
                Game1.showingHealthBar = false;

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
            if (!HideStaminaBar)
                return false;

            // Must be in stamina bar area
            if (!IsInStaminaBarArea(destPos))
                return false;

            // And must be stamina sprite or staminaRect
            if (IsStaminaSprite(sourceRect))
                return true;

            if (texture == Game1.staminaRect)
                return true;

            return false;
        }

        private static bool ShouldBlockRect(Texture2D texture, Rectangle? sourceRect, Rectangle destRect)
        {
            if (!HideStaminaBar)
                return false;

            if (!IsInStaminaBarArea(destRect))
                return false;

            if (IsStaminaSprite(sourceRect))
                return true;

            if (texture == Game1.staminaRect)
                return true;

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
}
