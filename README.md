# NativeLibraryEmbedExample

本机动态库嵌入为资源文件示例; Example of native library embed as resource; 

应该有更简单的方法，但还没有深入探究；

## 场景
.net增加aot发布后，第三方包引用的已编译本机库不会嵌入到可执行文件中，导致发布后需要分发多个文件，此示例演示一种将本机库嵌入可执行文件作为资源的办法；

## 实现步骤

### 1. 确定本机库路径，将其嵌入为资源文件

在 `csproj` 中添加嵌入资源的操作，示例为 `sqlite` 本机库

```xml
  <!-- 仅当启用 aot 发布，且编译条件为 Release ，且指定运行时时进行嵌入 -->
  <ItemGroup Condition="'$(PublishAot)' == 'true' and '$(Configuration)' == 'Release' and '$(RuntimeIdentifier)' != ''">
    <!-- 嵌入 obj/embed_depends_files 目录下的所有文件 -->
    <EmbeddedResource Include="$(IntermediateOutputPath)embed_depends_files\**\*" Link="\%(RecursiveDir)%(FileName)%(Extension)">
      <CopyToOutputDirectory>Never</CopyToOutputDirectory>
    </EmbeddedResource>
  </ItemGroup>

  <!-- 仅当启用 aot 发布，且编译条件为 Release ，且指定运行时时在构建中复制目标文件到 obj 目录下 -->
  <Target Name="__CopyEmbedDependsFilesToIntermediateOutput"
      AfterTargets="CopyFilesToOutputDirectory"
      Condition="'$(PublishAot)' == 'true' and '$(Configuration)' == 'Release' and '$(RuntimeIdentifier)' != ''">
    <ItemGroup>
      <!-- 复制输出目录的 e_sqlite3.* 和 libe_sqlite3.* 等文件，此值需要确认要嵌入的库可能的名称（windows、linux等不同的系统名称不同） -->
      <EmbedDependsFiles Include="$(OutputPath)e_sqlite3.*;$(OutputPath)libe_sqlite3.*" />
    </ItemGroup>
    <!-- 复制文件到 obj/embed_depends_files 目录下 -->
    <Copy SourceFiles="@(EmbedDependsFiles)" DestinationFolder="$(IntermediateOutputPath)embed_depends_files\" SkipUnchangedFiles="true" />
  </Target>
```

### 2. 配置 `AssemblyLoadContext.Default.ResolvingUnmanagedDll` 解析方法

在 `AssemblyLoadContext.Default.ResolvingUnmanagedDl` 中将嵌入的资源文件复制到物理路径，然后进行加载，示例为 `sqlite` 本机库

```C#
AssemblyLoadContext.Default.ResolvingUnmanagedDll += (Assembly _, string libraryName) =>
{
    //当库名称不为 e_sqlite3 时不进行处理
    if (!libraryName.Equals("e_sqlite3"))
    {
        return default;
    }
    var assembly = Assembly.GetExecutingAssembly();

    //获取 sqlite 本机库的嵌入资源名称
    var resourceName = assembly.GetManifestResourceNames().Single(str => str.Contains("e_sqlite3"));

    //读取资源
    using var stream = assembly.GetManifestResourceStream(resourceName);

    if (stream is null)
    {
        return default;
    }

    //构造临时路径
    var sqliteLibraryPath = Path.Combine(Path.GetTempPath(), Path.GetFileName(resourceName));
    if (!File.Exists(sqliteLibraryPath))
    {
        //将本机库复制到临时路径
        using var file = File.OpenWrite(sqliteLibraryPath);
        stream.CopyTo(file);
    }

    //进行加载
    return NativeLibrary.Load(sqliteLibraryPath);
};
```

## 注意：由于文件需要构建后才能得到，构建过程需要先进行两次非增量 `build` ，确保本机库已复制到 obj 目录进行嵌入，再执行 `publish` 命令才能正确使用嵌入资源

示例:
```shell
dotnet build -c Release -r win-x64 --no-incremental
dotnet build -c Release -r win-x64 --no-incremental
dotnet publish -c Release -r win-x64
```

## 示例代码中有帮助类 `EmbeddedUnmanagedDllResolver` 可直接使用