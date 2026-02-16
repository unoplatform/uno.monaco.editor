using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace WebView2FlickerSpike;

/// <summary>
/// Manual COM interface definitions for DirectComposition.
/// CsWin32 generates P/Invoke for DCompositionCreateDevice2, but the COM interfaces
/// for IDCompositionDevice, IDCompositionVisual, IDCompositionVirtualSurface etc.
/// require manual definitions due to output parameter typing issues in CsWin32.
/// </summary>
internal static class DCompInterop
{
    public static readonly Guid IID_IDCompositionDevice = new("C37EA93A-E7AA-450D-B16F-9746CB0407F3");
    public static readonly Guid IID_IDCompositionTarget = new("EACDD04C-117E-4E17-88F4-D1B12B0E13A1");
    public static readonly Guid IID_IDCompositionVisual = new("4D93059D-097B-4651-9A60-F0F25116E2F3");
    public static readonly Guid IID_IDCompositionSurface = new("BB8A4953-2C99-4F5A-96F5-4819027FA3AC");
    public static readonly Guid IID_IDCompositionVirtualSurface = new("AE471C51-5F53-4A24-8D3E-D0C39C30B3F0");

    public static readonly Guid IID_ID3D11Texture2D = new("6f15aaf2-d208-4e89-9ab4-489535d34f9c");
    public static readonly Guid IID_IDXGIDevice = new("54ec77fa-1377-44e6-8c32-88fd5f44c84c");

    [DllImport("dcomp.dll", PreserveSig = false)]
    public static extern void DCompositionCreateDevice2(
        IntPtr renderingDevice,
        [MarshalAs(UnmanagedType.LPStruct)] Guid iid,
        [MarshalAs(UnmanagedType.IUnknown)] out object dcompositionDevice);

    /// <summary>
    /// DXGI_FORMAT values used for DComp surface creation.
    /// </summary>
    public enum DXGI_FORMAT : uint
    {
        DXGI_FORMAT_B8G8R8A8_UNORM = 87,
    }

    /// <summary>
    /// DXGI_ALPHA_MODE values used for DComp surface creation.
    /// </summary>
    public enum DXGI_ALPHA_MODE : uint
    {
        DXGI_ALPHA_MODE_PREMULTIPLIED = 1,
    }
}

/// <summary>
/// IDCompositionDevice COM interface.
/// </summary>
[ComImport]
[Guid("C37EA93A-E7AA-450D-B16F-9746CB0407F3")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IDCompositionDevice
{
    void Commit();
    void WaitForCommitCompletion();

    void GetFrameStatistics(out IntPtr statistics);

    void CreateTargetForHwnd(
        IntPtr hwnd,
        [MarshalAs(UnmanagedType.Bool)] bool topmost,
        out IDCompositionTarget target);

    void CreateVisual(out IDCompositionVisual visual);

    void CreateSurface(
        uint width,
        uint height,
        DCompInterop.DXGI_FORMAT pixelFormat,
        DCompInterop.DXGI_ALPHA_MODE alphaMode,
        out IDCompositionSurface surface);

    void CreateVirtualSurface(
        uint initialWidth,
        uint initialHeight,
        DCompInterop.DXGI_FORMAT pixelFormat,
        DCompInterop.DXGI_ALPHA_MODE alphaMode,
        out IDCompositionVirtualSurface virtualSurface);

    // Remaining methods stubbed as needed
    void CreateSurfaceFromHandle(IntPtr handle, [MarshalAs(UnmanagedType.IUnknown)] out object surface);
    void CreateSurfaceFromHwnd(IntPtr hwnd, [MarshalAs(UnmanagedType.IUnknown)] out object surface);
    void CreateTranslateTransform([MarshalAs(UnmanagedType.IUnknown)] out object translateTransform);
    void CreateScaleTransform([MarshalAs(UnmanagedType.IUnknown)] out object scaleTransform);
    void CreateRotateTransform([MarshalAs(UnmanagedType.IUnknown)] out object rotateTransform);
    void CreateSkewTransform([MarshalAs(UnmanagedType.IUnknown)] out object skewTransform);
    void CreateMatrixTransform([MarshalAs(UnmanagedType.IUnknown)] out object matrixTransform);
    void CreateTransformGroup([MarshalAs(UnmanagedType.IUnknown)] object transforms, uint elements, [MarshalAs(UnmanagedType.IUnknown)] out object transformGroup);
    void CreateTranslateTransform3D([MarshalAs(UnmanagedType.IUnknown)] out object translateTransform3D);
    void CreateScaleTransform3D([MarshalAs(UnmanagedType.IUnknown)] out object scaleTransform3D);
    void CreateRotateTransform3D([MarshalAs(UnmanagedType.IUnknown)] out object rotateTransform3D);
    void CreateMatrixTransform3D([MarshalAs(UnmanagedType.IUnknown)] out object matrixTransform3D);
    void CreateTransform3DGroup([MarshalAs(UnmanagedType.IUnknown)] object transforms3D, uint elements, [MarshalAs(UnmanagedType.IUnknown)] out object transform3DGroup);
    void CreateEffectGroup([MarshalAs(UnmanagedType.IUnknown)] out object effectGroup);
    void CreateRectangleClip([MarshalAs(UnmanagedType.IUnknown)] out object clip);
    void CreateAnimation([MarshalAs(UnmanagedType.IUnknown)] out object animation);
    void CheckDeviceState([MarshalAs(UnmanagedType.Bool)] out bool pfValid);
}

/// <summary>
/// IDCompositionTarget COM interface.
/// </summary>
[ComImport]
[Guid("EACDD04C-117E-4E17-88F4-D1B12B0E13A1")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IDCompositionTarget
{
    void SetRoot(IDCompositionVisual? visual);
}

/// <summary>
/// IDCompositionVisual COM interface.
/// </summary>
[ComImport]
[Guid("4D93059D-097B-4651-9A60-F0F25116E2F3")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IDCompositionVisual
{
    void SetOffsetX_Float(float offsetX);
    void SetOffsetX_Animation([MarshalAs(UnmanagedType.IUnknown)] object animation);
    void SetOffsetY_Float(float offsetY);
    void SetOffsetY_Animation([MarshalAs(UnmanagedType.IUnknown)] object animation);
    void SetTransform_Matrix([In] ref DCOMP_MATRIX_3X2 matrix);
    void SetTransform_Object([MarshalAs(UnmanagedType.IUnknown)] object transform);
    void SetTransformParent(IDCompositionVisual? visual);
    void SetEffect([MarshalAs(UnmanagedType.IUnknown)] object? effect);
    void SetBitmapInterpolationMode(int interpolationMode);
    void SetBorderMode(int borderMode);
    void SetClip_Rect([In] ref RECT rect);
    void SetClip_Object([MarshalAs(UnmanagedType.IUnknown)] object? clip);
    void SetContent([MarshalAs(UnmanagedType.IUnknown)] object? content);

    void AddVisual(
        IDCompositionVisual visual,
        [MarshalAs(UnmanagedType.Bool)] bool insertAbove,
        IDCompositionVisual? referenceVisual);

    void RemoveVisual(IDCompositionVisual visual);
    void RemoveAllVisuals();

    void SetCompositeMode(int compositeMode);
}

/// <summary>
/// IDCompositionSurface COM interface.
/// </summary>
[ComImport]
[Guid("BB8A4953-2C99-4F5A-96F5-4819027FA3AC")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IDCompositionSurface
{
    void BeginDraw(
        [In] ref RECT updateRect,
        [MarshalAs(UnmanagedType.LPStruct)] Guid iid,
        [MarshalAs(UnmanagedType.IUnknown)] out object updateObject,
        out POINT updateOffset);

    void EndDraw();
    void SuspendDraw();
    void ResumeDraw();
    void Scroll(
        [In] ref RECT scrollRect,
        [In] ref RECT clipRect,
        int offsetX,
        int offsetY);
}

/// <summary>
/// IDCompositionVirtualSurface COM interface - extends IDCompositionSurface.
/// </summary>
[ComImport]
[Guid("AE471C51-5F53-4A24-8D3E-D0C39C30B3F0")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IDCompositionVirtualSurface
{
    // IDCompositionSurface methods (inherited)
    void BeginDraw(
        [In] ref RECT updateRect,
        [MarshalAs(UnmanagedType.LPStruct)] Guid iid,
        [MarshalAs(UnmanagedType.IUnknown)] out object updateObject,
        out POINT updateOffset);

    void EndDraw();
    void SuspendDraw();
    void ResumeDraw();
    void Scroll(
        [In] ref RECT scrollRect,
        [In] ref RECT clipRect,
        int offsetX,
        int offsetY);

    // IDCompositionVirtualSurface methods
    void Resize(uint width, uint height);
    void Trim([In] RECT[]? rectangles, uint count);
}

/// <summary>
/// Simple RECT structure for DComp interop.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct RECT
{
    public int Left;
    public int Top;
    public int Right;
    public int Bottom;

    public RECT(int left, int top, int right, int bottom)
    {
        Left = left;
        Top = top;
        Right = right;
        Bottom = bottom;
    }
}

/// <summary>
/// Simple POINT structure for DComp interop.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct POINT
{
    public int X;
    public int Y;
}

/// <summary>
/// 3x2 matrix for DComp visual transforms.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct DCOMP_MATRIX_3X2
{
    public float M11, M12;
    public float M21, M22;
    public float M31, M32;
}
