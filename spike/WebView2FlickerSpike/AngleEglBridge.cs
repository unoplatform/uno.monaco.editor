using System.Runtime.InteropServices;

namespace WebView2FlickerSpike;

/// <summary>
/// P/Invoke bindings for ANGLE's EGL API.
/// ANGLE provides OpenGL ES on top of D3D11, enabling Skia to render into
/// DirectComposition surfaces via the D3D11 texture bridge.
///
/// Binary sources:
/// - Primary: Microsoft.AspNetCore.Components.WebView NuGet (Blazor Hybrid ANGLE)
/// - Fallback: Chrome installation directory
/// </summary>
internal static class AngleEglBridge
{
    private const string LibEgl = "libEGL";
    private const string LibGlesV2 = "libGLESv2";

    // EGL constants
    public const int EGL_SUCCESS = 0x3000;
    public const int EGL_DEFAULT_DISPLAY = 0;
    public const int EGL_NONE = 0x3038;
    public const int EGL_NO_DISPLAY = 0;
    public const int EGL_NO_CONTEXT = 0;
    public const int EGL_NO_SURFACE = 0;
    public const int EGL_TRUE = 1;
    public const int EGL_FALSE = 0;

    // EGL config attributes
    public const int EGL_BUFFER_SIZE = 0x3020;
    public const int EGL_ALPHA_SIZE = 0x3021;
    public const int EGL_BLUE_SIZE = 0x3022;
    public const int EGL_GREEN_SIZE = 0x3023;
    public const int EGL_RED_SIZE = 0x3024;
    public const int EGL_DEPTH_SIZE = 0x3025;
    public const int EGL_STENCIL_SIZE = 0x3026;
    public const int EGL_SURFACE_TYPE = 0x3033;
    public const int EGL_RENDERABLE_TYPE = 0x3040;
    public const int EGL_CONFORMANT = 0x3042;
    public const int EGL_OPENGL_ES2_BIT = 0x0004;
    public const int EGL_OPENGL_ES3_BIT = 0x0040;
    public const int EGL_PBUFFER_BIT = 0x0001;
    public const int EGL_WINDOW_BIT = 0x0004;

    // EGL context attributes
    public const int EGL_CONTEXT_CLIENT_VERSION = 0x3098;
    public const int EGL_CONTEXT_MAJOR_VERSION = 0x3098;
    public const int EGL_CONTEXT_MINOR_VERSION = 0x30FB;

    // EGL surface attributes
    public const int EGL_WIDTH = 0x3057;
    public const int EGL_HEIGHT = 0x3058;
    public const int EGL_TEXTURE_FORMAT = 0x3080;
    public const int EGL_TEXTURE_TARGET = 0x3081;
    public const int EGL_TEXTURE_2D = 0x305F;
    public const int EGL_TEXTURE_RGBA = 0x305E;

    // ANGLE platform extensions
    public const int EGL_PLATFORM_ANGLE_ANGLE = 0x3202;
    public const int EGL_PLATFORM_ANGLE_TYPE_ANGLE = 0x3203;
    public const int EGL_PLATFORM_ANGLE_TYPE_D3D11_ANGLE = 0x3208;
    public const int EGL_PLATFORM_ANGLE_DEVICE_TYPE_ANGLE = 0x3209;
    public const int EGL_PLATFORM_ANGLE_DEVICE_TYPE_HARDWARE_ANGLE = 0x320A;

    // ANGLE D3D texture interop
    public const int EGL_D3D_TEXTURE_ANGLE = 0x33A3;
    public const int EGL_D3D_TEXTURE_2D_SHARE_HANDLE_ANGLE = 0x3200;

    // EGL API
    [DllImport(LibEgl, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr eglGetPlatformDisplayEXT(int platform, IntPtr native_display, int[] attrib_list);

    [DllImport(LibEgl, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool eglInitialize(IntPtr display, out int major, out int minor);

    [DllImport(LibEgl, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool eglChooseConfig(IntPtr display, int[] attrib_list, IntPtr[] configs, int config_size, out int num_config);

    [DllImport(LibEgl, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr eglCreateContext(IntPtr display, IntPtr config, IntPtr share_context, int[] attrib_list);

    [DllImport(LibEgl, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool eglMakeCurrent(IntPtr display, IntPtr draw, IntPtr read, IntPtr context);

    [DllImport(LibEgl, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool eglDestroyContext(IntPtr display, IntPtr context);

    [DllImport(LibEgl, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool eglDestroySurface(IntPtr display, IntPtr surface);

    [DllImport(LibEgl, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool eglTerminate(IntPtr display);

    [DllImport(LibEgl, CallingConvention = CallingConvention.Cdecl)]
    public static extern int eglGetError();

    [DllImport(LibEgl, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool eglSwapBuffers(IntPtr display, IntPtr surface);

    [DllImport(LibEgl, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr eglCreatePbufferFromClientBuffer(
        IntPtr display,
        int buftype,
        IntPtr buffer,
        IntPtr config,
        int[] attrib_list);

    [DllImport(LibEgl, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr eglCreatePbufferSurface(IntPtr display, IntPtr config, int[] attrib_list);

    [DllImport(LibEgl, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr eglGetProcAddress([MarshalAs(UnmanagedType.LPStr)] string procname);

    [DllImport(LibEgl, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool eglBindAPI(int api);

    public const int EGL_OPENGL_ES_API = 0x30A0;

    // OpenGL ES API (via libGLESv2)
    [DllImport(LibGlesV2, CallingConvention = CallingConvention.Cdecl)]
    public static extern void glViewport(int x, int y, int width, int height);

    [DllImport(LibGlesV2, CallingConvention = CallingConvention.Cdecl)]
    public static extern void glClearColor(float red, float green, float blue, float alpha);

    [DllImport(LibGlesV2, CallingConvention = CallingConvention.Cdecl)]
    public static extern void glClear(uint mask);

    [DllImport(LibGlesV2, CallingConvention = CallingConvention.Cdecl)]
    public static extern void glFlush();

    [DllImport(LibGlesV2, CallingConvention = CallingConvention.Cdecl)]
    public static extern void glFinish();

    [DllImport(LibGlesV2, CallingConvention = CallingConvention.Cdecl)]
    public static extern int glGetError();

    [DllImport(LibGlesV2, CallingConvention = CallingConvention.Cdecl)]
    public static extern void glGetIntegerv(int pname, out int data);

    [DllImport(LibGlesV2, CallingConvention = CallingConvention.Cdecl)]
    public static extern void glBindFramebuffer(int target, int framebuffer);

    public const int GL_COLOR_BUFFER_BIT = 0x00004000;
    public const int GL_FRAMEBUFFER = 0x8D40;
    public const int GL_FRAMEBUFFER_BINDING = 0x8CA6;
    public const int GL_RENDERBUFFER_BINDING = 0x8CA7;
    public const int GL_STENCIL_BUFFER_BIT = 0x00000400;
    public const int GL_DEPTH_BUFFER_BIT = 0x00000100;

    /// <summary>
    /// Initializes ANGLE EGL with D3D11 backend, using the provided D3D11 device pointer.
    /// Returns (display, config, context) tuple on success.
    /// </summary>
    public static (IntPtr display, IntPtr config, IntPtr context) InitializeAngle(IntPtr d3d11Device)
    {
        // Get EGL display backed by the D3D11 device (via ANGLE)
        int[] displayAttribs =
        [
            EGL_PLATFORM_ANGLE_TYPE_ANGLE, EGL_PLATFORM_ANGLE_TYPE_D3D11_ANGLE,
            EGL_PLATFORM_ANGLE_DEVICE_TYPE_ANGLE, EGL_PLATFORM_ANGLE_DEVICE_TYPE_HARDWARE_ANGLE,
            EGL_NONE
        ];

        var display = eglGetPlatformDisplayEXT(EGL_PLATFORM_ANGLE_ANGLE, d3d11Device, displayAttribs);
        if (display == IntPtr.Zero)
        {
            throw new InvalidOperationException($"eglGetPlatformDisplayEXT failed: 0x{eglGetError():X}");
        }

        if (!eglInitialize(display, out int major, out int minor))
        {
            throw new InvalidOperationException($"eglInitialize failed: 0x{eglGetError():X}");
        }

        Console.WriteLine($"[ANGLE] EGL initialized: {major}.{minor}");

        // Bind OpenGL ES API
        eglBindAPI(EGL_OPENGL_ES_API);

        // Choose EGL config
        int[] configAttribs =
        [
            EGL_RED_SIZE, 8,
            EGL_GREEN_SIZE, 8,
            EGL_BLUE_SIZE, 8,
            EGL_ALPHA_SIZE, 8,
            EGL_DEPTH_SIZE, 0,
            EGL_STENCIL_SIZE, 8,
            EGL_RENDERABLE_TYPE, EGL_OPENGL_ES2_BIT | EGL_OPENGL_ES3_BIT,
            EGL_SURFACE_TYPE, EGL_PBUFFER_BIT,
            EGL_NONE
        ];

        var configs = new IntPtr[1];
        if (!eglChooseConfig(display, configAttribs, configs, 1, out int numConfig) || numConfig == 0)
        {
            throw new InvalidOperationException($"eglChooseConfig failed: 0x{eglGetError():X}");
        }

        var config = configs[0];

        // Create EGL context (OpenGL ES 3.0)
        int[] contextAttribs =
        [
            EGL_CONTEXT_MAJOR_VERSION, 3,
            EGL_CONTEXT_MINOR_VERSION, 0,
            EGL_NONE
        ];

        var context = eglCreateContext(display, config, IntPtr.Zero, contextAttribs);
        if (context == IntPtr.Zero)
        {
            throw new InvalidOperationException($"eglCreateContext failed: 0x{eglGetError():X}");
        }

        Console.WriteLine("[ANGLE] EGL context created (OpenGL ES 3.0)");
        return (display, config, context);
    }

    /// <summary>
    /// Creates an EGL pbuffer surface wrapping a D3D11 texture (from DComp BeginDraw).
    /// The texture is transient - valid only between BeginDraw/EndDraw.
    /// </summary>
    public static IntPtr CreateSurfaceFromD3DTexture(IntPtr display, IntPtr config, IntPtr d3dTexture, int width, int height)
    {
        int[] surfaceAttribs =
        [
            EGL_WIDTH, width,
            EGL_HEIGHT, height,
            EGL_TEXTURE_TARGET, EGL_TEXTURE_2D,
            EGL_TEXTURE_FORMAT, EGL_TEXTURE_RGBA,
            EGL_NONE
        ];

        var surface = eglCreatePbufferFromClientBuffer(
            display,
            EGL_D3D_TEXTURE_ANGLE,
            d3dTexture,
            config,
            surfaceAttribs);

        if (surface == IntPtr.Zero)
        {
            throw new InvalidOperationException(
                $"eglCreatePbufferFromClientBuffer failed: 0x{eglGetError():X}. " +
                "Ensure the D3D11 texture is from the same device backing the ANGLE EGL display.");
        }

        return surface;
    }
}
