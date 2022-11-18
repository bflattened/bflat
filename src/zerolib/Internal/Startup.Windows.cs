// bflat minimal runtime library
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

#if WINDOWS

using System;
using System.Runtime;
using System.Runtime.InteropServices;

namespace Internal.Runtime.CompilerHelpers
{
    unsafe partial class StartupCodeHelpers
    {
        internal static unsafe void InitializeCommandLineArgsW(int argc, char** argv)
        {
            // argc and argv are a lie because CRT didn't start the process on Windows
        }

        private static string[] GetMainMethodArguments()
        {
            int argc;
            char** argv = CommandLineToArgvW(GetCommandLineW(), &argc);

            [DllImport("kernel32")]
            static extern char* GetCommandLineW();

            [DllImport("shell32")]
            static extern char** CommandLineToArgvW(char* lpCmdLine, int* pNumArgs);

            string[] args = new string[argc - 1];
            for (int i = 1; i < argc; ++i)
            {
                args[i - 1] = new string(argv[i]);
            }

            return args;
        }
    }
}

#endif
