using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace bflat.Tests
{
    internal class BflatCompilation
    {
        private string _exeExtension;
        private readonly string _compilerPath;

        public BflatCompilation()
            : this(RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "windows" : "linux-glibc",
                RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant())
        { }

        public BflatCompilation(string osPart, string archPart)
        {
            _compilerPath = FindCompiler(osPart, archPart);
            _exeExtension = osPart == "windows" ? ".exe" : "";
        }

        public BflatCompilationResult Build(string source, string arguments = null)
        {
            string fileWithoutExtension = Path.GetTempFileName();
            File.WriteAllText(fileWithoutExtension + ".cs", source);

            StringBuilder argBuilder = new StringBuilder("build ")
                .Append(arguments)
                .Append(" \"")
                .Append(fileWithoutExtension)
                .Append(".cs\" ")
                .Append("-o:\"")
                .Append(fileWithoutExtension)
                .Append(_exeExtension)
                .Append("\" ");

            var psi = new ProcessStartInfo(_compilerPath, argBuilder.ToString())
            {
                RedirectStandardError = true,
                RedirectStandardOutput = true,
            };

            var p = Process.Start(psi);
            if (!p.WaitForExit(30000))
            {
                p.Kill(entireProcessTree: true);
                throw new Exception("Timed out");
            }

            string stdOut = p.StandardOutput.ReadToEnd();
            string stdErr = p.StandardError.ReadToEnd();

            if (p.ExitCode != 0)
            {
                throw new Exception($"Non-zero exit code\nStdout:\n{stdOut}\nStderr:\n{stdErr}");
            }

            return new BflatCompilationResult(fileWithoutExtension + _exeExtension, stdErr, stdOut);
        }

        private static string FindCompiler(string os, string arch)
        {
            string compilerTuple = $"{os}-{arch}";

            string compilerName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "bflat.exe" : "bflat";

            string currentPath = Path.GetDirectoryName(Environment.ProcessPath);
            string root = Path.GetPathRoot(currentPath);

            while (currentPath != root)
            {
                string candidate = Path.Combine(currentPath, "layouts", compilerTuple, compilerName);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
                currentPath = Path.GetDirectoryName(currentPath);
            }

            throw new Exception("Compiler not found. Did you build the layouts before running tests?");
        }
    }
}
