using Internal.Runtime.CompilerHelpers;

namespace System
{
    public static partial class Environment
    {
        // This gives an uniform way to get to the command line args
        // in all environments and scenarios.
        public static string[] GetCommandLineArgs ()
            => StartupCodeHelpers.GetMainMethodArguments();
    }
}