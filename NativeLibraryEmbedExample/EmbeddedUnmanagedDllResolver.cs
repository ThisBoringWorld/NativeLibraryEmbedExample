using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;

namespace NativeLibraryEmbedExample;

internal delegate bool LibraryResolvePredicateDelegate(string libraryName);

internal record struct LibrarySelectDescriptor(LibraryResolvePredicateDelegate LibraryResolvePredicate, Func<string, bool> ResourceSelector);

internal class EmbeddedUnmanagedDllResolver
{
    private readonly List<LibrarySelectDescriptor> _librarySelectDescriptors = [];

    private readonly string? _libraryUniqueTag;

    private readonly string _localUnpackPath;

    private readonly Assembly _resourceAssembly;

    public EmbeddedUnmanagedDllResolver() : this(Assembly.GetExecutingAssembly())
    {
    }

    public EmbeddedUnmanagedDllResolver(Assembly resourceAssembly, string localUnpackPath, string? libraryUniqueTag = null)
    {
        _resourceAssembly = resourceAssembly ?? throw new ArgumentNullException(nameof(resourceAssembly));
        _localUnpackPath = localUnpackPath ?? throw new ArgumentNullException(nameof(localUnpackPath));
        _libraryUniqueTag = libraryUniqueTag;
    }

    public EmbeddedUnmanagedDllResolver(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        var assemblyName = assembly.GetName();
        var programName = assemblyName.Name
                          ?? Path.GetFileNameWithoutExtension(Environment.ProcessPath)
                          ?? Guid.NewGuid().ToString("n");
        _resourceAssembly = assembly;
        _localUnpackPath = Path.Combine(Path.GetTempPath(), programName, "libcache");
        _libraryUniqueTag = assemblyName.Version?.ToString();
    }

    public static EmbeddedUnmanagedDllResolver Default() => new();

    public EmbeddedUnmanagedDllResolver Add(string libraryName, Func<string, bool> resourceSelector) => Add(new(checkLibraryName => string.Equals(libraryName, checkLibraryName), resourceSelector));

    public EmbeddedUnmanagedDllResolver Add(LibrarySelectDescriptor librarySelectDescriptor)
    {
        _librarySelectDescriptors.Add(librarySelectDescriptor);
        return this;
    }

    public void Resolving(AssemblyLoadContext context)
    {
        var librarySelectDescriptors = _librarySelectDescriptors.ToArray();
        var resourceAssembly = _resourceAssembly;

        var localUnpackPath = _localUnpackPath;
        var libraryUniqueTag = _libraryUniqueTag;

        Func<Assembly, string, nint> resolvingUnmanagedDllFunc = (_, libraryName) =>
        {
            string[]? resourceNames = null;

            //遍历
            foreach (var librarySelectDescriptor in librarySelectDescriptors)
            {
                //是否可处理
                if (!librarySelectDescriptor.LibraryResolvePredicate(libraryName))
                {
                    continue;
                }
                //获取所有资源名
                resourceNames ??= resourceAssembly.GetManifestResourceNames();

                //查找可用资源
                var resourceName = resourceNames.FirstOrDefault(librarySelectDescriptor.ResourceSelector);
                if (string.IsNullOrEmpty(resourceName))
                {
                    continue;
                }

                //获取资源数据
                using var stream = resourceAssembly.GetManifestResourceStream(resourceName);

                if (stream is null)
                {
                    continue;
                }

                //构造临时的库存放路径
                var libraryPath = Path.Combine(localUnpackPath, $"{libraryUniqueTag}_{Path.GetFileName(resourceName)}");

                //没有唯一标识，避免更新带来的影响，尝试删除旧文件
                if (libraryUniqueTag is null)
                {
                    try
                    {
                        if (File.Exists(libraryPath))
                        {
                            File.Delete(libraryPath);
                        }
                    }
                    catch { }
                }

                try
                {
                    try
                    {
                        //检查目录
                        if (!Directory.Exists(localUnpackPath))
                        {
                            Directory.CreateDirectory(localUnpackPath);
                        }
                    }
                    catch { }

                    //复制文件
                    if (!File.Exists(libraryPath))
                    {
                        using var file = File.OpenWrite(libraryPath);
                        stream.CopyTo(file);
                    }
                }
                catch { }

                //加载数据
                return NativeLibrary.Load(libraryPath);
            }

            return default;
        };

        context.ResolvingUnmanagedDll += resolvingUnmanagedDllFunc;
    }

    public void ResolvingDefault() => Resolving(AssemblyLoadContext.Default);
}
