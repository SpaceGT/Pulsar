using ImGuiNET;
using SharpDX;
using SharpDX.D3DCompiler;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DirectInput;
using SharpDX.DXGI;
using SharpDX.Mathematics.Interop;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Windows.Forms;
using Buffer = SharpDX.Direct3D11.Buffer;
using Device = SharpDX.Direct3D11.Device;
using ImVec2 = System.Numerics.Vector2;
using ImDrawIdx = System.UInt16;

namespace Pulsar.Legacy.ImGuiBackends;

public static class ImGuiImpl
{
    const string VS_SOURCE = @"
cbuffer ProjectionMatrixBuffer : register(b0)
{
    float4x4 ProjectionMatrix;
};

struct VS_INPUT
{
    float2 pos : POSITION;
    float2 uv  : TEXCOORD0;
    float4 col : COLOR0;
};

struct PS_INPUT
{
    float4 pos : SV_POSITION;
    float4 col : COLOR0;
    float2 uv  : TEXCOORD0;
};

PS_INPUT main(VS_INPUT input)
{
    PS_INPUT output;
    output.pos = mul(ProjectionMatrix, float4(input.pos.xy, 0.f, 1.f));
    output.col = input.col;
    output.uv = input.uv;
    return output;
}
";

    const string PS_SOURCE = @"
struct PS_INPUT
{
    float4 pos : SV_POSITION;
    float4 col : COLOR0;
    float2 uv  : TEXCOORD0;
};

Texture2D FontTexture : register(t0);
sampler FontSampler : register(s0);

float4 main(PS_INPUT input) : SV_Target
{
    float4 out_col = input.col * FontTexture.Sample(FontSampler, input.uv);
    return out_col;
}
";

    public static bool Initialized => _initialized;

    static Form _gameWindow;
    static Device _device;
    static Texture2D _backbuffer;
    static ImVec2 _backbufferSize;
    static RenderTargetView _backbufferRtv;

    // input
    static DirectInput _input;
    static Mouse _mouse;
    static MouseState _mouseState;
    static Keyboard _keyboard;
    static KeyboardState _keyboardState;

    // device resources
    static Buffer VB;
    static Buffer IB;
    static int VBSize = 5000;
    static int IBSize = 10000;
    static VertexShader VertexShader;
    static InputLayout InputLayout;
    static Buffer VertexConstantBuffer;
    static PixelShader PixelShader;
    static SamplerState FontSampler;
    static ShaderResourceView FontTextureView;
    static RasterizerState RasterizerState;
    static BlendState BlendState;
    static DepthStencilState DepthStencilState;

    static bool _initialized = false;

    public static unsafe void Init(Form gameWindow, SwapChain swapChain)
    {
        if (_initialized)
        {
            throw new Exception("Already initialized!");
        }

        _initialized = true;

        _gameWindow = gameWindow;
        _device = swapChain.GetDevice<Device>();
        CreateBackbufferResources(swapChain);

        _input = new DirectInput();
        _mouse = new Mouse(_input);
        _mouseState = new MouseState();
        _keyboard = new Keyboard(_input);
        _keyboardState = new KeyboardState();

        ImGui.CreateContext();
        CreateDeviceResources();

        ImGuiIOPtr io = ImGui.GetIO();
        io.BackendFlags |= ImGuiBackendFlags.RendererHasVtxOffset;
        io.BackendFlags |= ImGuiBackendFlags.HasMouseCursors | ImGuiBackendFlags.HasSetMousePos;
        io.ConfigFlags |= ImGuiConfigFlags.NavEnableKeyboard | ImGuiConfigFlags.DockingEnable;

        ImGuiViewportPtr main_viewport = ImGui.GetMainViewport();
        main_viewport.PlatformHandle = _gameWindow.Handle;
        main_viewport.PlatformHandleRaw = _gameWindow.Handle;

        _gameWindow.MouseMove += OnMouseMove;
    }

    private static void OnMouseMove(object sender, MouseEventArgs e)
    {
        ImGuiIOPtr io = ImGui.GetIO();
        io.AddMouseSourceEvent(ImGuiMouseSource.Mouse);
        ImVec2 posScale = _backbufferSize / new ImVec2(_gameWindow.ClientSize.Width, _gameWindow.ClientSize.Height);
        io.AddMousePosEvent(e.X * posScale.X, e.Y * posScale.Y);
    }

    public static void NewFrame(float deltaSeconds)
    {
        if (!_initialized)
        {
            throw new Exception("Not initialized.");
        }

        ImGuiIOPtr io = ImGui.GetIO();
        var clientRect = new ImVec2(_gameWindow.ClientSize.Width, _gameWindow.ClientSize.Height);
        io.DisplaySize = clientRect;
        io.DisplayFramebufferScale = ImVec2.One;
        io.DeltaTime = deltaSeconds;

        ImGuiPlatformIOPtr pio = ImGui.GetPlatformIO();

        UpdateMouse();
        UpdateKeyboard();

        ImGui.NewFrame();
    }

    private static void UpdateMouse()
    {
        try
        {
            _mouse.Acquire();
            _mouse.GetCurrentState(ref _mouseState);
            _mouse.Poll();
        }
        catch (SharpDXException)
        {
            _mouseState.X = 0;
            _mouseState.Y = 0;
            _mouseState.Z = 0;
            Array.Clear(_mouseState.Buttons, 0, _mouseState.Buttons.Length);
        }

        ImGuiIOPtr io = ImGui.GetIO();
        io.AddMouseSourceEvent(ImGuiMouseSource.Mouse);
        io.AddMouseWheelEvent(0f, _mouseState.Z / 100f);
        io.AddMouseButtonEvent(0, _mouseState.Buttons[0]);
        io.AddMouseButtonEvent(1, _mouseState.Buttons[1]);
        io.AddMouseButtonEvent(2, _mouseState.Buttons[2]);
        io.AddMouseButtonEvent(3, _mouseState.Buttons[3]);
        io.AddMouseButtonEvent(4, _mouseState.Buttons[4]);
    }

    private static void UpdateKeyboard()
    {
        try
        {
            _keyboard.Acquire();
            _keyboard.GetCurrentState(ref _keyboardState);
            _keyboard.Poll();
        }
        catch (SharpDXException)
        {
            _keyboardState.PressedKeys.Clear();
        }

        ImGuiIOPtr io = ImGui.GetIO();
        foreach (Key key in _keyboardState.AllKeys)
        {
            io.AddKeyEvent(key.ToImGui(), _keyboardState.IsPressed(key));
        }
    }

    public static void Render()
    {
        if (!_initialized)
        {
            throw new Exception("Not initialized.");
        }

        ImGui.Render();

        _device.ImmediateContext.OutputMerger.SetRenderTargets(_backbufferRtv);
        RenderDrawData(ImGui.GetDrawData());
        _device.ImmediateContext.ClearState();
    }

    private static unsafe void RenderDrawData(ImDrawDataPtr draw_data)
    {
        // Avoid rendering when minimized
        if (draw_data.DisplaySize.X <= 0.0f || draw_data.DisplaySize.Y <= 0.0f)
            return;

        if (draw_data.CmdListsCount == 0)
            return;

        DeviceContext device = _device.ImmediateContext;

        // Create and grow vertex/index buffers if needed
        if (VB == null || VBSize < draw_data.TotalVtxCount)
        {
            Destroy(ref VB);
            VBSize = draw_data.TotalVtxCount + 5000;
            BufferDescription desc = default;
            desc.Usage = ResourceUsage.Dynamic;
            desc.SizeInBytes = VBSize * sizeof(ImDrawVert);
            desc.BindFlags = BindFlags.VertexBuffer;
            desc.CpuAccessFlags = CpuAccessFlags.Write;
            desc.OptionFlags = 0;
            VB = new Buffer(_device, desc);
        }
        if (IB == null || IBSize < draw_data.TotalIdxCount)
        {
            Destroy(ref IB);
            IBSize = draw_data.TotalIdxCount + 10000;
            BufferDescription desc = default;
            desc.Usage = ResourceUsage.Dynamic;
            desc.SizeInBytes = IBSize * sizeof(ImDrawIdx);
            desc.BindFlags = BindFlags.IndexBuffer;
            desc.CpuAccessFlags = CpuAccessFlags.Write;
            IB = new Buffer(_device, desc);
        }

        // Upload vertex/index data into a single contiguous GPU buffer
        DataBox vtx_resource = device.MapSubresource(VB, 0, MapMode.WriteDiscard, 0);
        DataBox idx_resource = device.MapSubresource(IB, 0, MapMode.WriteDiscard, 0);
        ImDrawVert* vtx_dst = (ImDrawVert*)vtx_resource.DataPointer;
        ImDrawIdx* idx_dst = (ImDrawIdx*)idx_resource.DataPointer;
        for (int n = 0; n < draw_data.CmdListsCount; n++)
        {
            ImDrawListPtr draw_list = draw_data.CmdLists[n];
            System.Buffer.MemoryCopy((void*)draw_list.VtxBuffer.Data, vtx_dst, vtx_resource.RowPitch, draw_list.VtxBuffer.Size * sizeof(ImDrawVert));
            System.Buffer.MemoryCopy((void*)draw_list.IdxBuffer.Data, idx_dst, idx_resource.RowPitch, draw_list.IdxBuffer.Size * sizeof(ImDrawIdx));
            vtx_dst += draw_list.VtxBuffer.Size;
            idx_dst += draw_list.IdxBuffer.Size;
        }
        device.UnmapSubresource(VB, 0);
        device.UnmapSubresource(IB, 0);

        // Setup orthographic projection matrix into our constant buffer
        // Our visible imgui space lies from draw_data->DisplayPos (top left) to draw_data->DisplayPos+data_data->DisplaySize (bottom right). DisplayPos is (0,0) for single viewport apps.
        ImGuiIOPtr io = ImGui.GetIO();
        {
            DataBox mapped_resource = device.MapSubresource(VertexConstantBuffer, 0, MapMode.WriteDiscard, 0);
            Matrix4x4 mvp = Matrix4x4.CreateOrthographicOffCenter(0, io.DisplaySize.X, io.DisplaySize.Y, 0, -1, 1);
            Unsafe.WriteUnaligned((void*)mapped_resource.DataPointer, mvp);
            device.UnmapSubresource(VertexConstantBuffer, 0);
        }

        // Setup desired DX state
        SetupRenderState(draw_data, device);

        draw_data.ScaleClipRects(io.DisplayFramebufferScale);

        // Render command lists
        // (Because we merged all buffers into a single one, we maintain our own offset into them)
        int global_vtx_offset = 0;
        int global_idx_offset = 0;
        for (int n = 0; n < draw_data.CmdListsCount; n++)
        {
            ImDrawListPtr draw_list = draw_data.CmdLists[n];
            for (int cmd_i = 0; cmd_i < draw_list.CmdBuffer.Size; cmd_i++)
            {
                ImDrawCmdPtr pcmd = draw_list.CmdBuffer[cmd_i];
                if (pcmd.UserCallback != IntPtr.Zero)
                {
                    throw new NotImplementedException();
                }
                else
                {
                    // Apply scissor/clipping rectangle
                    Vector4 clipRect = pcmd.ClipRect;
                    device.Rasterizer.SetScissorRectangle((int)clipRect.X, (int)clipRect.Y, (int)clipRect.Z, (int)clipRect.W);

                    // Bind texture
                    IntPtr texId = pcmd.GetTexID();
                    if (texId == FontTextureView.NativePointer)
                    {
                        device.PixelShader.SetShaderResources(0, FontTextureView);
                    }
                    else
                    {
                        using var srv = new ShaderResourceView(texId);
                        device.PixelShader.SetShaderResource(0, srv);
                    }

                    // Draw
                    device.DrawIndexed((int)pcmd.ElemCount, (int)(pcmd.IdxOffset + global_idx_offset), (int)(pcmd.VtxOffset + global_vtx_offset));
                }
            }
            global_vtx_offset += draw_list.VtxBuffer.Size;
            global_idx_offset += draw_list.IdxBuffer.Size;
        }
    }
    
    private static unsafe void SetupRenderState(ImDrawDataPtr draw_data, DeviceContext device_ctx)
    {
        // Setup viewport
        RawViewportF vp = default;
        vp.Width = draw_data.DisplaySize.X;
        vp.Height = draw_data.DisplaySize.Y;
        vp.MinDepth = 0.0f;
        vp.MaxDepth = 1.0f;
        vp.X = vp.Y = 0;
        device_ctx.Rasterizer.SetViewport(vp);

        // Setup shader and vertex buffers
        device_ctx.InputAssembler.InputLayout = InputLayout;
        device_ctx.InputAssembler.SetVertexBuffers(0, new VertexBufferBinding(VB, sizeof(ImDrawVert), 0));
        device_ctx.InputAssembler.SetIndexBuffer(IB, sizeof(ImDrawIdx) == 2 ? Format.R16_UInt : Format.R32_UInt, 0);
        device_ctx.InputAssembler.PrimitiveTopology = PrimitiveTopology.TriangleList;

        device_ctx.VertexShader.Set(VertexShader);
        device_ctx.VertexShader.SetConstantBuffer(0, VertexConstantBuffer);
        device_ctx.PixelShader.Set(PixelShader);
        device_ctx.PixelShader.SetSampler(0, FontSampler);

        device_ctx.GeometryShader.Set(null);
        device_ctx.HullShader    .Set(null); // In theory we should backup and restore this as well.. very infrequently used..
        device_ctx.DomainShader  .Set(null); // In theory we should backup and restore this as well.. very infrequently used..
        device_ctx.ComputeShader .Set(null); // In theory we should backup and restore this as well.. very infrequently used..

        // Setup blend state
        device_ctx.OutputMerger.SetBlendState(BlendState, new RawColor4(0, 0, 0, 0), 0xffffffffu);
        device_ctx.OutputMerger.SetDepthStencilState(DepthStencilState, 0);
        device_ctx.Rasterizer.State = RasterizerState;
    }

    private static void CreateDeviceResources()
    {
        VertexShader = _device.CreateVertexShader(VS_SOURCE, out byte[] vsBytecode);
        InputLayout = new InputLayout(_device, vsBytecode, new InputElement[]
        {
            new( "POSITION", 0, Format.R32G32_Float,   offset: 0,  0, InputClassification.PerVertexData, 0 ),
            new( "TEXCOORD", 0, Format.R32G32_Float,   offset: 8,  0, InputClassification.PerVertexData, 0 ),
            new( "COLOR",    0, Format.R8G8B8A8_UNorm, offset: 16, 0, InputClassification.PerVertexData, 0 ),
        });
        PixelShader = _device.CreatePixelShader(PS_SOURCE);

        // Create the constant buffer
        {
            BufferDescription desc = default;
            desc.SizeInBytes = sizeof(float) * 4 * 4;
            desc.Usage = ResourceUsage.Dynamic;
            desc.BindFlags = BindFlags.ConstantBuffer;
            desc.CpuAccessFlags = CpuAccessFlags.Write;
            desc.OptionFlags = 0;
            VertexConstantBuffer = new Buffer(_device, desc);
        }

        // Create the blending setup
        {
            BlendStateDescription desc = default;
            desc.AlphaToCoverageEnable = false;
            desc.RenderTarget[0].IsBlendEnabled = true;
            desc.RenderTarget[0].SourceBlend = BlendOption.SourceAlpha;
            desc.RenderTarget[0].DestinationBlend = BlendOption.InverseSourceAlpha;
            desc.RenderTarget[0].BlendOperation = BlendOperation.Add;
            desc.RenderTarget[0].SourceAlphaBlend = BlendOption.One;
            desc.RenderTarget[0].DestinationAlphaBlend = BlendOption.InverseSourceAlpha;
            desc.RenderTarget[0].AlphaBlendOperation = BlendOperation.Add;
            desc.RenderTarget[0].RenderTargetWriteMask = ColorWriteMaskFlags.All;
            BlendState = new BlendState(_device, desc);
        }

        // Create the rasterizer state
        {
            RasterizerStateDescription desc = default;
            desc.FillMode = FillMode.Solid;
            desc.CullMode = CullMode.None;
            desc.IsScissorEnabled = true;
            desc.IsDepthClipEnabled = true;
            RasterizerState = new RasterizerState(_device, desc);
        }

        // Create depth-stencil State
        {
            DepthStencilStateDescription desc = default;
            desc.IsDepthEnabled = false;
            desc.DepthWriteMask = DepthWriteMask.All;
            desc.DepthComparison = Comparison.Always;
            desc.IsStencilEnabled = false;
            desc.FrontFace.FailOperation = desc.FrontFace.DepthFailOperation = desc.FrontFace.PassOperation = StencilOperation.Keep;
            desc.FrontFace.Comparison = Comparison.Always;
            desc.BackFace = desc.FrontFace;
            DepthStencilState = new DepthStencilState(_device, desc);
        }

        // Create texture sampler
        // (Bilinear sampling is required by default. Set 'io.Fonts->Flags |= ImFontAtlasFlags_NoBakedLines' or 'style.AntiAliasedLinesUseTex = false' to allow point/nearest sampling)
        {
            SamplerStateDescription desc = default;
            desc.Filter = Filter.MinMagMipLinear;
            desc.AddressU = TextureAddressMode.Clamp;
            desc.AddressV = TextureAddressMode.Clamp;
            desc.AddressW = TextureAddressMode.Clamp;
            desc.MipLodBias = 0;
            desc.ComparisonFunction = Comparison.Always;
            desc.MinimumLod = 0;
            desc.MaximumLod = 0;
            FontSampler = new SamplerState(_device, desc);
        }

        CreateFontsTexture();
    }

    private static unsafe void CreateFontsTexture()
    {
        // Build texture atlas
        ImGuiIOPtr io = ImGui.GetIO();
        io.Fonts.GetTexDataAsRGBA32(out byte* pixels, out int width, out int height);

        // Upload texture to graphics system
        {
            Texture2DDescription desc = default;
            desc.Width = width;
            desc.Height = height;
            desc.MipLevels = 1;
            desc.ArraySize = 1;
            desc.Format = Format.R8G8B8A8_UNorm;
            desc.SampleDescription.Count = 1;
            desc.Usage = ResourceUsage.Default;
            desc.BindFlags = BindFlags.ShaderResource;
            desc.CpuAccessFlags = 0;

            DataBox subResource = default;
            subResource.DataPointer = (IntPtr)pixels;
            subResource.RowPitch = desc.Width * 4;
            subResource.SlicePitch = 0;
            Texture2D texture = new Texture2D(_device, desc, [subResource]);

            // Create texture view
            ShaderResourceViewDescription srvDesc = default;
            srvDesc.Format = Format.R8G8B8A8_UNorm;
            srvDesc.Dimension = ShaderResourceViewDimension.Texture2D;
            srvDesc.Texture2D.MipLevels = desc.MipLevels;
            srvDesc.Texture2D.MostDetailedMip = 0;
            FontTextureView = new ShaderResourceView(_device, texture, srvDesc);
        }

        // Store our identifier
        io.Fonts.SetTexID(FontTextureView.NativePointer);
    }

    private static void DestroyFontsTexture()
    {
        if (FontTextureView != null)
        {
            FontTextureView.Dispose();
            FontTextureView = null;
            ImGui.GetIO().Fonts.SetTexID(IntPtr.Zero); // We copied data->pFontTextureView to io.Fonts->TexID so let's clear that as well.
        }
    }

    private static void DestroyDeviceResources()
    {
        DestroyFontsTexture();

        Destroy(ref FontSampler);
        Destroy(ref IB);
        Destroy(ref VB);
        Destroy(ref BlendState);
        Destroy(ref DepthStencilState);
        Destroy(ref RasterizerState);
        Destroy(ref PixelShader);
        Destroy(ref VertexConstantBuffer);
        Destroy(ref InputLayout);
        Destroy(ref VertexShader);
    }

    public static void CreateBackbufferResources(SwapChain swapChain)
    {
        _backbuffer = swapChain.GetBackBuffer<Texture2D>(0);
        _backbufferSize = new ImVec2(_backbuffer.Description.Width, _backbuffer.Description.Height);
        _backbufferRtv = new RenderTargetView(_device, _backbuffer, new RenderTargetViewDescription
        {
            Format = Format.R8G8B8A8_UNorm,
            Dimension = RenderTargetViewDimension.Texture2D,
        });
    }

    public static void DestroyBackbufferResources()
    {
        Destroy(ref _backbuffer);
        _backbufferSize = default;
        Destroy(ref _backbufferRtv);
    }

    public static void Shutdown()
    {
        if (!_initialized)
        {
            throw new Exception("Not initialized or already shutdown.");
        }

        _initialized = false;

        _gameWindow.MouseMove -= OnMouseMove;

        ImGuiIOPtr io = ImGui.GetIO();
        io.BackendFlags = ImGuiBackendFlags.None;
        io.ConfigFlags = ImGuiConfigFlags.None;

        DestroyDeviceResources();
        ImGui.DestroyContext();

        _gameWindow = null;
        Destroy(ref _device);
        DestroyBackbufferResources();

        Destroy(ref _input);
        Destroy(ref _mouse);
        _mouseState = null;
        Destroy(ref _keyboard);
        _keyboardState = null;
    }

    // utils
    private static VertexShader CreateVertexShader(this Device device, string shaderSource, out byte[] shaderBytecode)
    {
        CompilationResult compilation = ShaderBytecode.Compile(shaderSource, "main", "vs_5_0", sourceFileName: nameof(ImGuiImpl));
        shaderBytecode = compilation;
        return new VertexShader(device, compilation);
    }

    private static PixelShader CreatePixelShader(this Device device, string shaderSource)
    {
        CompilationResult compilation = ShaderBytecode.Compile(shaderSource, "main", "ps_5_0", sourceFileName: nameof(ImGuiImpl));
        return new PixelShader(device, compilation);
    }

    private static void Destroy<T>(ref T obj) where T : class, IDisposable
    {
        if (obj != null)
        {
            obj.Dispose();
            obj = null;
        }
    }

    private static ImGuiKey ToImGui(this Key key)
    {
        return key switch
        {
            // modifiers
            Key.LeftControl => ImGuiKey.LeftCtrl,
            Key.RightControl => ImGuiKey.RightCtrl,
            Key.LeftShift => ImGuiKey.LeftShift,
            Key.RightShift => ImGuiKey.RightShift,
            Key.LeftAlt => ImGuiKey.LeftAlt,
            Key.RightAlt => ImGuiKey.RightAlt,

            Key.Escape => ImGuiKey.Escape,
            Key.Tab => ImGuiKey.Tab,
            Key.Grave => ImGuiKey.GraveAccent,
            Key.Capital => ImGuiKey.CapsLock,
            Key.Space => ImGuiKey.Space,
            Key.Back => ImGuiKey.Backspace,
            Key.Return => ImGuiKey.Enter,
            Key.PrintScreen => ImGuiKey.PrintScreen,
            Key.NumberLock => ImGuiKey.NumLock,
            Key.ScrollLock => ImGuiKey.ScrollLock,
            Key.Pause => ImGuiKey.Pause,

            Key.Minus => ImGuiKey.Minus,
            //Key.Underline => ,
            Key.Equals => ImGuiKey.Equal,

            Key.LeftBracket => ImGuiKey.LeftBracket,
            Key.RightBracket => ImGuiKey.RightBracket,
            Key.Backslash => ImGuiKey.Backslash,

            Key.Semicolon => ImGuiKey.Semicolon,
            //Key.Colon => ,
            Key.Apostrophe => ImGuiKey.Apostrophe,

            Key.Comma => ImGuiKey.Comma,
            Key.Period => ImGuiKey.Period,
            Key.Slash => ImGuiKey.Slash,

            Key.Insert => ImGuiKey.Insert,
            Key.Delete => ImGuiKey.Delete,
            Key.Home => ImGuiKey.Home,
            Key.End => ImGuiKey.End,
            Key.PageUp => ImGuiKey.PageUp,
            Key.PageDown => ImGuiKey.PageDown,

            // alphabet
            Key.A => ImGuiKey.A,
            Key.B => ImGuiKey.B,
            Key.C => ImGuiKey.C,
            Key.D => ImGuiKey.D,
            Key.E => ImGuiKey.E,
            Key.F => ImGuiKey.F,
            Key.G => ImGuiKey.G,
            Key.H => ImGuiKey.H,
            Key.I => ImGuiKey.I,
            Key.J => ImGuiKey.J,
            Key.K => ImGuiKey.K,
            Key.L => ImGuiKey.L,
            Key.M => ImGuiKey.M,
            Key.N => ImGuiKey.N,
            Key.O => ImGuiKey.O,
            Key.P => ImGuiKey.P,
            Key.Q => ImGuiKey.Q,
            Key.R => ImGuiKey.R,
            Key.S => ImGuiKey.S,
            Key.T => ImGuiKey.T,
            Key.U => ImGuiKey.U,
            Key.V => ImGuiKey.V,
            Key.W => ImGuiKey.W,
            Key.X => ImGuiKey.X,
            Key.Y => ImGuiKey.Y,
            Key.Z => ImGuiKey.Z,

            // numbers
            Key.D0 => ImGuiKey._0,
            Key.D1 => ImGuiKey._1,
            Key.D2 => ImGuiKey._2,
            Key.D3 => ImGuiKey._3,
            Key.D4 => ImGuiKey._4,
            Key.D5 => ImGuiKey._5,
            Key.D6 => ImGuiKey._6,
            Key.D7 => ImGuiKey._7,
            Key.D8 => ImGuiKey._8,
            Key.D9 => ImGuiKey._9,

            // numpad
            Key.NumberPad0 => ImGuiKey.Keypad0,
            Key.NumberPad1 => ImGuiKey.Keypad1,
            Key.NumberPad2 => ImGuiKey.Keypad2,
            Key.NumberPad3 => ImGuiKey.Keypad3,
            Key.NumberPad4 => ImGuiKey.Keypad4,
            Key.NumberPad5 => ImGuiKey.Keypad5,
            Key.NumberPad6 => ImGuiKey.Keypad6,
            Key.NumberPad7 => ImGuiKey.Keypad7,
            Key.NumberPad8 => ImGuiKey.Keypad8,
            Key.NumberPad9 => ImGuiKey.Keypad9,

            Key.Decimal => ImGuiKey.KeypadDecimal,
            Key.Divide => ImGuiKey.KeypadDivide,
            Key.Multiply => ImGuiKey.KeypadMultiply,
            Key.Subtract => ImGuiKey.KeypadSubtract,
            Key.Add => ImGuiKey.KeypadAdd,
            Key.NumberPadEnter => ImGuiKey.KeypadEnter,
            Key.NumberPadEquals => ImGuiKey.KeypadEqual,
            //Key.NumberPadComma => ,

            // function keys
            Key.F1 => ImGuiKey.F1,
            Key.F2 => ImGuiKey.F2,
            Key.F3 => ImGuiKey.F3,
            Key.F4 => ImGuiKey.F4,
            Key.F5 => ImGuiKey.F5,
            Key.F6 => ImGuiKey.F6,
            Key.F7 => ImGuiKey.F7,
            Key.F8 => ImGuiKey.F8,
            Key.F9 => ImGuiKey.F9,
            Key.F10 => ImGuiKey.F10,
            Key.F11 => ImGuiKey.F11,
            Key.F12 => ImGuiKey.F12,
            Key.F13 => ImGuiKey.F13,
            Key.F14 => ImGuiKey.F14,
            Key.F15 => ImGuiKey.F15,

            // arrows
            Key.Up => ImGuiKey.UpArrow,
            Key.Down => ImGuiKey.DownArrow,
            Key.Left => ImGuiKey.LeftArrow,
            Key.Right => ImGuiKey.RightArrow,

            _ => ImGuiKey.None,
        };
    }
}
