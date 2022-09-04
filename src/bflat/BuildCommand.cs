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

#pragma warning disable 8509

using System;
using System.Collections.Generic;
using System.CommandLine.Parsing;
using System.CommandLine;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis;

using ILCompiler;

using Internal.TypeSystem;
using Internal.IL;
using Internal.JitInterface;
using Internal.TypeSystem.Ecma;

internal class BuildCommand : CommandBase
{
    private const string DefaultSystemModule = "System.Private.CoreLib";
    private BuildCommand() { }

    private static Option<bool> NoReflectionOption = new Option<bool>("--no-reflection", "Disable support for reflection");
    private static Option<bool> NoStackTraceDataOption = new Option<bool>("--no-stacktrace-data", "Disable support for textual stack traces");
    private static Option<bool> NoGlobalizationOption = new Option<bool>("--no-globalization", "Disable support for globalization (use invariant mode)");
    private static Option<bool> NoExceptionMessagesOption = new Option<bool>("--no-exception-messages", "Disable exception messages");

    private static Option<bool> NoLinkOption = new Option<bool>("-c", "Produce object file, but don't run linker");
    private static Option<string[]> LdFlagsOption = new Option<string[]>(new string[] { "--ldflags" }, "Arguments to pass to the linker");
    private static Option<bool> PrintCommandsOption = new Option<bool>("-x", "Print the commands");

    private static Option<string[]> DirectPInvokesOption = new Option<string[]>("-i", "Bind to entrypoint statically")
    {
        ArgumentHelpName = "library|library!function"
    };

    private static Option<bool> OptimizeSizeOption = new Option<bool>(new string[] { "-Os", "--optimize-space" }, "Favor code space when optimizing");
    private static Option<bool> OptimizeSpeedOption = new Option<bool>(new string[] { "-Ot", "--optimize-time" }, "Favor code speed when optimizing");
    private static Option<bool> DisableOptimizationOption = new Option<bool>(new string[] { "-O0", "--no-optimization" }, "Disable optimizations");

    private static Option<string> TargetArchitectureOption = new Option<string>("--arch", "Target architecture")
    {
        ArgumentHelpName = "x64|arm64"
    };
    private static Option<string> TargetOSOption = new Option<string>("--os", "Target operating system")
    {
        ArgumentHelpName = "linux|windows"
    };
    
    private static Option<string> TargetLibcOption = new Option<string>("--libc", "Target libc ('shcrt' or 'none' on Windows)");

    private static Option<string> MapFileOption = new Option<string>("--map", "Generate an object map file")
    {
        ArgumentHelpName = "file",
    };

    private static Option<string[]> FeatureSwitchOption = new Option<string[]>("--feature", "Set feature switch value")
    {
        ArgumentHelpName = "Feature=[true|false]",
    };

    public static Command Create()
    {
        var command = new Command("build", "Compiles the specified C# source files into native code")
        {
            CommonOptions.InputFilesArgument,
            CommonOptions.DefinedSymbolsOption,
            CommonOptions.ReferencesOption,
            CommonOptions.TargetOption,
            CommonOptions.OutputOption,
            NoLinkOption,
            LdFlagsOption,
            PrintCommandsOption,
            TargetArchitectureOption,
            TargetOSOption,
            TargetLibcOption,
            OptimizeSizeOption,
            OptimizeSpeedOption,
            DisableOptimizationOption,
            NoReflectionOption,
            NoStackTraceDataOption,
            NoGlobalizationOption,
            NoExceptionMessagesOption,
            MapFileOption,
            DirectPInvokesOption,
            FeatureSwitchOption,
            CommonOptions.ResourceOption,
            CommonOptions.BareOption,
            CommonOptions.DeterministicOption,
            CommonOptions.VerbosityOption,
        };
        command.Handler = new BuildCommand();

        return command;
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

    public override int Handle(ParseResult result)
    {
        bool nooptimize = result.GetValueForOption(DisableOptimizationOption);
        bool optimizeSpace = result.GetValueForOption(OptimizeSizeOption);
        bool optimizeTime = result.GetValueForOption(OptimizeSpeedOption);

        OptimizationMode optimizationMode = OptimizationMode.Blended;
        if (optimizeSpace)
        {
            if (optimizeTime)
                Console.WriteLine("Warning: overriding -Ot with -Os");
            optimizationMode = OptimizationMode.PreferSize;
        }
        else if (optimizeTime)
            optimizationMode = OptimizationMode.PreferSpeed;
        else if (nooptimize)
            optimizationMode = OptimizationMode.None;

        bool bare = result.GetValueForOption(CommonOptions.BareOption);
        string[] userSpecifiedInputFiles = result.GetValueForArgument(CommonOptions.InputFilesArgument);
        string[] inputFiles = CommonOptions.GetInputFiles(userSpecifiedInputFiles);
        string[] defines = result.GetValueForOption(CommonOptions.DefinedSymbolsOption);
        string[] references = CommonOptions.GetReferencePaths(result.GetValueForOption(CommonOptions.ReferencesOption), bare);

        TargetOS targetOS;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            targetOS = TargetOS.Windows;
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            targetOS = TargetOS.Linux;
        else
            throw new NotImplementedException();
        
        TargetArchitecture targetArchitecture = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64 => TargetArchitecture.X64,
            Architecture.Arm64 => TargetArchitecture.ARM64,
        };

        string targetArchitectureStr = result.GetValueForOption(TargetArchitectureOption);
        if (targetArchitectureStr != null)
        {
            targetArchitecture = targetArchitectureStr.ToLowerInvariant() switch
            {
                "x64" => TargetArchitecture.X64,
                "arm64" => TargetArchitecture.ARM64,
                _ => throw new Exception($"Target architecture '{targetArchitectureStr}' is not supported"),
            };
        }
        string targetOSStr = result.GetValueForOption(TargetOSOption);
        if (targetOSStr != null)
        {
            targetOS = targetOSStr.ToLowerInvariant() switch
            {
                "windows" => TargetOS.Windows,
                "linux" => TargetOS.Linux,
                _ => throw new Exception($"Target OS '{targetOSStr}' is not supported"),
            };
        }

        OptimizationLevel optimizationLevel = nooptimize ? OptimizationLevel.Debug : OptimizationLevel.Release;

        string userSpecificedOutputFileName = result.GetValueForOption(CommonOptions.OutputOption);
        string outputNameWithoutSuffix =
            userSpecificedOutputFileName != null ? Path.GetFileNameWithoutExtension(userSpecificedOutputFileName) :
            CommonOptions.GetOutputFileNameWithoutSuffix(userSpecifiedInputFiles);

        ILProvider ilProvider = new NativeAotILProvider();
        bool verbose = result.GetValueForOption(CommonOptions.VerbosityOption);
        var logger = new Logger(Console.Out, ilProvider, verbose, Array.Empty<int>(), singleWarn: false, Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>());

        BuildTargetType buildTargetType = result.GetValueForOption(CommonOptions.TargetOption);
        string compiledModuleName = Path.GetFileName(outputNameWithoutSuffix);

        PerfWatch createCompilationWatch = new PerfWatch("Create IL compilation");
        CSharpCompilation sourceCompilation = ILBuildCommand.CreateCompilation(compiledModuleName, inputFiles, references, defines, optimizationLevel, buildTargetType, targetArchitecture, targetOS);
        createCompilationWatch.Complete();

        PerfWatch getEntryPointWatch = new PerfWatch("GetEntryPoint");
        bool nativeLib = sourceCompilation.GetEntryPoint(CancellationToken.None) == null;
        getEntryPointWatch.Complete();
        if (buildTargetType == 0)
            buildTargetType = nativeLib ? BuildTargetType.Shared : BuildTargetType.Exe;

        var emitOptions = new EmitOptions(debugInformationFormat: DebugInformationFormat.Embedded);

        var ms = new MemoryStream();
        PerfWatch emitWatch = new PerfWatch("C# compiler emit");
        var resinfos = CommonOptions.GetResourceDescriptions(result.GetValueForOption(CommonOptions.ResourceOption));
        var compResult = sourceCompilation.Emit(ms, manifestResources: resinfos, options: emitOptions);
        emitWatch.Complete();
        if (!compResult.Success)
        {
            IEnumerable<Diagnostic> failures = compResult.Diagnostics.Where(diagnostic =>
                diagnostic.IsWarningAsError ||
                diagnostic.Severity == DiagnosticSeverity.Error);

            foreach (Diagnostic diagnostic in failures)
            {
                Console.Error.WriteLine(diagnostic.ToString());
            }

            return 1;
        }
        ms.Seek(0, SeekOrigin.Begin);

        InstructionSetSupportBuilder instructionSetSupportBuilder = new InstructionSetSupportBuilder(targetArchitecture);

        // The runtime expects certain baselines that the codegen can assume as well.
        if ((targetArchitecture == TargetArchitecture.X86) || (targetArchitecture == TargetArchitecture.X64))
        {
            instructionSetSupportBuilder.AddSupportedInstructionSet("sse2");
        }
        else if (targetArchitecture == TargetArchitecture.ARM64)
        {
            instructionSetSupportBuilder.AddSupportedInstructionSet("neon");
        }

        instructionSetSupportBuilder.ComputeInstructionSetFlags(out var supportedInstructionSet, out var unsupportedInstructionSet,
            (string specifiedInstructionSet, string impliedInstructionSet) =>
                throw new Exception(String.Format("Unsupported combination of instruction sets: {0}/{1}", specifiedInstructionSet, impliedInstructionSet)));

        InstructionSetSupportBuilder optimisticInstructionSetSupportBuilder = new InstructionSetSupportBuilder(targetArchitecture);

        // Optimistically assume some instruction sets are present.
        if ((targetArchitecture == TargetArchitecture.X86) || (targetArchitecture == TargetArchitecture.X64))
        {
            // We set these hardware features as enabled always, as most
            // of hardware in the wild supports them. Note that we do not indicate support for AVX, or any other
            // instruction set which uses the VEX encodings as the presence of those makes otherwise acceptable
            // code be unusable on hardware which does not support VEX encodings, as well as emulators that do not
            // support AVX instructions.
            //
            // The compiler is able to generate runtime IsSupported checks for the following instruction sets.
            optimisticInstructionSetSupportBuilder.AddSupportedInstructionSet("sse4.2");
            optimisticInstructionSetSupportBuilder.AddSupportedInstructionSet("aes");
            optimisticInstructionSetSupportBuilder.AddSupportedInstructionSet("pclmul");
            optimisticInstructionSetSupportBuilder.AddSupportedInstructionSet("movbe");
            optimisticInstructionSetSupportBuilder.AddSupportedInstructionSet("popcnt");
            optimisticInstructionSetSupportBuilder.AddSupportedInstructionSet("lzcnt");

            // If AVX was enabled, we can opportunistically enable FMA/BMI/VNNI
            Debug.Assert(InstructionSet.X64_AVX == InstructionSet.X86_AVX);
            if (supportedInstructionSet.HasInstructionSet(InstructionSet.X64_AVX))
            {
                optimisticInstructionSetSupportBuilder.AddSupportedInstructionSet("fma");
                optimisticInstructionSetSupportBuilder.AddSupportedInstructionSet("bmi");
                optimisticInstructionSetSupportBuilder.AddSupportedInstructionSet("bmi2");
                optimisticInstructionSetSupportBuilder.AddSupportedInstructionSet("avxvnni");
            }
        }
        else if (targetArchitecture == TargetArchitecture.ARM64)
        {
            optimisticInstructionSetSupportBuilder.AddSupportedInstructionSet("aes");
            optimisticInstructionSetSupportBuilder.AddSupportedInstructionSet("crc");
            optimisticInstructionSetSupportBuilder.AddSupportedInstructionSet("sha1");
            optimisticInstructionSetSupportBuilder.AddSupportedInstructionSet("sha2");
            optimisticInstructionSetSupportBuilder.AddSupportedInstructionSet("lse");
            optimisticInstructionSetSupportBuilder.AddSupportedInstructionSet("rcpc");
        }

        optimisticInstructionSetSupportBuilder.ComputeInstructionSetFlags(out var optimisticInstructionSet, out _,
            (string specifiedInstructionSet, string impliedInstructionSet) => throw new NotSupportedException());
        optimisticInstructionSet.Remove(unsupportedInstructionSet);
        optimisticInstructionSet.Add(supportedInstructionSet);

        var instructionSetSupport = new InstructionSetSupport(supportedInstructionSet,
                                                              unsupportedInstructionSet,
                                                              optimisticInstructionSet,
                                                              InstructionSetSupportBuilder.GetNonSpecifiableInstructionSetsForArch(targetArchitecture),
                                                              targetArchitecture);

        bool disableReflection = result.GetValueForOption(NoReflectionOption);
        bool disableStackTraceData = result.GetValueForOption(NoStackTraceDataOption) || bare;
        string systemModuleName = DefaultSystemModule;
        if (bare && references.Length == 0)
            systemModuleName = compiledModuleName;

        bool supportsReflection = !disableReflection && systemModuleName == DefaultSystemModule;

        //
        // Initialize type system context
        //

        SharedGenericsMode genericsMode = SharedGenericsMode.CanonicalReferenceTypes;

        var simdVectorLength = instructionSetSupport.GetVectorTSimdVector();
        var targetAbi = TargetAbi.NativeAot;
        var targetDetails = new TargetDetails(targetArchitecture, targetOS, targetAbi, simdVectorLength);
        CompilerTypeSystemContext typeSystemContext =
            new BflatTypeSystemContext(targetDetails, genericsMode, supportsReflection ? DelegateFeature.All : 0, ms, compiledModuleName);

        var referenceFilePaths = new Dictionary<string, string>();

        foreach (var reference in references)
        {
            referenceFilePaths[Path.GetFileNameWithoutExtension(reference)] = reference;
        }

        string homePath = CommonOptions.HomePath;
        string libPath = Environment.GetEnvironmentVariable("BFLAT_LIB");
        if (libPath == null)
        {
            char separator = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ';' : ':';

            string osPart = targetOS switch
            {
                TargetOS.Linux => "linux-glibc",
                TargetOS.Windows => "windows",
                _ => throw new Exception(targetOS.ToString()),
            };

            string archPart = targetArchitecture switch
            {
                TargetArchitecture.ARM64 => "arm64",
                TargetArchitecture.X64 => "x64",
                _ => throw new Exception(targetArchitecture.ToString()),
            };

            string osArchPath = Path.Combine(homePath, "lib", $"{osPart}-{archPart}");
            if (!Directory.Exists(osArchPath))
            {
                Console.Error.WriteLine($"Directory '{osArchPath}' doesn't exist.");
                return 1;
            }

            libPath = String.Concat(osArchPath, separator.ToString(), Path.Combine(homePath, "lib"));
        }

        if (!bare)
        {
            foreach (var reference in EnumerateExpandedDirectories(libPath, "*.dll"))
            {
                string assemblyName = Path.GetFileNameWithoutExtension(reference);
                referenceFilePaths[assemblyName] = reference;
            }
        }

        typeSystemContext.InputFilePaths = new Dictionary<string, string>();
        typeSystemContext.ReferenceFilePaths = referenceFilePaths;

        typeSystemContext.SetSystemModule(typeSystemContext.GetModuleForSimpleName(systemModuleName));
        EcmaModule compiledAssembly = typeSystemContext.GetModuleForSimpleName(compiledModuleName);

        //
        // Initialize compilation group and compilation roots
        //

        List<string> initAssemblies = new List<string> { "System.Private.CoreLib" };

        if (!disableReflection || !disableStackTraceData)
            initAssemblies.Add("System.Private.StackTraceMetadata");

        initAssemblies.Add("System.Private.TypeLoader");

        if (!disableReflection)
            initAssemblies.Add("System.Private.Reflection.Execution");
        else
            initAssemblies.Add("System.Private.DisabledReflection");

        // Build a list of assemblies that have an initializer that needs to run before
        // any user code runs.
        List<ModuleDesc> assembliesWithInitalizers = new List<ModuleDesc>();
        if (!bare)
        {
            foreach (string initAssemblyName in initAssemblies)
            {
                ModuleDesc assembly = typeSystemContext.GetModuleForSimpleName(initAssemblyName);
                assembliesWithInitalizers.Add(assembly);
            }
        }

        var libraryInitializers = new LibraryInitializers(typeSystemContext, assembliesWithInitalizers);

        List<MethodDesc> initializerList = new List<MethodDesc>(libraryInitializers.LibraryInitializerMethods);

        CompilationModuleGroup compilationGroup;
        List<ICompilationRootProvider> compilationRoots = new List<ICompilationRootProvider>();

        compilationRoots.Add(new ExportedMethodsRootProvider(compiledAssembly));

        if (!nativeLib)
        {
            compilationRoots.Add(new MainMethodRootProvider(compiledAssembly, initializerList));
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
            compilationRoots.Add(new NativeLibraryInitializerRootProvider(typeSystemContext.GeneratedAssembly, initializerList));
            compilationRoots.Add(new RuntimeConfigurationRootProvider(Array.Empty<string>()));
            compilationRoots.Add(new ExpectedIsaFeaturesRootProvider(instructionSetSupport));
        }

        //
        // Compile
        //

        CompilationBuilder builder = new RyuJitCompilationBuilder(typeSystemContext, compilationGroup);

        builder.UseCompilationUnitPrefix("");

        List<string> directPinvokeList = new List<string>();
        List<string> directPinvokes = new List<string>(result.GetValueForOption(DirectPInvokesOption));
        if (targetOS == TargetOS.Windows)
        {
            directPinvokeList.Add(Path.Combine(homePath, "WindowsAPIs.txt"));
            directPinvokes.Add("System.IO.Compression.Native");
            directPinvokes.Add("System.Globalization.Native");
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

        var featureSwitches = new Dictionary<string, bool>()
        {
            { "System.Diagnostics.Debugger.IsSupported", false },
            { "System.Diagnostics.Tracing.EventSource.IsSupported", false },
            { "System.Runtime.Serialization.EnableUnsafeBinaryFormatterSerialization", false },
            { "System.Resources.ResourceManager.AllowCustomResourceTypes", false },
            { "System.Text.Encoding.EnableUnsafeUTF7Encoding", false },
            { "System.Runtime.Serialization.DataContractSerializer.IsReflectionOnly", true },
            { "System.Xml.Serialization.XmlSerializer.IsReflectionOnly", true },
            { "System.Xml.XmlDownloadManager.IsNonFileStreamSupported", false },
            { "System.Linq.Expressions.CanCompileToIL", false },
            { "System.Linq.Expressions.CanEmitObjectArrayDelegate", false },
            { "System.Linq.Expressions.CanCreateArbitraryDelegates", false },
        };

        bool disableExceptionMessages = result.GetValueForOption(NoExceptionMessagesOption);
        if (disableExceptionMessages || disableReflection)
        {
            featureSwitches.Add("System.Resources.UseSystemResourceKeys", true);
        }

        bool disableGlobalization = result.GetValueForOption(NoGlobalizationOption);
        if (disableGlobalization)
        {
            featureSwitches.Add("System.Globalization.Invariant", true);
        }

        if (disableReflection)
        {
            featureSwitches.Add("System.Collections.Generic.DefaultComparers", false);
            featureSwitches.Add("System.Reflection.IsReflectionExecutionAvailable", false);
        }

        foreach (var featurePair in result.GetValueForOption(FeatureSwitchOption))
        {
            int index = featurePair.IndexOf('=');
            if (index <= 0 || index == featurePair.Length - 1)
                continue;

            string name = featurePair.Substring(0, index);
            bool value = featurePair.Substring(index + 1) != "false";
            featureSwitches[name] = value;
        }

        ilProvider = new FeatureSwitchManager(ilProvider, featureSwitches);

        var stackTracePolicy = !disableStackTraceData ?
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
        }
        else
        {
            mdBlockingPolicy = new FullyBlockedMetadataBlockingPolicy();
            resBlockingPolicy = new FullyBlockedManifestResourceBlockingPolicy();
        }
        DynamicInvokeThunkGenerationPolicy invokeThunkGenerationPolicy = new DefaultDynamicInvokeThunkGenerationPolicy();

        var compilerGenerateState = new ILCompiler.Dataflow.CompilerGeneratedState(ilProvider, logger);
        var flowAnnotations = new ILLink.Shared.TrimAnalysis.FlowAnnotations(logger, ilProvider, compilerGenerateState);

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
        bool useScanner = optimizationMode != OptimizationMode.None;

        // Enable static data preinitialization in optimized builds.
        bool preinitStatics = optimizationMode != OptimizationMode.None;

        var preinitManager = new PreinitializationManager(typeSystemContext, compilationGroup, ilProvider, preinitStatics);
        builder
            .UseILProvider(ilProvider)
            .UsePreinitializationManager(preinitManager)
        .UseResilience(true);

        ILScanResults scanResults = null;
        if (useScanner)
        {
            if (logger.IsVerbose)
                logger.LogMessage("Scanning input IL");
            ILScannerBuilder scannerBuilder = builder.GetILScannerBuilder()
                .UseCompilationRoots(compilationRoots)
                .UseMetadataManager(metadataManager)
                .UseInteropStubManager(interopStubManager)
                .UseLogger(logger);

            IILScanner scanner = scannerBuilder.ToILScanner();

            PerfWatch scanWatch = new PerfWatch("Scanner");
            scanResults = scanner.Scan();
            scanWatch.Complete();

            metadataManager = ((UsageBasedMetadataManager)metadataManager).ToAnalysisBasedMetadataManager();

            interopStubManager = scanResults.GetInteropStubManager(interopStateManager, pinvokePolicy);
        }

        DebugInformationProvider debugInfoProvider = new DebugInformationProvider();

        DependencyTrackingLevel trackingLevel = DependencyTrackingLevel.None;

        compilationRoots.Add(metadataManager);
        compilationRoots.Add(interopStubManager);
        builder
            .UseInstructionSetSupport(instructionSetSupport)
            .UseMethodBodyFolding(enable: optimizationMode != OptimizationMode.None)
            .UseMetadataManager(metadataManager)
            .UseInteropStubManager(interopStubManager)
            .UseLogger(logger)
            .UseDependencyTracking(trackingLevel)
            .UseCompilationRoots(compilationRoots)
            .UseOptimizationMode(optimizationMode)
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

            // Use an error provider that prevents us from re-importing methods that failed
            // to import with an exception during scanning phase. We would see the same failure during
            // compilation, but before RyuJIT gets there, it might ask questions that we don't
            // have answers for because we didn't scan the entire method.
            builder.UseMethodImportationErrorProvider(scanResults.GetMethodImportationErrorProvider());
        }

        ICompilation compilation = builder.ToCompilation();

        string outputFilePath = userSpecificedOutputFileName;
        if (outputFilePath == null)
        {
            outputFilePath = outputNameWithoutSuffix;
            if (targetOS == TargetOS.Windows)
            {
                if (buildTargetType is BuildTargetType.Exe or BuildTargetType.WinExe)
                    outputFilePath += ".exe";
                else
                    outputFilePath += ".dll";
            }
            else
            {
                if (buildTargetType is not BuildTargetType.Exe and not BuildTargetType.WinExe)
                {
                    outputFilePath += ".so";

                    outputFilePath = Path.Combine(
                        Path.GetDirectoryName(outputFilePath),
                        "lib" + Path.GetFileName(outputFilePath));
                }
            }
        }

        if (logger.IsVerbose)
            logger.LogMessage("Generating native code");
        string mapFileName = result.GetValueForOption(MapFileOption);
        ObjectDumper dumper = mapFileName != null ? new XmlObjectDumper(mapFileName) : null;
        string objectFilePath = Path.ChangeExtension(outputFilePath, targetOS == TargetOS.Windows ? ".obj" : ".o");

        PerfWatch compileWatch = new PerfWatch("Native compile");
        CompilationResults compilationResults = compilation.Compile(objectFilePath, dumper);
        compileWatch.Complete();

        string exportsFile = null;
        if (nativeLib)
        {
            exportsFile = Path.ChangeExtension(outputFilePath, targetOS == TargetOS.Windows ? ".def" : ".txt");
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

        if (result.GetValueForOption(NoLinkOption))
        {
            return 0;
        }

        //
        // Run the platform linker
        //

        if (logger.IsVerbose)
            logger.LogMessage("Running the linker");

        string ld = Environment.GetEnvironmentVariable("BFLAT_LD");
        if (ld == null)
        {
            string toolSuffix = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".exe" : "";

            ld = Path.Combine(homePath, "bin", "lld" + toolSuffix);
        }

        bool deterministic = result.GetValueForOption(CommonOptions.DeterministicOption);
        string libc = result.GetValueForOption(TargetLibcOption);

        var ldArgs = new StringBuilder();

        if (targetOS == TargetOS.Windows)
        {
            ldArgs.Append("-flavor link \"");
            ldArgs.Append(objectFilePath);
            ldArgs.Append("\" ");
            ldArgs.AppendFormat("/out:\"{0}\" ", outputFilePath);
            if (deterministic)
                ldArgs.Append("/Brepro ");

            foreach (var lpath in libPath.Split(RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ';' : ':'))
            {
                ldArgs.AppendFormat("/libpath:\"{0}\" ", lpath);
            }

            if (buildTargetType == BuildTargetType.Exe)
                ldArgs.Append("/subsystem:console ");
            if (buildTargetType == BuildTargetType.WinExe)
                ldArgs.Append("/subsystem:windows ");

            if (buildTargetType is BuildTargetType.Exe or BuildTargetType.WinExe)
            {
                if (!bare)
                    ldArgs.Append("/entry:wmainCRTStartup bootstrapper.lib ");
                else
                    ldArgs.Append("/entry:__managed__Main ");
            }
            else if (buildTargetType is BuildTargetType.Shared)
            {
                ldArgs.Append("/dll ");
                if (!bare)
                    ldArgs.Append("/include:NativeAOT_StaticInitialization bootstrapperdll.lib ");
                ldArgs.Append($"/def:\"{exportsFile}\" ");
            }

            ldArgs.Append("/incremental:no ");
            ldArgs.Append("/debug ");
            if (!bare)
            {
                ldArgs.Append("Runtime.WorkstationGC.lib System.IO.Compression.Native.Aot.lib System.Globalization.Native.Aot.lib ");
            }
            ldArgs.Append("sokol.lib advapi32.lib bcrypt.lib crypt32.lib iphlpapi.lib kernel32.lib mswsock.lib ncrypt.lib normaliz.lib  ntdll.lib ole32.lib oleaut32.lib user32.lib version.lib ws2_32.lib shell32.lib Secur32.Lib ");

            if (libc != "none")
            {
                ldArgs.Append("shcrt.lib ");
                ldArgs.Append("api-ms-win-crt-conio-l1-1-0.lib api-ms-win-crt-convert-l1-1-0.lib api-ms-win-crt-environment-l1-1-0.lib ");
                ldArgs.Append("api-ms-win-crt-filesystem-l1-1-0.lib api-ms-win-crt-heap-l1-1-0.lib api-ms-win-crt-locale-l1-1-0.lib ");
                ldArgs.Append("api-ms-win-crt-multibyte-l1-1-0.lib api-ms-win-crt-math-l1-1-0.lib ");
                ldArgs.Append("api-ms-win-crt-process-l1-1-0.lib api-ms-win-crt-runtime-l1-1-0.lib api-ms-win-crt-stdio-l1-1-0.lib ");
                ldArgs.Append("api-ms-win-crt-string-l1-1-0.lib api-ms-win-crt-time-l1-1-0.lib api-ms-win-crt-utility-l1-1-0.lib ");
            }
            ldArgs.Append("/opt:ref,icf /nodefaultlib:libcpmt.lib ");
        }
        else if (targetOS == TargetOS.Linux)
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
            if (buildTargetType != BuildTargetType.Shared)
            {
                ldArgs.Append("-dynamic-linker /lib64/ld-linux-x86-64.so.2 ");
                ldArgs.Append($"\"{firstLib}/Scrt1.o\" ");
                if (bare)
                    ldArgs.Append("--defsym=main=__managed__Main ");
            }

            ldArgs.AppendFormat("-o \"{0}\" ", outputFilePath);

            ldArgs.Append($"\"{firstLib}/crti.o\" ");
            ldArgs.Append($"\"{firstLib}/crtbeginS.o\" ");

            ldArgs.Append('"');
            ldArgs.Append(objectFilePath);
            ldArgs.Append('"');
            ldArgs.Append(' ');
            ldArgs.Append("--as-needed --discard-all --gc-sections ");
            ldArgs.Append("-rpath \"$ORIGIN\" ");

            if (buildTargetType == BuildTargetType.Shared)
            {
                if (!bare)
                {
                    ldArgs.Append("-lbootstrapperdll ");
                    ldArgs.Append("--undefined=NativeAOT_StaticInitialization ");
                }

                ldArgs.Append("-shared ");
                ldArgs.Append($"--version-script=\"{exportsFile}\" ");
            }
            else
            {
                if (!bare)
                    ldArgs.Append("-lbootstrapper ");
                ldArgs.Append("-pie ");
            }

            if (!bare)
                ldArgs.Append("-lRuntime.WorkstationGC -lSystem.Native -lSystem.Globalization.Native -lSystem.IO.Compression.Native -lSystem.Net.Security.Native -lSystem.Security.Cryptography.Native.OpenSsl ");

            ldArgs.Append("--as-needed -lstdc++ -ldl -lm -lz -lgssapi_krb5 -lrt -z relro -z now --discard-all --gc-sections -lgcc --as-needed -lgcc_s --no-as-needed -lpthread -lc -lgcc --as-needed -lgcc_s ");
            ldArgs.Append($"\"{firstLib}/crtendS.o\" ");
            ldArgs.Append($"\"{firstLib}/crtn.o\" ");
        }

        ldArgs.AppendJoin(' ', result.GetValueForOption(LdFlagsOption));

        if (result.GetValueForOption(PrintCommandsOption))
        {
            Console.WriteLine($"{ld} {ldArgs}");
        }

        PerfWatch linkWatch = new PerfWatch("Link");
        var p = Process.Start(ld, ldArgs.ToString());
        p.WaitForExit();
        linkWatch.Complete();

        int linkerExitCode = p.ExitCode;

        try { File.Delete(objectFilePath); } catch { }
        if (exportsFile != null)
            try { File.Delete(exportsFile); } catch { }

        return linkerExitCode;
    }
}
