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

#if UEFI

using System.Runtime.InteropServices;
using Internal.Runtime.CompilerHelpers;

namespace System
{
    public static partial class Environment
    {
        public unsafe static void FailFast(string message)
        {
            EFI_SYSTEM_TABLE* tbl = StartupCodeHelpers.s_efiSystemTable;
            fixed (char* pMessage = message)
            {
                tbl->ConOut->OutputString(tbl->ConOut, pMessage);
            }
            while (true) ;
        }

        static internal long s_lastTickCount;
        static internal long s_stallSinceLastTickCount;

        public static unsafe long TickCount64
        {
            get
            {
                EFI_SYSTEM_TABLE* tbl = StartupCodeHelpers.s_efiSystemTable;
                EFI_TIME time;
                tbl->RuntimeServices->GetTime(&time, null);
                long days = time.Year * 365 + time.Month * 31 + time.Day;
                long seconds = days * 24 * 60 * 60 + time.Hour * 60 * 60 + time.Minute * 60 + time.Second;
                long milliseconds = seconds * 1000 + time.Nanosecond / 1000000;

                // HACK: some systems will report a zero Nanosecond part. We keep track of Stall getting
                // previously called with the same tickcount and artificially inflate the tick count
                // by the amount of stall if it occured within the same TickCount.
                if (s_lastTickCount == milliseconds)
                {
                    milliseconds += s_stallSinceLastTickCount;
                }
                else
                {
                    s_lastTickCount = milliseconds;
                    s_stallSinceLastTickCount = 0;
                }
                return milliseconds;
            }
        }
    }
}

#endif
