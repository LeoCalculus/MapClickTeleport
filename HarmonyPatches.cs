using HarmonyLib;
using StardewValley;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Text;

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
}
