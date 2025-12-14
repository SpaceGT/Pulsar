using ImGuiNET;
using SharpDX;
using SharpDX.D3DCompiler;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.Mathematics.Interop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Buffer = SharpDX.Direct3D11.Buffer;
using Format = SharpDX.DXGI.Format;
using Device = SharpDX.Direct3D11.Device;
using ImVec2 = System.Numerics.Vector2;
using System.Runtime.CompilerServices;
using VRageRender;

namespace Pulsar.Legacy.ImGuiBackends;

public struct ImDrawIdx
{
    public ushort Value;
}

public unsafe struct ResourceMapping : IDisposable
{
    private DeviceContext _dc;
    private Buffer _buffer;
    private void* _ptr;

    public void Dispose()
    {
        if (_dc != null)
        {
            _dc.UnmapSubresource(_buffer, 0);
            _dc = null;
            _buffer = null;
            _ptr = null;
        }
    }

    public static ResourceMapping WriteDiscard(DeviceContext deviceContext, Buffer buffer)
    {
        return new ResourceMapping
        {
            _dc = deviceContext,
            _buffer = buffer,
            _ptr = (void*)deviceContext.MapSubresource(buffer, 0, MapMode.WriteDiscard, 0).DataPointer,
        };
    }
}

// from imgui 1.91.6
public static class ImGui_ImplDX11
{
    // initialized in Init()
    static Device Device;
    static DeviceContext DeviceContext;

    // initialized in CreateDeviceObjects()
    static Buffer VB;
    static Buffer IB;
    static VertexShader VertexShader;
    static InputLayout InputLayout;
    static Buffer VertexConstantBuffer;
    static PixelShader PixelShader;
    static SamplerState FontSampler;
    static ShaderResourceView FontTextureView;
    static RasterizerState RasterizerState;
    static BlendState BlendState;
    static DepthStencilState DepthStencilState;
    static int VertexBufferSize = 5000;
    static int IndexBufferSize = 10000;

    public static bool Init(Device device, DeviceContext device_context)
    {
        if (Device != null)
        {
            throw new Exception("Already initialized a renderer backend!");
        }

        ImGuiIOPtr io = ImGui.GetIO();

        // Setup backend capabilities flags
        io.BackendRendererUserData = device.NativePointer;
        //io.BackendRendererName = "imgui_impl_dx11";
        io.BackendFlags |= ImGuiBackendFlags.RendererHasVtxOffset;  // We can honor the ImDrawCmd::VtxOffset field, allowing for large meshes.
        io.ConfigFlags |= ImGuiConfigFlags.NavEnableKeyboard | ImGuiConfigFlags.DockingEnable;
        io.Fonts.Flags |= ImFontAtlasFlags.NoBakedLines;

        CreateDeviceObjects();

        Device = device;
        DeviceContext = device_context;

        return true;
    }

    public static unsafe bool CreateDeviceObjects()
    {
        if (Device == null)
            return false;
        if (FontSampler != null)
            InvalidateDeviceObjects();

        // By using D3DCompile() from <d3dcompiler.h> / d3dcompiler.lib, we introduce a dependency to a given version of d3dcompiler_XX.dll (see D3DCOMPILER_DLL_A)
        // If you would like to use this DX11 sample code but remove this dependency you can:
        //  1) compile once, save the compiled shader blobs into a file or source code and pass them to CreateVertexShader()/CreatePixelShader() [preferred solution]
        //  2) use code to detect any version of the DLL and grab a pointer to D3DCompile from the DLL.
        // See https://github.com/ocornut/imgui/pull/638 for sources and details.

        // Create the vertex shader
        {
            const string vertexShader =
@"
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

PS_INPUT VS(VS_INPUT input)
{
    PS_INPUT output;
    output.pos = mul(ProjectionMatrix, float4(input.pos.xy, 0.f, 1.f));
    output.col = input.col;
    output.uv = input.uv;
    return output;
}
";

            VertexShader = Device.CreateVertexShader(vertexShader, out byte[] vertexShaderBytecode);

            // Create the input layout
            InputElement[] local_layout =
            {
                new( "POSITION", 0, Format.R32G32_Float,   offset: 0,  0, InputClassification.PerVertexData, 0 ),
                new( "TEXCOORD", 0, Format.R32G32_Float,   offset: 8,  0, InputClassification.PerVertexData, 0 ),
                new( "COLOR",    0, Format.R8G8B8A8_UNorm, offset: 16, 0, InputClassification.PerVertexData, 0 ),
            };
            InputLayout = new InputLayout(Device, vertexShaderBytecode, local_layout);

            // Create the constant buffer
            {
                BufferDescription desc = default;
                desc.SizeInBytes = sizeof(float) * 4 * 4;
                desc.Usage = ResourceUsage.Dynamic;
                desc.BindFlags = BindFlags.ConstantBuffer;
                desc.CpuAccessFlags = CpuAccessFlags.Write;
                desc.OptionFlags = 0;
                VertexConstantBuffer = new Buffer(Device, desc);
            }
        }

        // Create the pixel shader
        {
            const string pixelShader =
@"
struct PS_INPUT
{
    float4 pos : SV_POSITION;
    float4 col : COLOR0;
    float2 uv  : TEXCOORD0;
};

Texture2D FontTexture : register(t0);
sampler FontSampler : register(s0);

float4 FS(PS_INPUT input) : SV_Target
{
    float4 out_col = input.col * FontTexture.Sample(FontSampler, input.uv);
    return out_col;
}
";

            PixelShader = Device.CreatePixelShader(pixelShader);
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
            BlendState = new BlendState(Device, desc);
        }

        // Create the rasterizer state
        {
            RasterizerStateDescription desc = default;
            desc.FillMode = FillMode.Solid;
            desc.CullMode = CullMode.None;
            desc.IsScissorEnabled = true;
            desc.IsDepthClipEnabled = true;
            RasterizerState = new RasterizerState(Device, desc);
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
            DepthStencilState = new DepthStencilState(Device, desc);
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
            FontSampler = new SamplerState(Device, desc);
        }

        CreateFontsTexture();

        return true;
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

    public static unsafe void RenderDrawData(ImDrawDataPtr draw_data)
    {
        // Avoid rendering when minimized
        if (draw_data.DisplaySize.X <= 0.0f || draw_data.DisplaySize.Y <= 0.0f)
            return;

        if (draw_data.CmdListsCount == 0)
            return;

        DeviceContext device = DeviceContext;

        // Create and grow vertex/index buffers if needed
        if (VB == null || VertexBufferSize < draw_data.TotalVtxCount)
        {
            if (VB != null) { VB.Dispose(); VB = null; }
            VertexBufferSize = draw_data.TotalVtxCount + 5000;
            BufferDescription desc = default;
            desc.Usage = ResourceUsage.Dynamic;
            desc.SizeInBytes = VertexBufferSize * sizeof(ImDrawVert);
            desc.BindFlags = BindFlags.VertexBuffer;
            desc.CpuAccessFlags = CpuAccessFlags.Write;
            desc.OptionFlags = 0;
            VB = new Buffer(Device, desc);
        }
        if (IB == null || IndexBufferSize < draw_data.TotalIdxCount)
        {
            if (IB != null) { IB.Dispose(); IB = null; }
            IndexBufferSize = draw_data.TotalIdxCount + 10000;
            BufferDescription desc = default;
            desc.Usage = ResourceUsage.Dynamic;
            desc.SizeInBytes = IndexBufferSize * sizeof(ImDrawIdx);
            desc.BindFlags = BindFlags.IndexBuffer;
            desc.CpuAccessFlags = CpuAccessFlags.Write;
            IB = new Buffer(Device, desc);
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
        ImVec2 clip_off = draw_data.DisplayPos;
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
                    device.Rasterizer.SetScissorRectangle((int)clipRect.X, (int)clipRect.Y, (int)(clipRect.Z - clipRect.X), (int)(clipRect.W - clipRect.Y));

                    // Bind texture, Draw
                    using ShaderResourceView texture_srv = new ShaderResourceView(pcmd.GetTexID());
                    device.PixelShader.SetShaderResource(0, texture_srv);
                    device.DrawIndexed((int)pcmd.ElemCount, (int)(pcmd.IdxOffset + global_idx_offset), (int)(pcmd.VtxOffset + global_vtx_offset));
                }
            }
            global_vtx_offset += draw_list.VtxBuffer.Size;
            global_idx_offset += draw_list.IdxBuffer.Size;
        }
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
            Texture2D texture = new Texture2D(Device, desc, [subResource]);

            // Create texture view
            ShaderResourceViewDescription srvDesc = default;
            srvDesc.Format = Format.R8G8B8A8_UNorm;
            srvDesc.Dimension = ShaderResourceViewDimension.Texture2D;
            srvDesc.Texture2D.MipLevels = desc.MipLevels;
            srvDesc.Texture2D.MostDetailedMip = 0;
            FontTextureView = new ShaderResourceView(Device, texture, srvDesc);
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

    public static void InvalidateDeviceObjects()
    {
        if (Device == null)
            return;

        DestroyFontsTexture();

        if (FontSampler != null)          { FontSampler.Dispose(); FontSampler = null; }
        if (IB != null)                   { IB.Dispose(); IB = null; }
        if (VB != null)                   { VB.Dispose(); VB = null; }
        if (BlendState != null)           { BlendState.Dispose(); BlendState = null; }
        if (DepthStencilState != null)    { DepthStencilState.Dispose(); DepthStencilState = null; }
        if (RasterizerState != null)      { RasterizerState.Dispose(); RasterizerState = null; }
        if (PixelShader != null)          { PixelShader.Dispose(); PixelShader = null; }
        if (VertexConstantBuffer != null) { VertexConstantBuffer.Dispose(); VertexConstantBuffer = null; }
        if (InputLayout != null)          { InputLayout.Dispose(); InputLayout = null; }
        if (VertexShader != null)         { VertexShader.Dispose(); VertexShader = null; }
    }

    public static void Shutdown()
    {
        if (Device == null)
        {
            throw new Exception("No renderer backend to shutdown, or already shutdown?");
        }

        ImGuiIOPtr io = ImGui.GetIO();

        InvalidateDeviceObjects();
        Device = null;
        DeviceContext = null;
        //io.BackendRendererName = nullptr;
        io.BackendRendererUserData = IntPtr.Zero;
        io.BackendFlags &= ~ImGuiBackendFlags.RendererHasVtxOffset;
    }

    public static void NewFrame()
    {
        if (Device == null)
        {
            throw new Exception("Context or backend not initialized! Did you call ImGui_ImplDX11_Init()?");
        }

        if (FontSampler == null)
            CreateDeviceObjects();
    }

    // utils
    private static VertexShader CreateVertexShader(this Device device, string shaderSource, out byte[] shaderBytecode)
    {
        CompilationResult compilation = ShaderBytecode.Compile(shaderSource, "VS", "vs_5_0", sourceFileName: nameof(ImGui_ImplDX11));
        shaderBytecode = compilation;
        return new VertexShader(device, compilation);
    }

    private static PixelShader CreatePixelShader(this Device device, string shaderSource)
    {
        CompilationResult compilation = ShaderBytecode.Compile(shaderSource, "FS", "ps_5_0", sourceFileName: nameof(ImGui_ImplDX11));
        return new PixelShader(device, compilation);
    }
}
