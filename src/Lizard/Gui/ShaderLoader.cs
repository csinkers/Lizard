using System.Reflection;
using Veldrid;

namespace Lizard.Gui;

static class ShaderLoader
{
    public static byte[] LoadEmbeddedShaderCode(Assembly assembly, ResourceFactory factory, string name)
    {
        string resourceName = factory.BackendType switch
        {
            GraphicsBackend.Direct3D11 => name + ".hlsl.bytes",
            GraphicsBackend.OpenGL => name + ".glsl",
            GraphicsBackend.OpenGLES => name + ".glsles",
            GraphicsBackend.Vulkan => name + ".spv",
            GraphicsBackend.Metal => name + ".metallib",
            _ => throw new NotImplementedException()
        };
        return GetEmbeddedResourceBytes(assembly, resourceName);
    }

    static string GetEmbeddedResourceText(Assembly assembly, string resourceName)
    {
        var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
            throw new InvalidOperationException($"Could not load resource stream \"{resourceName}\" from assembly \"{assembly.FullName}\"");

        using var sr = new StreamReader(stream);
        return sr.ReadToEnd();
    }

    static byte[] GetEmbeddedResourceBytes(Assembly assembly, string resourceName)
    {
        string prefix = $"{assembly.GetName().Name}.shaders.";
        var name = prefix + resourceName;
        using Stream? s = assembly.GetManifestResourceStream(name);

        if (s == null)
        {
            var valid = string.Join(", ", assembly.GetManifestResourceNames());
            throw new FileNotFoundException($"Could not load embedded resource stream \"{name}\". Valid names: {valid}");
        }

        byte[] ret = new byte[s.Length];
        _ = s.Read(ret, 0, (int)s.Length);
        return ret;
    }
}