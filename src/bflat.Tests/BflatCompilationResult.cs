using System;
using System.Diagnostics;
using Xunit;

namespace bflat.Tests
{
    internal record BflatCompilationResult(string BinaryName, string StdErr, string StdOut)
    {
        public void Run(string expectedOutput = null)
        {
            var psi = new ProcessStartInfo(BinaryName)
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
            
            if (expectedOutput != null)
            {
                Assert.Equal(expectedOutput, stdOut);
            }

            Assert.Equal(0, p.ExitCode);
        }

    }
}
