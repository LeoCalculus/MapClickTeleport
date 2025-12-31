using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using ImGuiNET;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewValley;

namespace MapClickTeleport
{
    /// <summary>
    /// ImGui Renderer for MonoGame/XNA (Stardew Valley)
    /// Handles rendering ImGui draw data using MonoGame's SpriteBatch and custom shaders
    /// </summary>
    public class ImGuiRenderer : IDisposable
    {
        private GraphicsDevice _graphicsDevice;
        private BasicEffect? _effect;
        private RasterizerState? _rasterizerState;
        private Texture2D? _fontTexture;
        private readonly Dictionary<IntPtr, Texture2D> _textureMap = new();
        private int _textureId = 1;
        private int _scrollWheelValue;
        private readonly List<int> _keys = new();
        private bool _initialized = false;

        public ImGuiRenderer(GraphicsDevice graphicsDevice)
        {
            _graphicsDevice = graphicsDevice;
            
            var context = ImGui.CreateContext();
            ImGui.SetCurrentContext(context);
            
            Initialize();
        }

        private void Initialize()
        {
            var io = ImGui.GetIO();
            
            // Configure ImGui
            io.ConfigFlags |= ImGuiConfigFlags.NavEnableKeyboard;
            io.BackendFlags |= ImGuiBackendFlags.HasMouseCursors;
            
            // Setup key mappings
            SetupKeyMappings();
            
            // Build font atlas with high quality settings
            BuildFontAtlas();
            
            // Create rendering resources
            CreateGraphicsResources();
            
            _initialized = true;
        }

        private void SetupKeyMappings()
        {
            var io = ImGui.GetIO();
            
            _keys.Add((int)Keys.Tab);
            _keys.Add((int)Keys.Left);
            _keys.Add((int)Keys.Right);
            _keys.Add((int)Keys.Up);
            _keys.Add((int)Keys.Down);
            _keys.Add((int)Keys.PageUp);
            _keys.Add((int)Keys.PageDown);
            _keys.Add((int)Keys.Home);
            _keys.Add((int)Keys.End);
            _keys.Add((int)Keys.Delete);
            _keys.Add((int)Keys.Back);
            _keys.Add((int)Keys.Enter);
            _keys.Add((int)Keys.Escape);
            _keys.Add((int)Keys.Space);
            _keys.Add((int)Keys.A);
            _keys.Add((int)Keys.C);
            _keys.Add((int)Keys.V);
            _keys.Add((int)Keys.X);
            _keys.Add((int)Keys.Y);
            _keys.Add((int)Keys.Z);
        }

        private unsafe void BuildFontAtlas()
        {
            var io = ImGui.GetIO();
            io.Fonts.Clear();
            
            // Create font config with high quality settings (matching C++ reference)
            ImFontConfigPtr fontConfig = ImGuiNative.ImFontConfig_ImFontConfig();
            fontConfig.OversampleH = 4;  // 4x horizontal oversampling for crisp text
            fontConfig.OversampleV = 4;  // 4x vertical oversampling
            fontConfig.PixelSnapH = true; // Snap to pixel grid
            
            // Try loading system fonts in order of preference
            string[] fontPaths = new[]
            {
                @"C:\Windows\Fonts\segoeui.ttf",   // Segoe UI - Windows default
                @"C:\Windows\Fonts\arial.ttf",      // Arial - fallback
                @"C:\Windows\Fonts\calibri.ttf",    // Calibri - another option
            };
            
            bool fontLoaded = false;
            foreach (string fontPath in fontPaths)
            {
                if (File.Exists(fontPath))
                {
                    try
                    {
                        io.Fonts.AddFontFromFileTTF(fontPath, 20.0f, fontConfig, io.Fonts.GetGlyphRangesDefault());
                        fontLoaded = true;
                        break;
                    }
                    catch
                    {
                        // Try next font
                    }
                }
            }
            
            // Fallback to default font with high quality config
            if (!fontLoaded)
            {
                io.Fonts.AddFontDefault(fontConfig);
            }
            
            io.Fonts.Build();
            
            // Get font texture data
            io.Fonts.GetTexDataAsRGBA32(out IntPtr pixels, out int width, out int height, out int bytesPerPixel);
            
            // Create texture
            _fontTexture = new Texture2D(_graphicsDevice, width, height, false, SurfaceFormat.Color);
            
            // Copy pixel data
            byte[] data = new byte[width * height * bytesPerPixel];
            Marshal.Copy(pixels, data, 0, data.Length);
            _fontTexture.SetData(data);
            
            // Store texture ID
            IntPtr fontTextureId = BindTexture(_fontTexture);
            io.Fonts.SetTexID(fontTextureId);
            io.Fonts.ClearTexData();
            
            // Destroy font config
            ImGuiNative.ImFontConfig_destroy(fontConfig);
        }

        private void CreateGraphicsResources()
        {
            _effect = new BasicEffect(_graphicsDevice)
            {
                TextureEnabled = true,
                VertexColorEnabled = true,
                World = Matrix.Identity,
                View = Matrix.Identity
            };
            
            _rasterizerState = new RasterizerState
            {
                CullMode = CullMode.None,
                DepthBias = 0,
                FillMode = FillMode.Solid,
                MultiSampleAntiAlias = false,
                ScissorTestEnable = true,
                SlopeScaleDepthBias = 0
            };
        }

        public IntPtr BindTexture(Texture2D texture)
        {
            IntPtr id = new IntPtr(_textureId++);
            _textureMap[id] = texture;
            return id;
        }

        public void UnbindTexture(IntPtr textureId)
        {
            _textureMap.Remove(textureId);
        }

        public void BeginLayout(GameTime gameTime)
        {
            if (!_initialized) return;
            
            ImGui.SetCurrentContext(ImGui.GetCurrentContext());
            
            var io = ImGui.GetIO();
            
            // Update display size
            io.DisplaySize = new System.Numerics.Vector2(
                _graphicsDevice.PresentationParameters.BackBufferWidth,
                _graphicsDevice.PresentationParameters.BackBufferHeight
            );
            io.DisplayFramebufferScale = System.Numerics.Vector2.One;
            
            // Update delta time
            io.DeltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
            
            // Update input
            UpdateInput();
            
            ImGui.NewFrame();
        }

        private void UpdateInput()
        {
            var io = ImGui.GetIO();
            
            // Mouse
            var mouse = Mouse.GetState();
            io.MousePos = new System.Numerics.Vector2(mouse.X, mouse.Y);
            io.MouseDown[0] = mouse.LeftButton == ButtonState.Pressed;
            io.MouseDown[1] = mouse.RightButton == ButtonState.Pressed;
            io.MouseDown[2] = mouse.MiddleButton == ButtonState.Pressed;
            
            // Mouse wheel
            int scrollDelta = mouse.ScrollWheelValue - _scrollWheelValue;
            io.MouseWheel = scrollDelta > 0 ? 1 : scrollDelta < 0 ? -1 : 0;
            _scrollWheelValue = mouse.ScrollWheelValue;
            
            // Keyboard
            var keyboard = Keyboard.GetState();
            
            io.AddKeyEvent(ImGuiKey.Tab, keyboard.IsKeyDown(Keys.Tab));
            io.AddKeyEvent(ImGuiKey.LeftArrow, keyboard.IsKeyDown(Keys.Left));
            io.AddKeyEvent(ImGuiKey.RightArrow, keyboard.IsKeyDown(Keys.Right));
            io.AddKeyEvent(ImGuiKey.UpArrow, keyboard.IsKeyDown(Keys.Up));
            io.AddKeyEvent(ImGuiKey.DownArrow, keyboard.IsKeyDown(Keys.Down));
            io.AddKeyEvent(ImGuiKey.PageUp, keyboard.IsKeyDown(Keys.PageUp));
            io.AddKeyEvent(ImGuiKey.PageDown, keyboard.IsKeyDown(Keys.PageDown));
            io.AddKeyEvent(ImGuiKey.Home, keyboard.IsKeyDown(Keys.Home));
            io.AddKeyEvent(ImGuiKey.End, keyboard.IsKeyDown(Keys.End));
            io.AddKeyEvent(ImGuiKey.Delete, keyboard.IsKeyDown(Keys.Delete));
            io.AddKeyEvent(ImGuiKey.Backspace, keyboard.IsKeyDown(Keys.Back));
            io.AddKeyEvent(ImGuiKey.Enter, keyboard.IsKeyDown(Keys.Enter));
            io.AddKeyEvent(ImGuiKey.Escape, keyboard.IsKeyDown(Keys.Escape));
            io.AddKeyEvent(ImGuiKey.Space, keyboard.IsKeyDown(Keys.Space));
            io.AddKeyEvent(ImGuiKey.A, keyboard.IsKeyDown(Keys.A));
            io.AddKeyEvent(ImGuiKey.C, keyboard.IsKeyDown(Keys.C));
            io.AddKeyEvent(ImGuiKey.V, keyboard.IsKeyDown(Keys.V));
            io.AddKeyEvent(ImGuiKey.X, keyboard.IsKeyDown(Keys.X));
            io.AddKeyEvent(ImGuiKey.Y, keyboard.IsKeyDown(Keys.Y));
            io.AddKeyEvent(ImGuiKey.Z, keyboard.IsKeyDown(Keys.Z));
            
            // Modifiers
            io.AddKeyEvent(ImGuiKey.ModCtrl, keyboard.IsKeyDown(Keys.LeftControl) || keyboard.IsKeyDown(Keys.RightControl));
            io.AddKeyEvent(ImGuiKey.ModShift, keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift));
            io.AddKeyEvent(ImGuiKey.ModAlt, keyboard.IsKeyDown(Keys.LeftAlt) || keyboard.IsKeyDown(Keys.RightAlt));
        }

        public void AddInputCharacter(char c)
        {
            if (_initialized)
            {
                ImGui.GetIO().AddInputCharacter(c);
            }
        }

        public void EndLayout()
        {
            if (!_initialized) return;
            
            ImGui.Render();
            RenderDrawData(ImGui.GetDrawData());
        }

        private unsafe void RenderDrawData(ImDrawDataPtr drawData)
        {
            if (drawData.CmdListsCount == 0) return;
            
            // Setup viewport
            var viewport = _graphicsDevice.Viewport;
            
            // Update projection matrix
            _effect.Projection = Matrix.CreateOrthographicOffCenter(
                0, viewport.Width, viewport.Height, 0, -1f, 1f
            );
            
            // Save current state
            var oldBlendState = _graphicsDevice.BlendState;
            var oldDepthStencilState = _graphicsDevice.DepthStencilState;
            var oldRasterizerState = _graphicsDevice.RasterizerState;
            var oldSamplerState = _graphicsDevice.SamplerStates[0];
            
            // Set render states - use LinearClamp for smoother text rendering
            _graphicsDevice.BlendState = BlendState.NonPremultiplied;
            _graphicsDevice.DepthStencilState = DepthStencilState.None;
            _graphicsDevice.RasterizerState = _rasterizerState;
            _graphicsDevice.SamplerStates[0] = SamplerState.LinearClamp; // Linear filtering for smoother fonts
            
            // Render command lists
            drawData.ScaleClipRects(ImGui.GetIO().DisplayFramebufferScale);
            
            for (int n = 0; n < drawData.CmdListsCount; n++)
            {
                ImDrawListPtr cmdList = drawData.CmdLists[n];
                
                // Create vertex/index buffers
                var vtxBuffer = cmdList.VtxBuffer;
                var idxBuffer = cmdList.IdxBuffer;
                
                var vertices = new VertexPositionColorTexture[vtxBuffer.Size];
                for (int i = 0; i < vtxBuffer.Size; i++)
                {
                    var v = vtxBuffer[i];
                    vertices[i] = new VertexPositionColorTexture(
                        new Vector3(v.pos.X, v.pos.Y, 0),
                        new Color(v.col),
                        new Vector2(v.uv.X, v.uv.Y)
                    );
                }
                
                var indices = new short[idxBuffer.Size];
                for (int i = 0; i < idxBuffer.Size; i++)
                {
                    indices[i] = (short)idxBuffer[i];
                }
                
                // Process draw commands
                int idxOffset = 0;
                for (int cmdIdx = 0; cmdIdx < cmdList.CmdBuffer.Size; cmdIdx++)
                {
                    ImDrawCmdPtr cmd = cmdList.CmdBuffer[cmdIdx];
                    
                    if (cmd.UserCallback != IntPtr.Zero)
                    {
                        // User callback (not used in standard ImGui)
                    }
                    else
                    {
                        // Set texture
                        if (_textureMap.TryGetValue(cmd.TextureId, out var texture))
                        {
                            _effect.Texture = texture;
                        }
                        
                        // Set scissor rect
                        _graphicsDevice.ScissorRectangle = new Rectangle(
                            (int)cmd.ClipRect.X,
                            (int)cmd.ClipRect.Y,
                            (int)(cmd.ClipRect.Z - cmd.ClipRect.X),
                            (int)(cmd.ClipRect.W - cmd.ClipRect.Y)
                        );
                        
                        // Draw
                        foreach (var pass in _effect.CurrentTechnique.Passes)
                        {
                            pass.Apply();
                            _graphicsDevice.DrawUserIndexedPrimitives(
                                PrimitiveType.TriangleList,
                                vertices,
                                0,
                                vertices.Length,
                                indices,
                                idxOffset,
                                (int)cmd.ElemCount / 3
                            );
                        }
                    }
                    
                    idxOffset += (int)cmd.ElemCount;
                }
            }
            
            // Restore state
            _graphicsDevice.BlendState = oldBlendState;
            _graphicsDevice.DepthStencilState = oldDepthStencilState;
            _graphicsDevice.RasterizerState = oldRasterizerState;
            _graphicsDevice.SamplerStates[0] = oldSamplerState;
        }

        public bool WantCaptureMouse => _initialized && ImGui.GetIO().WantCaptureMouse;
        public bool WantCaptureKeyboard => _initialized && ImGui.GetIO().WantCaptureKeyboard;

        public void Dispose()
        {
            _fontTexture?.Dispose();
            _effect?.Dispose();
            _rasterizerState?.Dispose();
            
            foreach (var texture in _textureMap.Values)
            {
                if (texture != _fontTexture)
                    texture?.Dispose();
            }
            _textureMap.Clear();
            
            ImGui.DestroyContext();
        }
    }
}
