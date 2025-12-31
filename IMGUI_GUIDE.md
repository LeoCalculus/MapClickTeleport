# ImGui Custom UI Guide for Stardew Valley

This guide explains how to create custom draggable ImGui windows in Stardew Valley using this mod's framework.

## Table of Contents
1. [Basic Window Creation](#basic-window-creation)
2. [Accessing Game Data](#accessing-game-data)
3. [Common ImGui Widgets](#common-imgui-widgets)
4. [Styling Your Windows](#styling-your-windows)
5. [Complete Examples](#complete-examples)

---

## Basic Window Creation

### Simple Window
```csharp
// In DrawCustomWindows() method of ImGuiMenu.cs

// Set initial position (only applies first time)
ImGui.SetNextWindowPos(new SVector2(100, 100), ImGuiCond.FirstUseEver);
ImGui.SetNextWindowSize(new SVector2(200, 150), ImGuiCond.FirstUseEver);

// Create window - the bool ref allows closing with X button
bool showWindow = true;
if (ImGui.Begin("My Window", ref showWindow, ImGuiWindowFlags.NoCollapse))
{
    ImGui.Text("Hello World!");
}
ImGui.End();
```

### Window Flags
```csharp
ImGuiWindowFlags.None              // Default - draggable, resizable
ImGuiWindowFlags.NoCollapse        // Remove collapse button
ImGuiWindowFlags.NoResize          // Fixed size
ImGuiWindowFlags.NoMove            // Fixed position
ImGuiWindowFlags.NoTitleBar        // No title bar
ImGuiWindowFlags.AlwaysAutoResize  // Auto-fit content
ImGuiWindowFlags.NoBackground      // Transparent background
```

---

## Accessing Game Data

### Player Stats
```csharp
// Health
int currentHealth = Game1.player.health;
int maxHealth = Game1.player.maxHealth;
float healthPercent = (float)currentHealth / maxHealth;

// Stamina
float currentStamina = Game1.player.Stamina;
float maxStamina = Game1.player.MaxStamina;

// Money
int gold = Game1.player.Money;

// Position
float tileX = Game1.player.Tile.X;
float tileY = Game1.player.Tile.Y;
string location = Game1.currentLocation.Name;

// Experience & Skills
int farmingLevel = Game1.player.FarmingLevel;
int miningLevel = Game1.player.MiningLevel;
int foragingLevel = Game1.player.ForagingLevel;
int fishingLevel = Game1.player.FishingLevel;
int combatLevel = Game1.player.CombatLevel;
```

### Time & Date
```csharp
int timeOfDay = Game1.timeOfDay;       // e.g., 1430 = 2:30 PM
string season = Game1.currentSeason;    // "spring", "summer", etc.
int day = Game1.dayOfMonth;
int year = Game1.year;

// Format time nicely
int hour = timeOfDay / 100;
int minute = timeOfDay % 100;
string ampm = hour >= 12 ? "PM" : "AM";
if (hour > 12) hour -= 12;
if (hour == 0) hour = 12;
string formattedTime = $"{hour}:{minute:D2} {ampm}";
```

### Inventory
```csharp
var inventory = Game1.player.Items;

foreach (var item in inventory)
{
    if (item != null)
    {
        string name = item.Name;
        int stack = item.Stack;
        int sellPrice = item.sellToStorePrice();
    }
}
```

### NPCs & Relationships
```csharp
// Get all villagers
var npcs = Utility.getAllCharacters()
    .Where(n => n.IsVillager);

foreach (var npc in npcs)
{
    string name = npc.Name;
    string location = npc.currentLocation?.Name ?? "Unknown";
    
    // Friendship
    if (Game1.player.friendshipData.TryGetValue(name, out var friendship))
    {
        int hearts = friendship.Points / 250;
    }
}
```

---

## Common ImGui Widgets

### Text & Labels
```csharp
ImGui.Text("Plain text");
ImGui.TextColored(new SVector4(1, 0, 0, 1), "Red text");  // RGBA
ImGui.TextWrapped("Long text that wraps automatically...");
ImGui.LabelText("Label", "Value");
```

### Buttons
```csharp
if (ImGui.Button("Click Me"))
{
    // Button was clicked
    Game1.playSound("coin");
}

// Button with size
if (ImGui.Button("Big Button", new SVector2(200, 50)))
{
    // Clicked
}
```

### Checkboxes & Toggles
```csharp
bool myToggle = false;
if (ImGui.Checkbox("Enable Feature", ref myToggle))
{
    // Value changed
}
```

### Input Fields
```csharp
string inputText = "";
ImGui.InputText("Name", ref inputText, 100);

int inputInt = 0;
ImGui.InputInt("Amount", ref inputInt);

float inputFloat = 0f;
ImGui.InputFloat("Value", ref inputFloat);
```

### Sliders
```csharp
int intValue = 50;
ImGui.SliderInt("Volume", ref intValue, 0, 100);

float floatValue = 0.5f;
ImGui.SliderFloat("Opacity", ref floatValue, 0f, 1f);
```

### Progress Bars
```csharp
float progress = 0.75f;  // 75%
ImGui.ProgressBar(progress, new SVector2(-1, 0), "75%");
// -1 width = fill available space
```

### Combo Box (Dropdown)
```csharp
string[] options = { "Option 1", "Option 2", "Option 3" };
int selectedIndex = 0;
ImGui.Combo("Select", ref selectedIndex, options, options.Length);
```

### Child Windows (Scrollable Areas)
```csharp
if (ImGui.BeginChild("ScrollArea", new SVector2(0, 200), true))
{
    // Content here is scrollable
    for (int i = 0; i < 50; i++)
    {
        ImGui.Text($"Item {i}");
    }
}
ImGui.EndChild();
```

### Columns
```csharp
ImGui.Columns(2, "MyColumns", false);

ImGui.Text("Left column");
ImGui.NextColumn();
ImGui.Text("Right column");

ImGui.Columns(1);  // Reset to single column
```

### Tooltips
```csharp
ImGui.Button("Hover me");
if (ImGui.IsItemHovered())
{
    ImGui.SetTooltip("This is a tooltip!");
}
```

---

## Styling Your Windows

### Global Style Settings
```csharp
var style = ImGui.GetStyle();

// Rounding
style.WindowRounding = 8f;   // Window corners
style.FrameRounding = 4f;    // Button/input corners
style.GrabRounding = 4f;     // Slider grab
style.TabRounding = 4f;      // Tab corners
style.ScrollbarRounding = 4f;

// Padding & Spacing
style.WindowPadding = new SVector2(15, 15);
style.FramePadding = new SVector2(8, 4);
style.ItemSpacing = new SVector2(10, 8);
```

### Colors
```csharp
var colors = ImGui.GetStyle().Colors;

// Window background
colors[(int)ImGuiCol.WindowBg] = new SVector4(0.1f, 0.1f, 0.12f, 0.95f);

// Title bar
colors[(int)ImGuiCol.TitleBg] = new SVector4(0.08f, 0.08f, 0.1f, 1f);
colors[(int)ImGuiCol.TitleBgActive] = new SVector4(0.12f, 0.12f, 0.15f, 1f);

// Buttons
colors[(int)ImGuiCol.Button] = new SVector4(0.2f, 0.4f, 0.7f, 1f);
colors[(int)ImGuiCol.ButtonHovered] = new SVector4(0.3f, 0.5f, 0.8f, 1f);
colors[(int)ImGuiCol.ButtonActive] = new SVector4(0.15f, 0.3f, 0.6f, 1f);

// Input fields
colors[(int)ImGuiCol.FrameBg] = new SVector4(0.15f, 0.15f, 0.18f, 1f);
colors[(int)ImGuiCol.FrameBgHovered] = new SVector4(0.2f, 0.2f, 0.25f, 1f);

// Tabs
colors[(int)ImGuiCol.Tab] = new SVector4(0.15f, 0.15f, 0.18f, 1f);
colors[(int)ImGuiCol.TabHovered] = new SVector4(0.25f, 0.4f, 0.6f, 1f);
colors[(int)ImGuiCol.TabActive] = new SVector4(0.3f, 0.5f, 0.8f, 1f);
```

### Color Values (SVector4)
```csharp
// SVector4(Red, Green, Blue, Alpha) - values 0.0 to 1.0

new SVector4(1, 0, 0, 1)      // Red
new SVector4(0, 1, 0, 1)      // Green
new SVector4(0, 0, 1, 1)      // Blue
new SVector4(1, 1, 0, 1)      // Yellow
new SVector4(1, 0.5f, 0, 1)   // Orange
new SVector4(0.5f, 0, 1, 1)   // Purple
new SVector4(1, 1, 1, 1)      // White
new SVector4(0, 0, 0, 1)      // Black
new SVector4(0.5f, 0.5f, 0.5f, 1)  // Gray
```

---

## Complete Examples

### Health/Stamina HUD Window
```csharp
private bool _showHUD = true;

private void DrawHUDWindow()
{
    if (!_showHUD) return;
    
    ImGui.SetNextWindowPos(new SVector2(10, 10), ImGuiCond.FirstUseEver);
    ImGui.SetNextWindowSize(new SVector2(180, 100), ImGuiCond.FirstUseEver);
    
    if (ImGui.Begin("HUD", ref _showHUD, 
        ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoResize))
    {
        // Health
        float hp = (float)Game1.player.health / Game1.player.maxHealth;
        ImGui.PushStyleColor(ImGuiCol.PlotHistogram, new SVector4(0.8f, 0.2f, 0.2f, 1));
        ImGui.ProgressBar(hp, new SVector2(-1, 15), $"{Game1.player.health}");
        ImGui.PopStyleColor();
        
        // Stamina
        float stamina = Game1.player.Stamina / Game1.player.MaxStamina;
        ImGui.PushStyleColor(ImGuiCol.PlotHistogram, new SVector4(0.2f, 0.7f, 0.2f, 1));
        ImGui.ProgressBar(stamina, new SVector2(-1, 15), $"{(int)Game1.player.Stamina}");
        ImGui.PopStyleColor();
        
        // Gold
        ImGui.TextColored(new SVector4(1, 0.85f, 0, 1), $"${Game1.player.Money:N0}");
    }
    ImGui.End();
}
```

### Quick Warp Window
```csharp
private bool _showWarp = true;
private string[] _locations = { "Farm", "Town", "Beach", "Mountain", "Forest" };

private void DrawWarpWindow()
{
    if (!_showWarp) return;
    
    ImGui.SetNextWindowPos(new SVector2(200, 10), ImGuiCond.FirstUseEver);
    
    if (ImGui.Begin("Quick Warp", ref _showWarp, ImGuiWindowFlags.AlwaysAutoResize))
    {
        foreach (var loc in _locations)
        {
            if (ImGui.Button(loc, new SVector2(100, 25)))
            {
                Game1.warpFarmer(loc, 50, 50, false);
                Game1.playSound("wand");
            }
        }
    }
    ImGui.End();
}
```

### Skill Tracker Window
```csharp
private bool _showSkills = true;

private void DrawSkillsWindow()
{
    if (!_showSkills) return;
    
    ImGui.SetNextWindowPos(new SVector2(10, 150), ImGuiCond.FirstUseEver);
    
    if (ImGui.Begin("Skills", ref _showSkills, ImGuiWindowFlags.AlwaysAutoResize))
    {
        var player = Game1.player;
        
        ImGui.Columns(2, "SkillCols", false);
        
        ImGui.Text("Farming");
        ImGui.NextColumn();
        ImGui.Text($"Lv {player.FarmingLevel}");
        ImGui.NextColumn();
        
        ImGui.Text("Mining");
        ImGui.NextColumn();
        ImGui.Text($"Lv {player.MiningLevel}");
        ImGui.NextColumn();
        
        ImGui.Text("Foraging");
        ImGui.NextColumn();
        ImGui.Text($"Lv {player.ForagingLevel}");
        ImGui.NextColumn();
        
        ImGui.Text("Fishing");
        ImGui.NextColumn();
        ImGui.Text($"Lv {player.FishingLevel}");
        ImGui.NextColumn();
        
        ImGui.Text("Combat");
        ImGui.NextColumn();
        ImGui.Text($"Lv {player.CombatLevel}");
        
        ImGui.Columns(1);
    }
    ImGui.End();
}
```

---

## Tips & Best Practices

1. **Use `ImGuiCond.FirstUseEver`** for initial window positions so they're only set once
2. **Always call `ImGui.End()`** after `ImGui.Begin()`
3. **Use `ImGui.PushID()`/`ImGui.PopID()`** when creating multiple widgets in a loop
4. **Use `ref` variables** for input widgets that need to modify state
5. **Play sounds** on user interactions for better UX:
   - `Game1.playSound("coin")` - Success
   - `Game1.playSound("cancel")` - Cancel/Off
   - `Game1.playSound("smallSelect")` - Select

## Adding New Windows to the Mod

1. Add a boolean toggle in the class fields: `private bool _showMyWindow = false;`
2. Add a checkbox in `DrawUITab()` to toggle it
3. Create a draw method: `private void DrawMyWindow() { ... }`
4. Call your draw method from `DrawCustomWindows()`

That's it! Your window will be draggable, resizable, and closeable by default.

Compile and Run:  
cd D:\Code\c#\stardew-valley-utilities\MapClickTeleport && dotnet build -c Release /p:GamePath="E:\SteamLibrary\steamapps\common\Stardew Valley"