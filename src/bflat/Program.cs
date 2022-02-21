// bflat C# compiler
// Copyright (C) 2021-2022 Michal Strehovsky
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License as published
// by the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Affero General Public License for more details.
//
// You should have received a copy of the GNU Affero General Public License
// along with this program.  If not, see <https://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Text;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Text;

using ILCompiler;

using Internal.CommandLine;
using Internal.IL;
using Internal.JitInterface;
using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;

class Program
{
    private const string DefaultSystemModule = "System.Private.CoreLib";

    private IReadOnlyList<string> _inputFiles = Array.Empty<string>();
    private IReadOnlyList<string> _referenceFiles = Array.Empty<string>();
    private IReadOnlyList<string> _defines = Array.Empty<string>();
    private IReadOnlyList<string> _ldflags = Array.Empty<string>();
    private IReadOnlyList<string> _directPinvokes = Array.Empty<string>();

    private string _outputFilePath;
    private bool _isVerbose;

    private TargetArchitecture _targetArchitecture;
    private string _targetArchitectureStr;
    private TargetOS _targetOS;
    private string _targetOSStr;
    private OptimizationMode _optimizationMode;
    private string _systemModuleName = DefaultSystemModule;
    private bool _disableStackTraceData;
    private bool _disableReflection;
    private bool _disableGlobalization;
    private bool _disableExceptionMessages;
    private bool _bare;
    private bool _dontLink;
    private string _mapFileName;
    private string _target;
    private bool _printCommands;

    class Tsc : CompilerTypeSystemContext
    {
        public unsafe Tsc(TargetDetails details, SharedGenericsMode genericsMode, DelegateFeature delegateFeatures, MemoryStream compiledModule, string compiledModuleName)
            : base(details, genericsMode, delegateFeatures)
        {
            var mappedFile = MemoryMappedFile.CreateNew(mapName: null, compiledModule.Length);
            var vs = mappedFile.CreateViewStream();
            compiledModule.CopyTo(vs);
            compiledModule.Dispose();
            vs.Dispose();

            var accessor = mappedFile.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);

            var safeBuffer = accessor.SafeMemoryMappedViewHandle;
            var peReader = new PEReader((byte*)safeBuffer.DangerousGetHandle(), (int)safeBuffer.ByteLength);

            var pdbReader = PortablePdbSymbolReader.TryOpenEmbedded(peReader, GetMetadataStringDecoder());

            CacheOpenModule(compiledModuleName, compiledModuleName, EcmaModule.Create(this, peReader, null, pdbReader), accessor);
        }
    }

    private void InitializeDefaultOptions()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            _targetOS = TargetOS.Windows;
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            _targetOS = TargetOS.Linux;
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            _targetOS = TargetOS.OSX;
        else
            throw new NotImplementedException();

        switch (RuntimeInformation.ProcessArchitecture)
        {
            case Architecture.X86:
                _targetArchitecture = TargetArchitecture.X86;
                break;
            case Architecture.X64:
                _targetArchitecture = TargetArchitecture.X64;
                break;
            case Architecture.Arm:
                _targetArchitecture = TargetArchitecture.ARM;
                break;
            case Architecture.Arm64:
                _targetArchitecture = TargetArchitecture.ARM64;
                break;
            default:
                throw new NotImplementedException();
        }
    }

    private ArgumentSyntax ParseCommandLine(string[] args)
    {
        bool nooptimize = false;
        bool optimizeSpace = false;
        bool optimizeTime = false;

        ArgumentSyntax argSyntax = ArgumentSyntax.Parse(args, syntax =>
        {
            syntax.ApplicationName = "bflat";

            var buildCommand = syntax.DefineCommand("build");
            buildCommand.Help = "Builds the specified C# source files or module";

            syntax.DefineOption("o|out", ref _outputFilePath, "Output file path");
            syntax.DefineOptionList("d|define", ref _defines, "Define conditional compilation symbol(s)");
            syntax.DefineOptionList("ldflags", ref _ldflags, "Arguments to pass to the linker");
            syntax.DefineOption("x", ref _printCommands, "Print the commands");
            syntax.DefineOption("c", ref _dontLink, "Produce object file, but don't run linker");
            syntax.DefineOptionList("r", ref _referenceFiles, "Additional .NET assemblies to include");
            syntax.DefineOption("O0", ref nooptimize, "Disable optimizations");
            syntax.DefineOption("Os", ref optimizeSpace, "Favor code space when optimizing");
            syntax.DefineOption("Ot", ref optimizeTime, "Favor code speed when optimizing");
            syntax.DefineOption("verbose", ref _isVerbose, "Enable verbose logging");
            syntax.DefineOption("no-reflection", ref _disableReflection, "Disable support for reflection");
            syntax.DefineOption("no-stacktrace-data", ref _disableStackTraceData, "Disable support for textual stack traces");
            syntax.DefineOption("no-globalization", ref _disableGlobalization, "Disable support for globalization (use invariant mode)");
            syntax.DefineOption("no-exception-messages", ref _disableExceptionMessages, "Disable exception messages");
            syntax.DefineOption("bare", ref _bare, "Do not include standard library");
            syntax.DefineOption("map", ref _mapFileName, "Generate a map file");
            syntax.DefineOption("target", ref _target, "Build target (one of: exe, winexe, shared)");
            syntax.DefineOptionList("i", ref _directPinvokes, "Bind to entrypoint statically ('-i libraryName' or '-i lib!Function')");

            syntax.DefineOption("arch", ref _targetArchitectureStr, "Target architecture (x64, arm64)");
            syntax.DefineOption("os", ref _targetOSStr, "Target OS (windows, linux-glibc)");

            syntax.DefineParameterList("in", ref _inputFiles, "Input source files to compile");
        });

        _optimizationMode = OptimizationMode.Blended;
        if (optimizeSpace)
        {
            if (optimizeTime)
                Console.WriteLine("Warning: overriding -Ot with -Os");
            _optimizationMode = OptimizationMode.PreferSize;
        }
        else if (optimizeTime)
            _optimizationMode = OptimizationMode.PreferSpeed;
        else if (nooptimize)
            _optimizationMode = OptimizationMode.None;

        if (_target != null)
        {
            _target = _target.ToLowerInvariant();
            if (_target != "exe" && _target != "winexe"
                && _target != "shared" && _target != "archive")
            {
                Console.Error.WriteLine("Target '{0}' is not recognized.");
                return null;
            }
        }

        return argSyntax;
    }

    private IReadOnlyCollection<MethodDesc> CreateInitializerList(CompilerTypeSystemContext context)
    {
        List<ModuleDesc> assembliesWithInitalizers = new List<ModuleDesc>();

        List<string> initAssemblies = new List<string> { "System.Private.CoreLib" };

        if (!_disableReflection || !_disableStackTraceData)
            initAssemblies.Add("System.Private.StackTraceMetadata");

        initAssemblies.Add("System.Private.TypeLoader");

        if (!_disableReflection)
            initAssemblies.Add("System.Private.Reflection.Execution");
        else
            initAssemblies.Add("System.Private.DisabledReflection");

        initAssemblies.Add("System.Private.Interop");

        // Build a list of assemblies that have an initializer that needs to run before
        // any user code runs.
        if (!_bare)
        {
            foreach (string initAssemblyName in initAssemblies)
            {
                ModuleDesc assembly = context.GetModuleForSimpleName(initAssemblyName);
                assembliesWithInitalizers.Add(assembly);
            }
        }

        var libraryInitializers = new LibraryInitializers(context, assembliesWithInitalizers);

        List<MethodDesc> initializerList = new List<MethodDesc>(libraryInitializers.LibraryInitializerMethods);

        return initializerList;
    }

    private static IEnumerable<string> EnumerateExpandedDirectories(string paths, string pattern)
    {
        string[] split = paths.Split(RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ';' : ':');
        foreach (var dir in split)
        {
            foreach (var file in Directory.GetFiles(dir, pattern))
            {
                yield return file;
            }
        }
    }

    private int Run(string[] args)
    {
        if (args.Length == 0 ||
            args[args.Length - 1] == "--help" || args[args.Length - 1] == "-h" || args[args.Length - 1] == "-?")
        {
            Console.WriteLine("bflat C# compiler 0.0.3");
            Console.WriteLine("https://github.com/MichalStrehovsky/bflat");
            Console.WriteLine();
        }

        InitializeDefaultOptions();

        ArgumentSyntax syntax = ParseCommandLine(args);
        if (syntax == null)
        {
            return 1;
        }

        var logger = new Logger(Console.Out, _isVerbose, Array.Empty<int>(), singleWarn: false, Array.Empty<string>(), Array.Empty<string>());

        //
        // Set target Architecture and OS
        //
        if (_targetArchitectureStr != null)
        {
            if (_targetArchitectureStr.Equals("x86", StringComparison.OrdinalIgnoreCase))
                _targetArchitecture = TargetArchitecture.X86;
            else if (_targetArchitectureStr.Equals("x64", StringComparison.OrdinalIgnoreCase))
                _targetArchitecture = TargetArchitecture.X64;
            else if (_targetArchitectureStr.Equals("arm", StringComparison.OrdinalIgnoreCase))
                _targetArchitecture = TargetArchitecture.ARM;
            else if (_targetArchitectureStr.Equals("armel", StringComparison.OrdinalIgnoreCase))
                _targetArchitecture = TargetArchitecture.ARM;
            else if (_targetArchitectureStr.Equals("arm64", StringComparison.OrdinalIgnoreCase))
                _targetArchitecture = TargetArchitecture.ARM64;
            else
                throw new Exception("Target architecture is not supported");
        }
        if (_targetOSStr != null)
        {
            if (_targetOSStr.Equals("windows", StringComparison.OrdinalIgnoreCase))
                _targetOS = TargetOS.Windows;
            else if (_targetOSStr.StartsWith("linux", StringComparison.OrdinalIgnoreCase))
                _targetOS = TargetOS.Linux;
            else if (_targetOSStr.Equals("osx", StringComparison.OrdinalIgnoreCase))
                _targetOS = TargetOS.OSX;
            else
                throw new Exception("Target OS is not supported");
        }

        //
        // Compile C# source files
        //

        bool needsOutputSuffix = false;
        if (_inputFiles.Count == 0)
        {
            _inputFiles = new List<string>(Directory.EnumerateFiles(Directory.GetCurrentDirectory(), "*.cs", SearchOption.AllDirectories));

            if (_outputFilePath == null)
            {
                _outputFilePath = Path.Combine(
                    Directory.GetCurrentDirectory(),
                    Path.GetFileName(Directory.GetCurrentDirectory()));
                needsOutputSuffix = true;
            }
        }
        else if (_outputFilePath == null)
        {
            _outputFilePath = Path.Combine(
                    Directory.GetCurrentDirectory(),
                    Path.GetFileNameWithoutExtension(_inputFiles[0]));
            needsOutputSuffix = true;
        }

        if (_inputFiles.Count == 0)
        {
            Console.Error.WriteLine("No input files specified and no files matching *.cs found in the current directory");
            return 1;
        }

        var defines = new List<string>(_defines);
        defines.Add("BFLAT");
        switch (_targetArchitecture)
        {
            case TargetArchitecture.ARM:
                defines.Add("ARM32"); break;
            case TargetArchitecture.ARM64:
                defines.Add("ARM64"); break;
            case TargetArchitecture.X86:
                defines.Add("X86"); break;
            case TargetArchitecture.X64:
                defines.Add("X64"); break;
            default:
                throw new NotImplementedException();
        }

        switch (_targetOS)
        {
            case TargetOS.Windows:
                defines.Add("WINDOWS"); break;
            case TargetOS.Linux:
                defines.Add("LINUX"); break;
            case TargetOS.OSX:
                defines.Add("MACOS"); break;
            default:
                throw new NotImplementedException();
        }

        if (_optimizationMode == OptimizationMode.None)
            defines.Add("DEBUG");

        var trees = new List<SyntaxTree>();
        foreach (var sourceFile in _inputFiles)
        {
            var st = SourceText.From(File.OpenRead(sourceFile));
            CSharpParseOptions parseOptions = new CSharpParseOptions(preprocessorSymbols: defines);
            string path = sourceFile;
            if (!Path.IsPathRooted(sourceFile))
                path = Path.GetFullPath(sourceFile, Directory.GetCurrentDirectory());
            trees.Add(CSharpSyntaxTree.ParseText(st, parseOptions, path));
        }

        bool nativeLib;

        // Check if we have a Main
        if (_target == null)
        {
            nativeLib = true;
            _target = "shared";
            foreach (var tree in trees)
            {
                foreach (var descendant in tree.GetRoot().DescendantNodes())
                {
                    if (descendant is Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax methodSyntax)
                    {
                        if (methodSyntax.Identifier.Text == "Main" &&
                            methodSyntax.DescendantTokens().Any(x => x.IsKind(SyntaxKind.StaticKeyword)))
                        {
                            int paramCount = methodSyntax.ParameterList.Parameters.Count;
                            if (paramCount == 0 || paramCount == 1)
                            {
                                nativeLib = false;
                                _target = "exe";
                            }
                        }
                    }
                    else if (descendant is Microsoft.CodeAnalysis.CSharp.Syntax.GlobalStatementSyntax)
                    {
                        nativeLib = false;
                        _target = "exe";
                    }
                }
            }
        }
        else
        {
            nativeLib = _target == "shared" || _target == "archive";
        }

        if (needsOutputSuffix)
        {
            if (_targetOS == TargetOS.Windows)
            {
                if (_target == "exe" || _target == "winexe")
                    _outputFilePath += ".exe";
                else
                    _outputFilePath += ".dll";
            }
            else
            {
                if (_target != "exe" && _target != "winexe")
                {
                    _outputFilePath += ".so";

                    _outputFilePath = Path.Combine(
                        Path.GetDirectoryName(_outputFilePath),
                        "lib" + Path.GetFileName(_outputFilePath));
                }
            }
        }

        try
        {
            if (File.Exists(_outputFilePath))
                File.Delete(_outputFilePath);
        }
        catch { }

        var metadataReferences = new List<MetadataReference>();
        foreach (var referenceFile in _referenceFiles)
        {
            metadataReferences.Add(MetadataReference.CreateFromFile(referenceFile));
        }

        string homePath = Environment.GetEnvironmentVariable("BFLAT_HOME") ?? AppContext.BaseDirectory;

        if (!_bare)
        {
            string refPath = Environment.GetEnvironmentVariable("BFLAT_REF");
            if (refPath == null)
            {
                refPath = Path.Combine(homePath, "ref");
            }

            foreach (var referenceFile in EnumerateExpandedDirectories(refPath, "*.dll"))
            {
                metadataReferences.Add(MetadataReference.CreateFromFile(referenceFile));
            }
        }

        string compiledModuleName = Path.GetFileNameWithoutExtension(_outputFilePath);

        OptimizationLevel optimizationLevel = _optimizationMode == OptimizationMode.None ? OptimizationLevel.Debug : OptimizationLevel.Release;
        OutputKind outputKind = nativeLib ? OutputKind.DynamicallyLinkedLibrary : OutputKind.ConsoleApplication;

        var comp = CSharpCompilation.Create(compiledModuleName, trees, metadataReferences, new CSharpCompilationOptions(outputKind, allowUnsafe: true, optimizationLevel: optimizationLevel));

        var emitOptions = new EmitOptions(debugInformationFormat: DebugInformationFormat.Embedded);

        if (logger.IsVerbose)
            logger.Writer.WriteLine($"Compiling {trees.Count} C# source files");
        var ms = new MemoryStream();
        var result = comp.Emit(ms, options: emitOptions);
        if (!result.Success)
        {
            IEnumerable<Diagnostic> failures = result.Diagnostics.Where(diagnostic =>
                diagnostic.IsWarningAsError ||
                diagnostic.Severity == DiagnosticSeverity.Error);

            foreach (Diagnostic diagnostic in failures)
            {
                Console.Error.WriteLine(diagnostic.ToString());
            }

            return 1;
        }
        ms.Seek(0, SeekOrigin.Begin);

        if (_bare)
            _systemModuleName = compiledModuleName;

        InstructionSetSupportBuilder instructionSetSupportBuilder = new InstructionSetSupportBuilder(_targetArchitecture);

        // The runtime expects certain baselines that the codegen can assume as well.
        if ((_targetArchitecture == TargetArchitecture.X86) || (_targetArchitecture == TargetArchitecture.X64))
        {
            instructionSetSupportBuilder.AddSupportedInstructionSet("sse");
            instructionSetSupportBuilder.AddSupportedInstructionSet("sse2");
        }
        else if (_targetArchitecture == TargetArchitecture.ARM64)
        {
            instructionSetSupportBuilder.AddSupportedInstructionSet("base");
            instructionSetSupportBuilder.AddSupportedInstructionSet("neon");
        }

        instructionSetSupportBuilder.ComputeInstructionSetFlags(out var supportedInstructionSet, out var unsupportedInstructionSet,
            (string specifiedInstructionSet, string impliedInstructionSet) =>
                throw new Exception(String.Format("Unsupported combination of instruction sets: {0}/{1}", specifiedInstructionSet, impliedInstructionSet)));

        InstructionSetSupportBuilder optimisticInstructionSetSupportBuilder = new InstructionSetSupportBuilder(_targetArchitecture);

        // Optimistically assume some instruction sets are present.
        if ((_targetArchitecture == TargetArchitecture.X86) || (_targetArchitecture == TargetArchitecture.X64))
        {
            // We set these hardware features as enabled always, as most
            // of hardware in the wild supports them. Note that we do not indicate support for AVX, or any other
            // instruction set which uses the VEX encodings as the presence of those makes otherwise acceptable
            // code be unusable on hardware which does not support VEX encodings, as well as emulators that do not
            // support AVX instructions.
            //
            // The compiler is able to generate runtime IsSupported checks for the following instruction sets.
            optimisticInstructionSetSupportBuilder.AddSupportedInstructionSet("sse4.1");
            optimisticInstructionSetSupportBuilder.AddSupportedInstructionSet("sse4.2");
            optimisticInstructionSetSupportBuilder.AddSupportedInstructionSet("ssse3");
            optimisticInstructionSetSupportBuilder.AddSupportedInstructionSet("aes");
            optimisticInstructionSetSupportBuilder.AddSupportedInstructionSet("pclmul");
            optimisticInstructionSetSupportBuilder.AddSupportedInstructionSet("popcnt");
            optimisticInstructionSetSupportBuilder.AddSupportedInstructionSet("lzcnt");

            // If AVX was enabled, we can opportunistically enable FMA/BMI
            Debug.Assert(InstructionSet.X64_AVX == InstructionSet.X86_AVX);
            if (supportedInstructionSet.HasInstructionSet(InstructionSet.X64_AVX))
            {
                optimisticInstructionSetSupportBuilder.AddSupportedInstructionSet("fma");
                optimisticInstructionSetSupportBuilder.AddSupportedInstructionSet("bmi");
                optimisticInstructionSetSupportBuilder.AddSupportedInstructionSet("bmi2");
            }
        }
        else if (_targetArchitecture == TargetArchitecture.ARM64)
        {
            optimisticInstructionSetSupportBuilder.AddSupportedInstructionSet("aes");
            optimisticInstructionSetSupportBuilder.AddSupportedInstructionSet("crc");
            optimisticInstructionSetSupportBuilder.AddSupportedInstructionSet("sha1");
            optimisticInstructionSetSupportBuilder.AddSupportedInstructionSet("sha2");
            optimisticInstructionSetSupportBuilder.AddSupportedInstructionSet("lse");
        }

        optimisticInstructionSetSupportBuilder.ComputeInstructionSetFlags(out var optimisticInstructionSet, out _,
            (string specifiedInstructionSet, string impliedInstructionSet) => throw new NotSupportedException());
        optimisticInstructionSet.Remove(unsupportedInstructionSet);
        optimisticInstructionSet.Add(supportedInstructionSet);

        var instructionSetSupport = new InstructionSetSupport(supportedInstructionSet,
                                                              unsupportedInstructionSet,
                                                              optimisticInstructionSet,
                                                              InstructionSetSupportBuilder.GetNonSpecifiableInstructionSetsForArch(_targetArchitecture),
                                                              _targetArchitecture);

        bool supportsReflection = !_disableReflection && _systemModuleName == DefaultSystemModule;

        //
        // Initialize type system context
        //

        SharedGenericsMode genericsMode = SharedGenericsMode.CanonicalReferenceTypes;

        var simdVectorLength = instructionSetSupport.GetVectorTSimdVector();
        var targetAbi = TargetAbi.CoreRT;
        var targetDetails = new TargetDetails(_targetArchitecture, _targetOS, targetAbi, simdVectorLength);
        CompilerTypeSystemContext typeSystemContext =
            new Tsc(targetDetails, genericsMode, supportsReflection ? DelegateFeature.All : 0, ms, compiledModuleName);

        var referenceFilePaths = new Dictionary<string, string>();

        foreach (var reference in _referenceFiles)
        {
            referenceFilePaths[Path.GetFileNameWithoutExtension(reference)] = reference;
        }

        string libPath = Environment.GetEnvironmentVariable("BFLAT_LIB");
        if (libPath == null)
        {
            char separator = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ';' : ':';

            string osPart = _targetOS switch
            {
                TargetOS.Linux => "linux-glibc",
                TargetOS.Windows => "windows",
                _ => throw new Exception(_targetOS.ToString()),
            };

            string archPart = _targetArchitecture switch
            {
                TargetArchitecture.ARM64 => "arm64",
                TargetArchitecture.X64 => "x64",
                _ => throw new Exception(_targetArchitecture.ToString()),
            };

            string osArchPath = Path.Combine(homePath, "lib", $"{osPart}-{archPart}");
            if (!Directory.Exists(osArchPath))
            {
                Console.Error.WriteLine($"Directory '{osArchPath}' doesn't exist.");
                return 1;
            }

            libPath = String.Concat(osArchPath, separator.ToString(), Path.Combine(homePath, "lib"));
        }

        if (!_bare)
        {
            foreach (var reference in EnumerateExpandedDirectories(libPath, "*.dll"))
            {
                string assemblyName = Path.GetFileNameWithoutExtension(reference);
                referenceFilePaths[assemblyName] = reference;
            }
        }

        typeSystemContext.InputFilePaths = new Dictionary<string, string>();
        typeSystemContext.ReferenceFilePaths = referenceFilePaths;

        typeSystemContext.SetSystemModule(typeSystemContext.GetModuleForSimpleName(_systemModuleName));
        EcmaModule compiledAssembly = typeSystemContext.GetModuleForSimpleName(compiledModuleName);

        //
        // Initialize compilation group and compilation roots
        //

        CompilationModuleGroup compilationGroup;
        List<ICompilationRootProvider> compilationRoots = new List<ICompilationRootProvider>();

        compilationRoots.Add(new ExportedMethodsRootProvider(compiledAssembly));

        if (!nativeLib)
        {
            compilationRoots.Add(new MainMethodRootProvider(compiledAssembly, CreateInitializerList(typeSystemContext)));
            compilationRoots.Add(new RuntimeConfigurationRootProvider(Array.Empty<string>()));
            compilationRoots.Add(new ExpectedIsaFeaturesRootProvider(instructionSetSupport));
        }

        if (compiledAssembly != typeSystemContext.SystemModule)
            compilationRoots.Add(new ExportedMethodsRootProvider((EcmaModule)typeSystemContext.SystemModule));
        compilationGroup = new SingleFileCompilationModuleGroup();

        if (nativeLib)
        {
            // Set owning module of generated native library startup method to compiler generated module,
            // to ensure the startup method is included in the object file during multimodule mode build
            compilationRoots.Add(new NativeLibraryInitializerRootProvider(typeSystemContext.GeneratedAssembly, CreateInitializerList(typeSystemContext)));
            compilationRoots.Add(new RuntimeConfigurationRootProvider(Array.Empty<string>()));
            compilationRoots.Add(new ExpectedIsaFeaturesRootProvider(instructionSetSupport));
        }

        //
        // Compile
        //

        CompilationBuilder builder = new RyuJitCompilationBuilder(typeSystemContext, compilationGroup);

        builder.UseCompilationUnitPrefix("");

        List<string> directPinvokeList = new List<string>();
        List<string> directPinvokes = new List<string>(_directPinvokes);
        if (_targetOS == TargetOS.Windows)
        {
            directPinvokeList.Add(Path.Combine(homePath, "WindowsAPIs.txt"));
            directPinvokes.Add("System.IO.Compression.Native");
            directPinvokes.Add("sokol");
        }
        else
        {
            directPinvokes.Add("libSystem.Native");
            directPinvokes.Add("libSystem.Globalization.Native");
            directPinvokes.Add("libSystem.IO.Compression.Native");
            directPinvokes.Add("libSystem.Net.Security.Native");
            directPinvokes.Add("libSystem.Security.Cryptography.Native.OpenSsl");
            directPinvokes.Add("libsokol");
        }

        PInvokeILEmitterConfiguration pinvokePolicy = new ConfigurablePInvokePolicy(typeSystemContext.Target, directPinvokes, directPinvokeList);

        ILProvider ilProvider = new CoreRTILProvider();

        List<KeyValuePair<string, bool>> featureSwitches = new List<KeyValuePair<string, bool>>
        {
            KeyValuePair.Create("System.Diagnostics.Debugger.IsSupported", false),
            KeyValuePair.Create("System.Diagnostics.Tracing.EventSource.IsSupported", false),
            KeyValuePair.Create("System.Runtime.Serialization.EnableUnsafeBinaryFormatterSerialization", false),
            KeyValuePair.Create("System.Resources.ResourceManager.AllowCustomResourceTypes", false),
            KeyValuePair.Create("System.Text.Encoding.EnableUnsafeUTF7Encoding", false),
            KeyValuePair.Create("System.Runtime.Serialization.DataContractSerializer.IsReflectionOnly", true),
            KeyValuePair.Create("System.Xml.Serialization.XmlSerializer.IsReflectionOnly", true),
            KeyValuePair.Create("System.Xml.XmlDownloadManager.IsNonFileStreamSupported", false),
        };

        if (_disableExceptionMessages || _disableReflection)
        {
            featureSwitches.Add(KeyValuePair.Create("System.Resources.UseSystemResourceKeys", true));
        }

        if (_disableGlobalization)
        {
            featureSwitches.Add(KeyValuePair.Create("System.Globalization.Invariant", true));
        }

        if (_disableReflection)
        {
            featureSwitches.Add(KeyValuePair.Create("System.Collections.Generic.DefaultComparers", false));
        }

        ilProvider = new FeatureSwitchManager(ilProvider, featureSwitches);

        var stackTracePolicy = !_disableStackTraceData ?
            (StackTraceEmissionPolicy)new EcmaMethodStackTraceEmissionPolicy() : new NoStackTraceEmissionPolicy();

        MetadataBlockingPolicy mdBlockingPolicy;
        ManifestResourceBlockingPolicy resBlockingPolicy;
        UsageBasedMetadataGenerationOptions metadataGenerationOptions = default;
        if (supportsReflection)
        {
            mdBlockingPolicy = new BlockedInternalsBlockingPolicy(typeSystemContext);

            resBlockingPolicy = new ManifestResourceBlockingPolicy(featureSwitches);

            metadataGenerationOptions |= UsageBasedMetadataGenerationOptions.AnonymousTypeHeuristic;
            metadataGenerationOptions |= UsageBasedMetadataGenerationOptions.ReflectionILScanning;
            metadataGenerationOptions |= UsageBasedMetadataGenerationOptions.ReflectedMembersOnly;
        }
        else
        {
            mdBlockingPolicy = new FullyBlockedMetadataBlockingPolicy();
            resBlockingPolicy = new FullyBlockedManifestResourceBlockingPolicy();
        }

        DynamicInvokeThunkGenerationPolicy invokeThunkGenerationPolicy = new DefaultDynamicInvokeThunkGenerationPolicy();

        var flowAnnotations = new ILCompiler.Dataflow.FlowAnnotations(logger, ilProvider);

        MetadataManager metadataManager = new UsageBasedMetadataManager(
            compilationGroup,
            typeSystemContext,
            mdBlockingPolicy,
            resBlockingPolicy,
            logFile: null,
            stackTracePolicy,
            invokeThunkGenerationPolicy,
            flowAnnotations,
            metadataGenerationOptions,
            logger,
            featureSwitches,
            Array.Empty<string>(),
            Array.Empty<string>());

        InteropStateManager interopStateManager = new InteropStateManager(typeSystemContext.GeneratedAssembly);
        InteropStubManager interopStubManager = new UsageBasedInteropStubManager(interopStateManager, pinvokePolicy, logger);

        // We enable scanner for retail builds by default.
        bool useScanner = _optimizationMode != OptimizationMode.None;

        // Enable static data preinitialization in optimized builds.
        bool preinitStatics = _optimizationMode != OptimizationMode.None;

        var preinitManager = new PreinitializationManager(typeSystemContext, compilationGroup, ilProvider, preinitStatics);
        builder
            .UseILProvider(ilProvider)
            .UsePreinitializationManager(preinitManager)
            .UseResilience(true);

        ILScanResults scanResults = null;
        if (useScanner)
        {
            if (logger.IsVerbose)
                logger.Writer.WriteLine("Scanning input IL");
            ILScannerBuilder scannerBuilder = builder.GetILScannerBuilder()
                .UseCompilationRoots(compilationRoots)
                .UseMetadataManager(metadataManager)
                .UseInteropStubManager(interopStubManager);

            IILScanner scanner = scannerBuilder.ToILScanner();

            scanResults = scanner.Scan();

            metadataManager = ((UsageBasedMetadataManager)metadataManager).ToAnalysisBasedMetadataManager();

            interopStubManager = scanResults.GetInteropStubManager(interopStateManager, pinvokePolicy);
        }

        DebugInformationProvider debugInfoProvider = new DebugInformationProvider();

        DependencyTrackingLevel trackingLevel = DependencyTrackingLevel.None;

        compilationRoots.Add(metadataManager);
        compilationRoots.Add(interopStubManager);

        builder
            .UseInstructionSetSupport(instructionSetSupport)
            .UseMethodBodyFolding(enable: _optimizationMode != OptimizationMode.None)
            .UseMetadataManager(metadataManager)
            .UseInteropStubManager(interopStubManager)
            .UseLogger(logger)
            .UseDependencyTracking(trackingLevel)
            .UseCompilationRoots(compilationRoots)
            .UseOptimizationMode(_optimizationMode)
            .UseDebugInfoProvider(debugInfoProvider);

        if (scanResults != null)
        {
            // If we have a scanner, feed the vtable analysis results to the compilation.
            // This could be a command line switch if we really wanted to.
            builder.UseVTableSliceProvider(scanResults.GetVTableLayoutInfo());

            // If we have a scanner, feed the generic dictionary results to the compilation.
            // This could be a command line switch if we really wanted to.
            builder.UseGenericDictionaryLayoutProvider(scanResults.GetDictionaryLayoutInfo());

            // If we have a scanner, we can drive devirtualization using the information
            // we collected at scanning time (effectively sealing unsealed types if possible).
            // This could be a command line switch if we really wanted to.
            builder.UseDevirtualizationManager(scanResults.GetDevirtualizationManager());

            // If we use the scanner's result, we need to consult it to drive inlining.
            // This prevents e.g. devirtualizing and inlining methods on types that were
            // never actually allocated.
            builder.UseInliningPolicy(scanResults.GetInliningPolicy());
        }

        ICompilation compilation = builder.ToCompilation();

        if (logger.IsVerbose)
            logger.Writer.WriteLine("Generating native code");
        ObjectDumper dumper = _mapFileName != null ? new ObjectDumper(_mapFileName) : null;
        string objectFilePath = Path.ChangeExtension(_outputFilePath, _targetOS == TargetOS.Windows ? ".obj" : ".o");
        CompilationResults compilationResults = compilation.Compile(objectFilePath, dumper);

        string exportsFile = null;
        if (nativeLib)
        {
            exportsFile = Path.ChangeExtension(_outputFilePath, _targetOS == TargetOS.Windows ? ".def" : ".txt");
            ExportsFileWriter defFileWriter = new ExportsFileWriter(typeSystemContext, exportsFile);
            foreach (var compilationRoot in compilationRoots)
            {
                if (compilationRoot is ExportedMethodsRootProvider provider)
                    defFileWriter.AddExportedMethods(provider.ExportedMethods);
            }

            defFileWriter.EmitExportedMethods();
        }

        if (debugInfoProvider is IDisposable)
            ((IDisposable)debugInfoProvider).Dispose();

        preinitManager.LogStatistics(logger);

        if (_dontLink)
        {
            return 0;
        }

        //
        // Run the platform linker
        //

        if (logger.IsVerbose)
            logger.Writer.WriteLine("Running the linker");

        string ld = Environment.GetEnvironmentVariable("BFLAT_LD");
        if (ld == null)
        {
            string toolSuffix = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".exe" : "";

            ld = Path.Combine(homePath, "bin", "lld" + toolSuffix);
        }

        var ldArgs = new StringBuilder();

        if (_targetOS == TargetOS.Windows)
        {
            ldArgs.Append("-flavor link \"");
            ldArgs.Append(objectFilePath);
            ldArgs.Append("\" ");
            ldArgs.AppendFormat("/out:\"{0}\" ", _outputFilePath);

            foreach (var lpath in libPath.Split(RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ';' : ':'))
            {
                ldArgs.AppendFormat("/libpath:\"{0}\" ", lpath);
            }

            if (_target == "exe")
                ldArgs.Append("/subsystem:console ");
            if (_target == "winexe")
                ldArgs.Append("/subsystem:windows ");

            if (_target == "exe" || _target == "winexe")
                ldArgs.Append("/entry:wmainCRTStartup bootstrapper.lib ");

            if (_target == "shared")
            {
                ldArgs.Append("/dll /include:CoreRT_StaticInitialization bootstrapperdll.lib ");
                ldArgs.Append($"/def:\"{exportsFile}\" ");
            }

            ldArgs.Append("/incremental:no ");
            ldArgs.Append("/debug ");
            ldArgs.Append("sokol.lib Runtime.WorkstationGC.lib System.IO.Compression.Native.Aot.lib advapi32.lib bcrypt.lib crypt32.lib iphlpapi.lib kernel32.lib mswsock.lib ncrypt.lib normaliz.lib  ntdll.lib ole32.lib oleaut32.lib user32.lib version.lib ws2_32.lib shell32.lib Secur32.lib msvcrt.lib ");
            ldArgs.Append("/opt:ref,icf /nodefaultlib:libcpmt.lib ");
        }
        else if (_targetOS == TargetOS.Linux)
        {
            ldArgs.Append("-flavor ld ");

            string firstLib = null;
            foreach (var lpath in libPath.Split(RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ';' : ':'))
            {
                ldArgs.AppendFormat("-L\"{0}\" ", lpath);
                if (firstLib == null)
                    firstLib = lpath;
            }

            ldArgs.Append("-z now -z relro --hash-style=gnu --eh-frame-hdr ");
            if (_target != "shared")
            {
                ldArgs.Append("-dynamic-linker /lib64/ld-linux-x86-64.so.2 ");
                ldArgs.Append($"\"{firstLib}/Scrt1.o\" ");
            }
            
            ldArgs.AppendFormat("-o \"{0}\" ", _outputFilePath);

            ldArgs.Append($"\"{firstLib}/crti.o\" ");
            ldArgs.Append($"\"{firstLib}/crtbeginS.o\" ");

            ldArgs.Append('"');
            ldArgs.Append(objectFilePath);
            ldArgs.Append('"');
            ldArgs.Append(' ');
            ldArgs.Append("--as-needed --discard-all --gc-sections ");
            ldArgs.Append("-rpath \"$ORIGIN\" ");

            if (_target == "shared")
            {
                ldArgs.Append("-lbootstrapperdll ");
                ldArgs.Append("-shared ");
                ldArgs.Append("--undefined=CoreRT_StaticInitialization ");
                ldArgs.Append($"--version-script=\"{exportsFile}\" ");
            }
            else
            {
                ldArgs.Append("-lbootstrapper -pie ");
            }

            ldArgs.Append("-lRuntime.WorkstationGC -lSystem.Native -lSystem.Globalization.Native -lSystem.IO.Compression.Native -lSystem.Net.Security.Native -lSystem.Security.Cryptography.Native.OpenSsl ");
            ldArgs.Append("--as-needed -lstdc++ -ldl -lm -lz -lgssapi_krb5 -lrt -z relro -z now --discard-all --gc-sections -lgcc --as-needed -lgcc_s --no-as-needed -lpthread -lc -lgcc --as-needed -lgcc_s ");
            ldArgs.Append($"\"{firstLib}/crtendS.o\" ");
            ldArgs.Append($"\"{firstLib}/crtn.o\" ");
        }

        ldArgs.AppendJoin(' ', _ldflags);

        if (_printCommands)
        {
            Console.WriteLine($"{ld} {ldArgs}");
        }

        var p = Process.Start(ld, ldArgs.ToString());
        p.WaitForExit();

        int linkerExitCode = p.ExitCode;

        try { File.Delete(objectFilePath); } catch { }
        if (exportsFile != null)
            try { File.Delete(exportsFile); } catch { }

        return linkerExitCode;
    }

    private static int Main(string[] args)
    {
#if DEBUG
        return new Program().Run(args);
#else
        try
        {
            return new Program().Run(args);
        }
        catch (Exception e)
        {
            Console.Error.WriteLine("Error: " + e.Message);
            Console.Error.WriteLine(e.ToString());
            return 1;
        }
#endif
    }
}