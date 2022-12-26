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

#if LINUX

using System;
using System.Runtime;
using System.Runtime.InteropServices;

namespace Internal.Runtime.CompilerHelpers
{
    unsafe partial class StartupCodeHelpers
    {
        static int s_argc;
        static sbyte** s_argv;

        internal static unsafe void InitializeCommandLineArgs(int argc, sbyte** argv)
        {
            s_argc = argc;
            s_argv = argv;
        }

        private static string[] GetMainMethodArguments()
        {
            string[] args = new string[s_argc - 1];
            for (int i = 1; i < s_argc; ++i)
            {
                args[i - 1] = new string(s_argv[i]);
            }

            return args;
        }
    }
}

#endif
