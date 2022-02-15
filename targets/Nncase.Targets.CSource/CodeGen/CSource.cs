using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Nncase.IR;
using Nncase.Schedule;
using Nncase.TIR;

namespace Nncase.CodeGen;

/// <summary>
/// the c source runtime function.
/// </summary>
/// <param name="name"></param>
/// <param name="handle"></param>
public record CSourceRTFunction(string name, Delegate handle) : IRTFunction
{
    public string Name { get => name; set { } }
    public Delegate Handle { get => handle; set { } }
}

public class CSourceSerializeResult : ISerializeResult
{

}

/// <summary>
/// c runtime module impl
/// </summary>
public class CSourceRTModel : IRTModule, IRTModel
{
    /// <inheritdoc/>
    public ModuleType ModuleType { get => CodeGen.ModuleType.Create("CSource"); set { } }

    /// <inheritdoc/>
    public ITarget Target { get; set; }

    /// <inheritdoc/>
    public IReadOnlyList<IRTModule> Modules => throw new NotImplementedException();

    /// <inheritdoc/>
    public string SourcePath { get; private set; }

    IRModule _MainModule;
    IRTFunction? _entry = null;

    /// <inheritdoc/>
    public bool IsSerialized { get; private set; }

    readonly List<IRTFunction> _functions = new();

    /// <summary>
    /// <see cref="CSourceRTModel"/>
    /// </summary>
    public CSourceRTModel(Schedule.SchedModelResult result, ITarget target)
    {
        SourcePath = CodeGenUtil.GetTempFileName("c");
        _MainModule = result.ParentModule;
        Target = target;
    }

    /// <inheritdoc/>
    public byte[] Source { get => File.ReadAllBytes(SourcePath); set { } }

    /// <inheritdoc/>
    public string SourceExt { get => "c"; set { } }

    /// <inheritdoc/>
    public IRTFunction? Entry => _entry;

    /// <inheritdoc/>
    public IReadOnlyList<IRTFunction> Functions => _functions;

    public SchedModelResult modelResult { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

    /// <inheritdoc/>
    string _dllPath = "";

    /// <summary>
    /// write the c source code into source path.
    /// </summary>
    /// <exception cref="InvalidProgramException"></exception>
    void BuildCode()
    {
        if (File.Exists(SourcePath))
            File.Delete(SourcePath);
        using (var writer = new StreamWriter(SourcePath, false, Encoding.UTF8))
        {
            var visior = new CSourceHostBuildVisior(writer);
            if (_MainModule.Entry is null) { throw new InvalidProgramException("The Model Entry Is Null!"); }
            if (_MainModule.Entry.CheckedType is null && _MainModule.Entry.InferenceType() == false) { throw new InvalidProgramException("The Model Entry Can't Inference Type!"); }
            visior.Visit(_MainModule.Entry);
        }
    }

    public void CompileCode()
    {
        if (!File.Exists(SourcePath))
            throw new InvalidProgramException("The Source Code Path Is Invalid!");
        var compiler = new CSourceCompiler();
        _dllPath = compiler.Compile(SourcePath);
    }

    /// <summary>
    /// bind each IR.Funtion with C function
    /// </summary>
    /// <exception cref="InvalidProgramException"></exception>
    public void ExportCode()
    {
        if (!File.Exists(_dllPath))
            throw new InvalidProgramException("The DLL Path Is Invalid!");
        var dllPtr = NativeLibrary.Load(_dllPath);
        foreach (var f in _MainModule.Functions)
        {
            var funcType = f.ToDelegateType(Path.GetFileName(_dllPath));
            NativeLibrary.GetExport(dllPtr, f.Name);
            var funPtr = NativeLibrary.GetExport(dllPtr, f.Name);
            _functions.Add(new CSourceRTFunction(f.Name, funPtr.BindDelegate(funcType)));
            if (f == _MainModule.Entry) { _entry = _functions.Last(); }
        }
    }

    /// <inheritdoc/>
    public ISerializeResult Serialize()
    {
        if (IsSerialized) { return new CSourceSerializeResult(); }
        BuildCode();
        CompileCode();
        ExportCode();
        return new CSourceSerializeResult();
    }

    /// <summary>
    /// invoke the module entry
    /// </summary>
    /// <param name="args">input args</param>
    /// <returns> results </returns>
    /// <exception cref="InvalidOperationException"></exception>
    public object? Invoke(params object?[]? args)
    {
        if (Entry is null)
            throw new InvalidOperationException("This RTModule Have No Entry Function!");
        return Entry.Handle.DynamicInvoke(args);
    }

    public void Dump(string name, string DumpDirPath)
    {
        using var file = File.Open($"{DumpDirPath}/{name}.{SourceExt}", FileMode.OpenOrCreate, FileAccess.Write);
        using var writer = new StreamWriter(file);
        writer.Write(Source);
    }

    string IRTModel.Dump(string name, string dumpDirPath)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// the csource code compiler.
/// </summary>
public class CSourceCompiler
{
    /// <summary>
    /// compiler exe name
    /// </summary>
    string _exe = "", _arch = "", _ext = "";

    /// <summary>
    /// select current pattern's exe
    /// </summary>
    /// <exception cref="NotSupportedException"></exception>
    void PlatformSpecific()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            _exe = "gcc";
            _ext = "so";
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            _exe = "clang";
            _ext = "dylib";
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            throw new NotSupportedException($"{OSPlatform.Windows}");
        }
    }

    void ArchSpecific()
    {
        _arch = RuntimeInformation.OSArchitecture switch
        {
            Architecture.X64 => "x86_64",
            Architecture.Arm64 => "arm64",
            _ => throw new NotSupportedException(RuntimeInformation.OSArchitecture.ToString()),
        };
    }

    protected string Exe
    {
        get => _exe;
    }

    protected string Arch
    {
        get => _arch;
    }

    protected string Ext
    {
        get => _ext;
    }

    public CSourceCompiler()
    {
        PlatformSpecific();
        ArchSpecific();
    }

    /// <summary>
    /// compile the source txt, write to the out_path
    /// </summary>
    /// <param name="sourcePath"> c source code</param>
    /// <param name="outPath"> out .so path </param>
    /// <returns> outPath </returns>
    public string Compile(string sourcePath, string outPath)
    {
        var errMsg = new StringBuilder();
        using (var errWriter = new StringWriter(errMsg))
        {
            using (var proc = new Process())
            {
                proc.StartInfo.FileName = Exe;
                proc.StartInfo.Arguments = $"{sourcePath} -fPIC -shared -arch {Arch} -o {outPath}";
                proc.StartInfo.RedirectStandardError = true;
                proc.ErrorDataReceived += (sender, e) => errWriter.WriteLine(e.Data);
                proc.Start();
                proc.BeginErrorReadLine();
                proc.WaitForExit();
                if (proc.ExitCode != 0)
                {
                    throw new InvalidOperationException(errMsg.ToString());
                }
            }
        }
        return outPath;
    }

    /// <summary>
    /// create the temp dll file and compile source
    /// <see cref="Compile(string, string)"/>
    /// </summary>
    public string Compile(string sourcePath) => Compile(sourcePath, CodeGenUtil.GetTempFileName(Ext));
}