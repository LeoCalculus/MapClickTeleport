using System;
using ImGuiNET;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using SVector2 = System.Numerics.Vector2;
using SVector4 = System.Numerics.Vector4;

namespace MapClickTeleport
{
    /// <summary>
    /// Manages floating ImGui windows that persist outside of the main menu
    /// Features animated health/stamina bars with percentage display
    /// </summary>
    public static class FloatingWindows
    {
        // Window visibility states (persist across menu open/close)
        public static bool ShowPlayerStats { get; set; } = false;
        public static bool ShowTimeWidget { get; set; } = false;
        public static bool ShowInventoryWidget { get; set; } = false;
        public static bool ShowFPSCounter { get; set; } = false;

        // Style settings - opacity affects ONLY background, not text/bars
        public static float WindowOpacity { get; set; } = 0.85f;
        public static float WindowRounding { get; set; } = 8f;
        public static float PanelBlur { get; set; } = 3.0f;

        // Animation settings
        private const float ANIM_SPEED = 5.0f;

        // Animated values for smooth interpolation (stored as percentages 0-1)
        private static float _displayHealth = -1f;
        private static float _displayStamina = -1f;
        private static float _displayMoney = -1f;

        // FPS tracking - use actual system time for accuracy
        private static DateTime _lastFPSUpdate = DateTime.Now;
        private static int _frameCount = 0;
        private static float _currentFPS = 60f;
        public static int TargetFPS { get; set; } = 60; // 0 = unlimited (vsync off)
        public static int CustomFPS { get; set; } = 144;
        private static bool _vsyncEnabled = true;
        
        // Target values (actual current)
        private static float _targetHealthPct = 1f;
        private static float _targetStaminaPct = 1f;

        /// <summary>
        /// Draw all enabled floating windows
        /// Call this from ModEntry after ImGui.NewFrame()
        /// </summary>
        public static void Draw()
        {
            if (!Context.IsWorldReady) return;

            float dt = ImGui.GetIO().DeltaTime;
            if (dt <= 0) dt = 0.016f; // fallback to ~60fps

            var player = Game1.player;
            
            // Calculate target percentages
            _targetHealthPct = (float)player.health / Math.Max(player.maxHealth, 1);
            _targetStaminaPct = player.Stamina / Math.Max(player.MaxStamina, 1f);

            // Initialize display values on first frame
            if (_displayHealth < 0f) _displayHealth = _targetHealthPct;
            if (_displayStamina < 0f) _displayStamina = _targetStaminaPct;
            if (_displayMoney < 0f) _displayMoney = player.Money;

            // Animate towards targets with smooth interpolation
            float healthDiff = Math.Abs(_targetHealthPct - _displayHealth);
            float staminaDiff = Math.Abs(_targetStaminaPct - _displayStamina);
            
            // Speed up animation when change is large
            float healthSpeed = ANIM_SPEED * (1f + healthDiff * 3f);
            float staminaSpeed = ANIM_SPEED * (1f + staminaDiff * 3f);
            
            // Lerp towards target
            _displayHealth = Lerp(_displayHealth, _targetHealthPct, healthSpeed * dt);
            _displayStamina = Lerp(_displayStamina, _targetStaminaPct, staminaSpeed * dt);
            _displayMoney = Lerp(_displayMoney, player.Money, ANIM_SPEED * dt);
            
            // Snap when very close
            if (Math.Abs(_targetHealthPct - _displayHealth) < 0.005f) _displayHealth = _targetHealthPct;
            if (Math.Abs(_targetStaminaPct - _displayStamina) < 0.005f) _displayStamina = _targetStaminaPct;
            if (Math.Abs(player.Money - _displayMoney) < 1f) _displayMoney = player.Money;

            // Update FPS counter using actual system time for accuracy
            _frameCount++;
            var now = DateTime.Now;
            double elapsedSeconds = (now - _lastFPSUpdate).TotalSeconds;
            if (elapsedSeconds >= 0.5) // Update FPS display twice per second
            {
                _currentFPS = (float)(_frameCount / elapsedSeconds);
                _frameCount = 0;
                _lastFPSUpdate = now;
            }

            if (ShowPlayerStats) DrawPlayerStats();
            if (ShowTimeWidget) DrawTimeWidget();
            if (ShowInventoryWidget) DrawInventoryWidget();
            if (ShowFPSCounter) DrawFPSCounter();
        }

        /// <summary>
        /// Apply FPS limit to the game
        /// Call this when TargetFPS changes
        /// </summary>
        public static void ApplyFPSLimit()
        {
            try
            {
                if (TargetFPS == 0) // Unlimited - disable vsync and fixed timestep for max FPS
                {
                    _vsyncEnabled = false;
                    Game1.game1.IsFixedTimeStep = false;
                    Game1.graphics.SynchronizeWithVerticalRetrace = false;
                    Game1.graphics.ApplyChanges();
                }
                else if (TargetFPS == 60) // Standard - enable vsync
                {
                    _vsyncEnabled = true;
                    Game1.game1.IsFixedTimeStep = true;
                    Game1.game1.TargetElapsedTime = TimeSpan.FromSeconds(1.0 / 60.0);
                    Game1.graphics.SynchronizeWithVerticalRetrace = true;
                    Game1.graphics.ApplyChanges();
                }
                else // Custom FPS (120, 144, etc.)
                {
                    _vsyncEnabled = false;
                    Game1.game1.IsFixedTimeStep = true;
                    // Set target elapsed time to achieve desired FPS
                    Game1.game1.TargetElapsedTime = TimeSpan.FromSeconds(1.0 / TargetFPS);
                    Game1.graphics.SynchronizeWithVerticalRetrace = false;
                    Game1.graphics.ApplyChanges();
                }
            }
            catch
            {
                // Fallback if graphics device not available
            }
        }
        
        private static float Lerp(float a, float b, float t)
        {
            t = Math.Clamp(t, 0f, 1f);
            return a + (b - a) * t;
        }

        private static void DrawPlayerStats()
        {
            ImGui.SetNextWindowPos(new SVector2(10, 10), ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowBgAlpha(0f);

            ImGuiWindowFlags flags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize |
                ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoScrollbar |
                ImGuiWindowFlags.NoBackground;

            bool show = ShowPlayerStats;
            if (ImGui.Begin("##StatusBars", ref show, flags))
            {
                var drawList = ImGui.GetWindowDrawList();
                var pos = ImGui.GetCursorScreenPos();
                var boxSize = new SVector2(260, 120);

                DrawModernPanel(drawList, pos, boxSize, "STATUS");

                var player = Game1.player;

                // Health bar
                var barPos = new SVector2(pos.X + 15, pos.Y + 38);
                var barSize = new SVector2(230, 22);

                uint healthFg = ImGui.ColorConvertFloat4ToU32(new SVector4(0.9f, 0.25f, 0.25f, 1f));
                uint healthBg = ImGui.ColorConvertFloat4ToU32(new SVector4(0.25f, 0.12f, 0.12f, WindowOpacity * 0.8f));
                DrawModernBar(drawList, barPos, barSize, _displayHealth, healthFg, healthBg);

                // Health text with percentage
                int healthPct = (int)(_displayHealth * 100);
                string healthText = $"HP {player.health}/{player.maxHealth} ({healthPct}%)";
                var textSize = ImGui.CalcTextSize(healthText);
                uint textColor = ImGui.ColorConvertFloat4ToU32(new SVector4(1, 1, 1, 1f));
                drawList.AddText(new SVector2(barPos.X + (barSize.X - textSize.X) / 2, barPos.Y + 2), textColor, healthText);

                // Stamina bar
                barPos.Y += 30;
                uint staminaFg = ImGui.ColorConvertFloat4ToU32(new SVector4(0.2f, 0.8f, 0.35f, 1f));
                uint staminaBg = ImGui.ColorConvertFloat4ToU32(new SVector4(0.12f, 0.25f, 0.14f, WindowOpacity * 0.8f));
                DrawModernBar(drawList, barPos, barSize, _displayStamina, staminaFg, staminaBg);

                // Stamina text with percentage
                int staminaPct = (int)(_displayStamina * 100);
                string staminaText = $"EP {(int)player.Stamina}/{(int)player.MaxStamina} ({staminaPct}%)";
                textSize = ImGui.CalcTextSize(staminaText);
                drawList.AddText(new SVector2(barPos.X + (barSize.X - textSize.X) / 2, barPos.Y + 2), textColor, staminaText);

                // Money with animated counting
                barPos.Y += 28;
                uint goldColor = ImGui.ColorConvertFloat4ToU32(new SVector4(1f, 0.84f, 0.31f, 1f));
                string moneyText = $"$ {(int)_displayMoney:N0} g";
                drawList.AddText(barPos, goldColor, moneyText);

                ImGui.Dummy(boxSize);
            }
            ImGui.End();
            ShowPlayerStats = show;
        }

        private static void DrawTimeWidget()
        {
            ImGui.SetNextWindowPos(new SVector2(Game1.graphics.GraphicsDevice.Viewport.Width - 220, 10), ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowBgAlpha(0f);

            ImGuiWindowFlags flags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize |
                ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoScrollbar |
                ImGuiWindowFlags.NoBackground;

            bool show = ShowTimeWidget;
            if (ImGui.Begin("##Calendar", ref show, flags))
            {
                var drawList = ImGui.GetWindowDrawList();
                var pos = ImGui.GetCursorScreenPos();
                var boxSize = new SVector2(200, 105);

                DrawModernPanel(drawList, pos, boxSize, "CALENDAR");

                uint textColor = ImGui.ColorConvertFloat4ToU32(new SVector4(1, 1, 1, 1f));
                uint accentColor = GetSeasonColor();

                string[] dayNames = { "Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat" };
                int dayOfWeek = Game1.dayOfMonth % 7;
                var textPos = new SVector2(pos.X + 15, pos.Y + 38);
                drawList.AddText(textPos, textColor, dayNames[dayOfWeek]);

                textPos.Y += 22;
                string season = Game1.currentSeason;
                season = char.ToUpper(season[0]) + season.Substring(1);
                string dateStr = $"{season} {Game1.dayOfMonth}, Year {Game1.year}";
                drawList.AddText(textPos, accentColor, dateStr);

                textPos.Y += 22;
                int hour = Game1.timeOfDay / 100;
                int minute = Game1.timeOfDay % 100;
                string ampm = (hour < 12 || hour >= 24) ? "AM" : "PM";
                int displayHour = hour % 12;
                if (displayHour == 0) displayHour = 12;
                string timeStr = $"{displayHour}:{minute:D2} {ampm}";
                drawList.AddText(textPos, textColor, timeStr);

                ImGui.Dummy(boxSize);
            }
            ImGui.End();
            ShowTimeWidget = show;
        }

        private static void DrawInventoryWidget()
        {
            ImGui.SetNextWindowPos(new SVector2(10, 145), ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowBgAlpha(0f);

            ImGuiWindowFlags flags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize |
                ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoScrollbar |
                ImGuiWindowFlags.NoBackground;

            bool show = ShowInventoryWidget;
            if (ImGui.Begin("##Hotbar", ref show, flags))
            {
                var drawList = ImGui.GetWindowDrawList();
                var pos = ImGui.GetCursorScreenPos();
                var boxSize = new SVector2(320, 75);

                DrawModernPanel(drawList, pos, boxSize, "HOTBAR");

                var items = Game1.player.Items;
                var slotPos = new SVector2(pos.X + 12, pos.Y + 35);
                float slotSize = 24f;
                float spacing = 1f;

                for (int i = 0; i < Math.Min(12, items.Count); i++)
                {
                    var item = items[i];
                    var slotStart = new SVector2(slotPos.X + i * (slotSize + spacing), slotPos.Y);
                    var slotEnd = new SVector2(slotStart.X + slotSize, slotStart.Y + slotSize);

                    bool isSelected = i == Game1.player.CurrentToolIndex;
                    uint slotBg = isSelected
                        ? ImGui.ColorConvertFloat4ToU32(new SVector4(0.3f, 0.5f, 0.8f, 0.9f))
                        : ImGui.ColorConvertFloat4ToU32(new SVector4(0.15f, 0.15f, 0.2f, WindowOpacity * 0.7f));
                    uint slotBorder = isSelected
                        ? ImGui.ColorConvertFloat4ToU32(new SVector4(0.5f, 0.7f, 1f, 1f))
                        : ImGui.ColorConvertFloat4ToU32(new SVector4(0.3f, 0.3f, 0.4f, WindowOpacity * 0.6f));

                    drawList.AddRectFilled(slotStart, slotEnd, slotBg, 3f);
                    drawList.AddRect(slotStart, slotEnd, slotBorder, 3f, ImDrawFlags.None, isSelected ? 2f : 1f);

                    if (item != null && item.Stack > 1)
                    {
                        string label = item.Stack.ToString();
                        uint stackColor = ImGui.ColorConvertFloat4ToU32(new SVector4(1, 1, 1, 1f));
                        var textSize = ImGui.CalcTextSize(label);
                        drawList.AddText(
                            new SVector2(slotEnd.X - textSize.X - 2, slotEnd.Y - textSize.Y - 1),
                            stackColor, label);
                    }
                }

                ImGui.Dummy(boxSize);

                var mousePos = ImGui.GetMousePos();
                for (int i = 0; i < Math.Min(12, items.Count); i++)
                {
                    var item = items[i];
                    if (item == null) continue;

                    var slotStart = new SVector2(slotPos.X + i * (slotSize + spacing), slotPos.Y);
                    var slotEnd = new SVector2(slotStart.X + slotSize, slotStart.Y + slotSize);

                    if (mousePos.X >= slotStart.X && mousePos.X <= slotEnd.X &&
                        mousePos.Y >= slotStart.Y && mousePos.Y <= slotEnd.Y)
                    {
                        ImGui.BeginTooltip();
                        ImGui.Text(item.Name ?? "Unknown");
                        ImGui.EndTooltip();
                    }
                }
            }
            ImGui.End();
            ShowInventoryWidget = show;
        }

        private static void DrawModernPanel(ImDrawListPtr drawList, SVector2 pos, SVector2 size, string title)
        {
            float rounding = WindowRounding;

            // Enhanced blur/glow effect with more layers for frosted glass appearance
            if (PanelBlur > 0f)
            {
                // Outer glow layers (creates soft edge blur)
                int blurLayers = (int)(PanelBlur * 3); // More layers based on blur strength
                blurLayers = Math.Clamp(blurLayers, 3, 15);

                for (int i = blurLayers; i >= 0; i--)
                {
                    float offset = i * (PanelBlur * 0.8f);
                    float layerProgress = (float)i / blurLayers;
                    float alpha = 0.12f * WindowOpacity * (1f - layerProgress) * (PanelBlur / 5f);

                    // Gradient from dark blue to transparent
                    uint blurColor = ImGui.ColorConvertFloat4ToU32(new SVector4(
                        0.02f + 0.03f * layerProgress,
                        0.02f + 0.03f * layerProgress,
                        0.05f + 0.05f * layerProgress,
                        alpha
                    ));

                    drawList.AddRectFilled(
                        new SVector2(pos.X - offset, pos.Y - offset),
                        new SVector2(pos.X + size.X + offset, pos.Y + size.Y + offset),
                        blurColor, rounding + offset * 0.3f
                    );
                }

                // Inner frosted layers for depth
                for (int i = 0; i < 3; i++)
                {
                    float inset = i * 1.5f;
                    float alpha = 0.05f * WindowOpacity * (PanelBlur / 5f);
                    uint frostColor = ImGui.ColorConvertFloat4ToU32(new SVector4(0.15f, 0.15f, 0.2f, alpha));
                    drawList.AddRectFilled(
                        new SVector2(pos.X + inset, pos.Y + inset),
                        new SVector2(pos.X + size.X - inset, pos.Y + size.Y - inset),
                        frostColor, rounding - inset * 0.2f
                    );
                }
            }

            // Main background
            uint bgColor = ImGui.ColorConvertFloat4ToU32(new SVector4(0.08f, 0.08f, 0.12f, WindowOpacity * 0.9f));
            drawList.AddRectFilled(pos, new SVector2(pos.X + size.X, pos.Y + size.Y), bgColor, rounding);

            // Top highlight gradient for glass effect
            uint innerTop = ImGui.ColorConvertFloat4ToU32(new SVector4(0.2f, 0.2f, 0.25f, WindowOpacity * 0.25f));
            drawList.AddRectFilledMultiColor(
                new SVector2(pos.X + 2, pos.Y + 2),
                new SVector2(pos.X + size.X - 2, pos.Y + size.Y * 0.35f),
                innerTop, innerTop, 0, 0
            );

            // Border with subtle glow
            uint borderColor = ImGui.ColorConvertFloat4ToU32(new SVector4(0.45f, 0.45f, 0.55f, WindowOpacity * 0.8f));
            drawList.AddRect(pos, new SVector2(pos.X + size.X, pos.Y + size.Y), borderColor, rounding, ImDrawFlags.None, 1.5f);

            // Title
            if (!string.IsNullOrEmpty(title))
            {
                var titleSize = ImGui.CalcTextSize(title);
                float titleX = pos.X + (size.X - titleSize.X) / 2;
                uint titleColor = ImGui.ColorConvertFloat4ToU32(new SVector4(0.85f, 0.85f, 0.92f, 1f));
                drawList.AddText(new SVector2(titleX, pos.Y + 8), titleColor, title);
            }
        }

        private static void DrawModernBar(ImDrawListPtr drawList, SVector2 pos, SVector2 size,
            float progress, uint fgColor, uint bgColor)
        {
            float rounding = size.Y * 0.4f;

            drawList.AddRectFilled(pos, new SVector2(pos.X + size.X, pos.Y + size.Y), bgColor, rounding);

            progress = Math.Clamp(progress, 0f, 1f);
            if (progress > 0.01f)
            {
                float fillWidth = size.X * progress;
                if (fillWidth > rounding * 2)
                {
                    drawList.AddRectFilled(pos, new SVector2(pos.X + fillWidth, pos.Y + size.Y), fgColor, rounding);

                    uint highlight = ImGui.ColorConvertFloat4ToU32(new SVector4(1, 1, 1, 0.2f));
                    drawList.AddRectFilled(
                        new SVector2(pos.X + 2, pos.Y + 2),
                        new SVector2(pos.X + fillWidth - 2, pos.Y + size.Y * 0.4f),
                        highlight, rounding - 2
                    );
                }
            }

            uint borderColor = ImGui.ColorConvertFloat4ToU32(new SVector4(0.5f, 0.5f, 0.6f, 0.5f));
            drawList.AddRect(pos, new SVector2(pos.X + size.X, pos.Y + size.Y), borderColor, rounding, ImDrawFlags.None, 1f);
        }

        private static uint GetSeasonColor()
        {
            return Game1.currentSeason switch
            {
                "spring" => ImGui.ColorConvertFloat4ToU32(new SVector4(0.47f, 0.85f, 0.39f, 1f)),
                "summer" => ImGui.ColorConvertFloat4ToU32(new SVector4(1f, 0.8f, 0.31f, 1f)),
                "fall" => ImGui.ColorConvertFloat4ToU32(new SVector4(0.9f, 0.55f, 0.24f, 1f)),
                "winter" => ImGui.ColorConvertFloat4ToU32(new SVector4(0.59f, 0.8f, 1f, 1f)),
                _ => ImGui.ColorConvertFloat4ToU32(new SVector4(1, 1, 1, 1f))
            };
        }

        private static void DrawFPSCounter()
        {
            ImGui.SetNextWindowPos(new SVector2(10, 200), ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowBgAlpha(WindowOpacity);

            ImGuiWindowFlags flags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize |
                ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoScrollbar;

            bool show = ShowFPSCounter;
            if (ImGui.Begin("FPS Counter##Floating", ref show, flags))
            {
                // Color based on FPS
                SVector4 fpsColor;
                if (_currentFPS >= 58) fpsColor = new SVector4(0.2f, 1f, 0.2f, 1f); // Green (good)
                else if (_currentFPS >= 30) fpsColor = new SVector4(1f, 0.8f, 0.2f, 1f); // Yellow (ok)
                else fpsColor = new SVector4(1f, 0.2f, 0.2f, 1f); // Red (bad)

                // Display FPS with accurate measurement
                ImGui.TextColored(fpsColor, $"{_currentFPS:F1} FPS");

                // Show VSync status instead of confusing "target"
                string vsyncText = _vsyncEnabled ? "VSync: ON" : "VSync: OFF";
                ImGui.TextColored(new SVector4(0.7f, 0.7f, 0.7f, 1f), vsyncText);

                ImGui.End();
            }

            ShowFPSCounter = show;
        }
    }
}
