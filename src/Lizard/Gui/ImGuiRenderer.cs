using System.Numerics;
using System.Reflection;
using ImGuiNET;
using Veldrid;

namespace Lizard.Gui;

public sealed class ImGuiRenderer : IDisposable // This is largely based on Veldrid.ImGuiRenderer from Veldrid.ImGui
{
    record struct ResourceSetInfo(IntPtr ImGuiBinding, ResourceSet ResourceSet);
    readonly IntPtr _fontAtlasId = (IntPtr)1;
    readonly Vector2 _scaleFactor = Vector2.One;

    // Image trackers
    readonly Dictionary<TextureView, ResourceSetInfo> _setsByView = new();
    readonly Dictionary<Texture, TextureView> _autoViewsByTexture = new();
    readonly Dictionary<IntPtr, ResourceSetInfo> _viewsById = new();
    readonly List<IDisposable> _ownedResources = new();

    // Device objects
    readonly DeviceBuffer _projMatrixBuffer;
    readonly Shader _vertexShader;
    readonly Shader _fragmentShader;
    readonly ResourceLayout _layout;
    readonly ResourceLayout _textureLayout;
    readonly Pipeline _pipeline;
    readonly ResourceSet _mainResourceSet;

    DeviceBuffer _vertexBuffer;
    DeviceBuffer _indexBuffer;
    Texture? _fontTexture;
    ResourceSet? _fontTextureResourceSet;

    int _windowWidth;
    int _windowHeight;
    int _lastAssignedId = 100;
    bool _frameBegun;
    readonly GraphicsDevice _gd;

    /// <summary>
    /// Constructs a new ImGuiRenderer.
    /// </summary>
    /// <param name="gd">The GraphicsDevice used to create and update resources.</param>
    /// <param name="outputDescription">The output format.</param>
    /// <param name="width">The initial width of the rendering target. Can be resized.</param>
    /// <param name="height">The initial height of the rendering target. Can be resized.</param>
    public ImGuiRenderer(GraphicsDevice gd, OutputDescription outputDescription, int width, int height)
    {
        _gd = gd ?? throw new ArgumentNullException(nameof(gd));
        _windowWidth = width;
        _windowHeight = height;

        IntPtr context = ImGui.CreateContext();
        ImGui.SetCurrentContext(context);

        var io = ImGui.GetIO();
        unsafe { io.NativePtr->IniFilename = null; } // Turn off ini file
        io.ConfigFlags |= ImGuiConfigFlags.DockingEnable;
        io.Fonts.AddFontDefault();
        io.Fonts.Flags |= ImFontAtlasFlags.NoBakedLines;

        ResourceFactory factory = gd.ResourceFactory;
        _vertexBuffer = factory.CreateBuffer(new BufferDescription(10000, BufferUsage.VertexBuffer | BufferUsage.Dynamic));
        _vertexBuffer.Name = "ImGui.NET Vertex Buffer";
        _indexBuffer = factory.CreateBuffer(new BufferDescription(2000, BufferUsage.IndexBuffer | BufferUsage.Dynamic));
        _indexBuffer.Name = "ImGui.NET Index Buffer";

        _projMatrixBuffer = factory.CreateBuffer(new BufferDescription(64, BufferUsage.UniformBuffer | BufferUsage.Dynamic));
        _projMatrixBuffer.Name = "ImGui.NET Projection Buffer";

        var assembly = typeof(ImGuiRenderer).GetTypeInfo().Assembly;
        byte[] vertexShaderBytes = ShaderLoader.LoadEmbeddedShaderCode(assembly, gd.ResourceFactory, "imgui-vertex");
        byte[] fragmentShaderBytes = ShaderLoader.LoadEmbeddedShaderCode(assembly, gd.ResourceFactory, "imgui-frag");
        _vertexShader = factory.CreateShader(new ShaderDescription(ShaderStages.Vertex, vertexShaderBytes, gd.BackendType == GraphicsBackend.Vulkan ? "main" : "VS"));
        _vertexShader.Name = "ImGui.NET Vertex Shader";
        _fragmentShader = factory.CreateShader(new ShaderDescription(ShaderStages.Fragment, fragmentShaderBytes, gd.BackendType == GraphicsBackend.Vulkan ? "main" : "FS"));
        _fragmentShader.Name = "ImGui.NET Fragment Shader";

        VertexLayoutDescription[] vertexLayouts = {
            new(
                new VertexElementDescription("in_position", VertexElementSemantic.Position, VertexElementFormat.Float2),
                new VertexElementDescription("in_texCoord", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float2),
                new VertexElementDescription("in_color", VertexElementSemantic.Color, VertexElementFormat.Byte4_Norm))
        };

        _layout = factory.CreateResourceLayout(new ResourceLayoutDescription(
            new ResourceLayoutElementDescription("ProjectionMatrixBuffer", ResourceKind.UniformBuffer, ShaderStages.Vertex),
            new ResourceLayoutElementDescription("MainSampler", ResourceKind.Sampler, ShaderStages.Fragment)));
        _layout.Name = "ImGui.NET Resource Layout";
        _textureLayout = factory.CreateResourceLayout(new ResourceLayoutDescription(
            new ResourceLayoutElementDescription("MainTexture", ResourceKind.TextureReadOnly, ShaderStages.Fragment)));
        _textureLayout.Name = "ImGui.NET Texture Layout";

        GraphicsPipelineDescription pd = new GraphicsPipelineDescription(
            BlendStateDescription.SingleAlphaBlend,
            new DepthStencilStateDescription(false, false, ComparisonKind.Always),
            new RasterizerStateDescription(FaceCullMode.None, PolygonFillMode.Solid, FrontFace.Clockwise, true, true),
            PrimitiveTopology.TriangleList,
            new ShaderSetDescription(
                vertexLayouts,
                new[] { _vertexShader, _fragmentShader },
                new[] { new SpecializationConstant(0, gd.IsClipSpaceYInverted), }),
            new[] { _layout, _textureLayout },
            outputDescription,
            ResourceBindingModel.Default);
        _pipeline = factory.CreateGraphicsPipeline(ref pd);
        _pipeline.Name = "ImGui.NET Pipeline";

        _mainResourceSet = factory.CreateResourceSet(new ResourceSetDescription(_layout,
            _projMatrixBuffer,
            gd.PointSampler));
        _mainResourceSet.Name = "ImGui.NET Main Resource Set";
        RecreateFontDeviceTexture(gd);

        SetPerFrameImGuiData(1f / 60f);

        ImGui.NewFrame();
        _frameBegun = true;
    }

    public void WindowResized(int width, int height)
    {
        _windowWidth = width;
        _windowHeight = height;
    }

    /// <summary>
    /// Gets or creates a handle for a texture to be drawn with ImGui.
    /// Pass the returned handle to Image() or ImageButton().
    /// </summary>
    public IntPtr GetOrCreateImGuiBinding(ResourceFactory factory, TextureView textureView)
    {
        if (!_setsByView.TryGetValue(textureView, out ResourceSetInfo rsi))
        {
            ResourceSet resourceSet = factory.CreateResourceSet(new ResourceSetDescription(_textureLayout, textureView));
            resourceSet.Name = $"ImGui.NET {textureView.Name} Resource Set";
            rsi = new ResourceSetInfo(GetNextImGuiBindingID(), resourceSet);

            _setsByView.Add(textureView, rsi);
            _viewsById.Add(rsi.ImGuiBinding, rsi);
            _ownedResources.Add(resourceSet);
        }

        return rsi.ImGuiBinding;
    }

    public void RemoveImGuiBinding(TextureView textureView)
    {
        if (_setsByView.TryGetValue(textureView, out ResourceSetInfo rsi))
        {
            _setsByView.Remove(textureView);
            _viewsById.Remove(rsi.ImGuiBinding);
            _ownedResources.Remove(rsi.ResourceSet);
            rsi.ResourceSet.Dispose();
        }
    }

    IntPtr GetNextImGuiBindingID()
    {
        int newID = _lastAssignedId++;
        return (IntPtr)newID;
    }

    /// <summary>
    /// Gets or creates a handle for a texture to be drawn with ImGui.
    /// Pass the returned handle to Image() or ImageButton().
    /// </summary>
    public IntPtr GetOrCreateImGuiBinding(ResourceFactory factory, Texture texture)
    {
        if (!_autoViewsByTexture.TryGetValue(texture, out TextureView? textureView))
        {
            textureView = factory.CreateTextureView(texture);
            textureView.Name = $"ImGui.NET {texture.Name} View";
            _autoViewsByTexture.Add(texture, textureView);
            _ownedResources.Add(textureView);
        }

        return GetOrCreateImGuiBinding(factory, textureView);
    }

    public void RemoveImGuiBinding(Texture texture)
    {
        if (_autoViewsByTexture.TryGetValue(texture, out TextureView? textureView))
        {
            _autoViewsByTexture.Remove(texture);
            _ownedResources.Remove(textureView);
            textureView.Dispose();
            RemoveImGuiBinding(textureView);
        }
    }

    /// <summary>
    /// Retrieves the shader texture binding for the given helper handle.
    /// </summary>
    public ResourceSet GetImageResourceSet(IntPtr imGuiBinding)
    {
        if (!_viewsById.TryGetValue(imGuiBinding, out ResourceSetInfo rsi))
            throw new InvalidOperationException("No registered ImGui binding with id " + imGuiBinding);

        return rsi.ResourceSet;
    }

    public void ClearCachedImageResources()
    {
        foreach (IDisposable resource in _ownedResources)
        {
            resource.Dispose();
        }

        _ownedResources.Clear();
        _setsByView.Clear();
        _viewsById.Clear();
        _autoViewsByTexture.Clear();
        _lastAssignedId = 100;
    }

    /// <summary>
    /// Recreates the device texture used to render text.
    /// </summary>
    public unsafe void RecreateFontDeviceTexture() => RecreateFontDeviceTexture(_gd);

    /// <summary>
    /// Recreates the device texture used to render text.
    /// </summary>
    unsafe void RecreateFontDeviceTexture(GraphicsDevice gd)
    {
        ImGuiIOPtr io = ImGui.GetIO();
        // Build
        io.Fonts.GetTexDataAsRGBA32(out byte* pixels, out int width, out int height, out int bytesPerPixel);

        // Store our identifier
        io.Fonts.SetTexID(_fontAtlasId);

        _fontTexture?.Dispose();
        _fontTexture = gd.ResourceFactory.CreateTexture(TextureDescription.Texture2D(
            (uint)width,
            (uint)height,
            1,
            1,
            PixelFormat.R8_G8_B8_A8_UNorm,
            TextureUsage.Sampled));
        _fontTexture.Name = "ImGui.NET Font Texture";
        gd.UpdateTexture(
            _fontTexture,
            (IntPtr)pixels,
            (uint)(bytesPerPixel * width * height),
            0,
            0,
            0,
            (uint)width,
            (uint)height,
            1,
            0,
            0);

        _fontTextureResourceSet?.Dispose();
        _fontTextureResourceSet = gd.ResourceFactory.CreateResourceSet(new ResourceSetDescription(_textureLayout, _fontTexture));
        _fontTextureResourceSet.Name = "ImGui.NET Font Texture Resource Set";

        io.Fonts.ClearTexData();
    }

    /// <summary>
    /// Renders the ImGui draw list data.
    /// </summary>
    public unsafe void Render(GraphicsDevice gd, CommandList cl)
    {
        if (_frameBegun)
        {
            _frameBegun = false;
            ImGui.Render();
            RenderImDrawData(ImGui.GetDrawData(), gd, cl);
        }
    }

    /// <summary>
    /// Updates ImGui input and IO configuration state.
    /// </summary>
    public void Update(float deltaSeconds, InputSnapshot snapshot)
    {
        BeginUpdate(deltaSeconds);
        UpdateImGuiInput(snapshot);
        EndUpdate();
    }

    /// <summary>
    /// Called before we handle the input in <see cref="Update(float, InputSnapshot)"/>.
    /// This render ImGui and update the state.
    /// </summary>
    void BeginUpdate(float deltaSeconds)
    {
        if (_frameBegun)
        {
            ImGui.Render();
        }

        SetPerFrameImGuiData(deltaSeconds);
    }

    /// <summary>
    /// Called at the end of <see cref="Update(float, InputSnapshot)"/>.
    /// This tells ImGui that we are on the next frame.
    /// </summary>
    void EndUpdate()
    {
        _frameBegun = true;
        ImGui.NewFrame();
    }

    /// <summary>
    /// Sets per-frame data based on the associated window.
    /// This is called by Update(float).
    /// </summary>
    void SetPerFrameImGuiData(float deltaSeconds)
    {
        ImGuiIOPtr io = ImGui.GetIO();
        io.DisplaySize = new Vector2(
            _windowWidth / _scaleFactor.X,
            _windowHeight / _scaleFactor.Y);
        io.DisplayFramebufferScale = _scaleFactor;
        io.DeltaTime = deltaSeconds; // DeltaTime is in seconds.
    }

    static bool TryMapKey(Key key, out ImGuiKey result)
    {
        ImGuiKey KeyToImGuiKeyShortcut(Key keyToConvert, Key startKey1, ImGuiKey startKey2)
        {
            int changeFromStart1 = (int)keyToConvert - (int)startKey1;
            return startKey2 + changeFromStart1;
        }

        switch (key)
        {
            case >= Key.F1 and <= Key.F12: result = KeyToImGuiKeyShortcut(key, Key.F1, ImGuiKey.F1); return true;
            case >= Key.Keypad0 and <= Key.Keypad9: result = KeyToImGuiKeyShortcut(key, Key.Keypad0, ImGuiKey.Keypad0); return true;
            case >= Key.A and <= Key.Z: result = KeyToImGuiKeyShortcut(key, Key.A, ImGuiKey.A); return true;
            case >= Key.Number0 and <= Key.Number9: result = KeyToImGuiKeyShortcut(key, Key.Number0, ImGuiKey._0); return true;
            case Key.ShiftLeft: case Key.ShiftRight: result = ImGuiKey.ModShift; return true;
            case Key.ControlLeft: case Key.ControlRight: result = ImGuiKey.ModCtrl; return true;
            case Key.AltLeft: case Key.AltRight: result = ImGuiKey.ModAlt; return true;
            case Key.WinLeft: case Key.WinRight: result = ImGuiKey.ModSuper; return true;
            case Key.Menu: result = ImGuiKey.Menu; return true;
            case Key.Up: result = ImGuiKey.UpArrow; return true;
            case Key.Down: result = ImGuiKey.DownArrow; return true;
            case Key.Left: result = ImGuiKey.LeftArrow; return true;
            case Key.Right: result = ImGuiKey.RightArrow; return true;
            case Key.Enter: result = ImGuiKey.Enter; return true;
            case Key.Escape: result = ImGuiKey.Escape; return true;
            case Key.Space: result = ImGuiKey.Space; return true;
            case Key.Tab: result = ImGuiKey.Tab; return true;
            case Key.BackSpace: result = ImGuiKey.Backspace; return true;
            case Key.Insert: result = ImGuiKey.Insert; return true;
            case Key.Delete: result = ImGuiKey.Delete; return true;
            case Key.PageUp: result = ImGuiKey.PageUp; return true;
            case Key.PageDown: result = ImGuiKey.PageDown; return true;
            case Key.Home: result = ImGuiKey.Home; return true;
            case Key.End: result = ImGuiKey.End; return true;

            case Key.CapsLock:
                result = ImGuiKey.Backspace; //ImGuiKey.CapsLock; // Colemak
                return true;

            case Key.ScrollLock: result = ImGuiKey.ScrollLock; return true;
            case Key.PrintScreen: result = ImGuiKey.PrintScreen; return true;
            case Key.Pause: result = ImGuiKey.Pause; return true;
            case Key.NumLock: result = ImGuiKey.NumLock; return true;
            case Key.KeypadDivide: result = ImGuiKey.KeypadDivide; return true;
            case Key.KeypadMultiply: result = ImGuiKey.KeypadMultiply; return true;
            case Key.KeypadSubtract: result = ImGuiKey.KeypadSubtract; return true;
            case Key.KeypadAdd: result = ImGuiKey.KeypadAdd; return true;
            case Key.KeypadDecimal: result = ImGuiKey.KeypadDecimal; return true;
            case Key.KeypadEnter: result = ImGuiKey.KeypadEnter; return true;
            case Key.Tilde: result = ImGuiKey.GraveAccent; return true;
            case Key.Minus: result = ImGuiKey.Minus; return true;
            case Key.Plus: result = ImGuiKey.Equal; return true;
            case Key.BracketLeft: result = ImGuiKey.LeftBracket; return true;
            case Key.BracketRight: result = ImGuiKey.RightBracket; return true;
            case Key.Semicolon: result = ImGuiKey.Semicolon; return true;
            case Key.Quote: result = ImGuiKey.Apostrophe; return true;
            case Key.Comma: result = ImGuiKey.Comma; return true;
            case Key.Period: result = ImGuiKey.Period; return true;
            case Key.Slash: result = ImGuiKey.Slash; return true;
            case Key.BackSlash: case Key.NonUSBackSlash: result = ImGuiKey.Backslash; return true;
            default: result = ImGuiKey.GamepadBack; return false;
        }
    }

    void UpdateImGuiInput(InputSnapshot snapshot)
    {
        ImGuiIOPtr io = ImGui.GetIO();
        io.AddMousePosEvent(snapshot.MousePosition.X, snapshot.MousePosition.Y);
        io.AddMouseButtonEvent(0, snapshot.IsMouseDown(MouseButton.Left));
        io.AddMouseButtonEvent(1, snapshot.IsMouseDown(MouseButton.Right));
        io.AddMouseButtonEvent(2, snapshot.IsMouseDown(MouseButton.Middle));
        io.AddMouseButtonEvent(3, snapshot.IsMouseDown(MouseButton.Button1));
        io.AddMouseButtonEvent(4, snapshot.IsMouseDown(MouseButton.Button2));
        io.AddMouseWheelEvent(0f, snapshot.WheelDelta);

        foreach (var c in snapshot.KeyCharPresses)
            io.AddInputCharacter(c);

        foreach (var keyEvent in snapshot.KeyEvents)
            if (TryMapKey(keyEvent.Key, out ImGuiKey imguikey))
                io.AddKeyEvent(imguikey, keyEvent.Down);
    }

    unsafe void RenderImDrawData(ImDrawDataPtr drawData, GraphicsDevice gd, CommandList cl)
    {
        uint vertexOffsetInVertices = 0;
        uint indexOffsetInElements = 0;

        if (drawData.CmdListsCount == 0)
            return;

        uint totalVbSize = (uint)(drawData.TotalVtxCount * sizeof(ImDrawVert));
        if (totalVbSize > _vertexBuffer.SizeInBytes)
        {
            _vertexBuffer.Dispose();
            _vertexBuffer = gd.ResourceFactory.CreateBuffer(new BufferDescription((uint)(totalVbSize * 1.5f), BufferUsage.VertexBuffer | BufferUsage.Dynamic));
            _vertexBuffer.Name = "ImGui.NET Vertex Buffer";
        }

        uint totalIbSize = (uint)(drawData.TotalIdxCount * sizeof(ushort));
        if (totalIbSize > _indexBuffer.SizeInBytes)
        {
            _indexBuffer.Dispose();
            _indexBuffer = gd.ResourceFactory.CreateBuffer(new BufferDescription((uint)(totalIbSize * 1.5f), BufferUsage.IndexBuffer | BufferUsage.Dynamic));
            _indexBuffer.Name = "ImGui.NET Index Buffer";
        }

        for (int i = 0; i < drawData.CmdListsCount; i++)
        {
            ImDrawListPtr cmdList = drawData.CmdListsRange[i];

            cl.UpdateBuffer(
                _vertexBuffer,
                vertexOffsetInVertices * (uint)sizeof(ImDrawVert),
                cmdList.VtxBuffer.Data,
                (uint)(cmdList.VtxBuffer.Size * sizeof(ImDrawVert)));

            cl.UpdateBuffer(
                _indexBuffer,
                indexOffsetInElements * sizeof(ushort),
                cmdList.IdxBuffer.Data,
                (uint)(cmdList.IdxBuffer.Size * sizeof(ushort)));

            vertexOffsetInVertices += (uint)cmdList.VtxBuffer.Size;
            indexOffsetInElements += (uint)cmdList.IdxBuffer.Size;
        }

        // Setup orthographic projection matrix into our constant buffer
        {
            var io = ImGui.GetIO();

            Matrix4x4 mvp = Matrix4x4.CreateOrthographicOffCenter(
                0f,
                io.DisplaySize.X,
                io.DisplaySize.Y,
                0.0f,
                -1.0f,
                1.0f);

            _gd.UpdateBuffer(_projMatrixBuffer, 0, ref mvp);
        }

        cl.SetVertexBuffer(0, _vertexBuffer);
        cl.SetIndexBuffer(_indexBuffer, IndexFormat.UInt16);
        cl.SetPipeline(_pipeline);
        cl.SetGraphicsResourceSet(0, _mainResourceSet);

        drawData.ScaleClipRects(ImGui.GetIO().DisplayFramebufferScale);

        // Render command lists
        int vtxOffset = 0;
        int idxOffset = 0;
        for (int n = 0; n < drawData.CmdListsCount; n++)
        {
            ImDrawListPtr cmdList = drawData.CmdListsRange[n];
            for (int i = 0; i < cmdList.CmdBuffer.Size; i++)
            {
                ImDrawCmdPtr pcmd = cmdList.CmdBuffer[i];
                if (pcmd.UserCallback != IntPtr.Zero)
                    throw new NotImplementedException();

                if (pcmd.TextureId != IntPtr.Zero)
                {
                    cl.SetGraphicsResourceSet(1,
                        pcmd.TextureId == _fontAtlasId
                            ? _fontTextureResourceSet
                            : GetImageResourceSet(pcmd.TextureId));
                }

                cl.SetScissorRect(
                    0,
                    (uint)pcmd.ClipRect.X,
                    (uint)pcmd.ClipRect.Y,
                    (uint)(pcmd.ClipRect.Z - pcmd.ClipRect.X),
                    (uint)(pcmd.ClipRect.W - pcmd.ClipRect.Y));

                cl.DrawIndexed(pcmd.ElemCount, 1, pcmd.IdxOffset + (uint)idxOffset, (int)(pcmd.VtxOffset + vtxOffset), 0);
            }

            idxOffset += cmdList.IdxBuffer.Size;
            vtxOffset += cmdList.VtxBuffer.Size;
        }
    }

    /// <summary>
    /// Frees all graphics resources used by the renderer.
    /// </summary>
    public void Dispose()
    {
        _vertexBuffer.Dispose();
        _indexBuffer.Dispose();
        _projMatrixBuffer.Dispose();
        _fontTexture?.Dispose();
        _vertexShader.Dispose();
        _fragmentShader.Dispose();
        _layout.Dispose();
        _textureLayout.Dispose();
        _pipeline.Dispose();
        _mainResourceSet.Dispose();
        _fontTextureResourceSet?.Dispose();

        foreach (IDisposable resource in _ownedResources)
        {
            resource.Dispose();
        }
    }
}