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

using Internal.Runtime.CompilerHelpers;

namespace System
{
    public static unsafe partial class Console
    {
        public static unsafe void Write(char c)
        {
            int cc = c;
            EFI_SYSTEM_TABLE* tbl = StartupCodeHelpers.s_efiSystemTable;
            tbl->ConOut->OutputString(tbl->ConOut, (char*)&cc);
        }

        public static unsafe ConsoleColor ForegroundColor
        {
            set
            {
                EFI_SYSTEM_TABLE* tbl = StartupCodeHelpers.s_efiSystemTable;
                tbl->ConOut->SetAttribute(tbl->ConOut, (uint)value);
            }
        }

        public static void SetCursorPosition(int x, int y)
        {
            EFI_SYSTEM_TABLE* tbl = StartupCodeHelpers.s_efiSystemTable;
            tbl->ConOut->SetCursorPosition(
                tbl->ConOut,
                (uint)x,
                (uint)y);
        }

        static char s_keyBuffer;
        static ushort s_scanCodeBuffer;

        private static unsafe bool FillBuffer()
        {
            if (s_scanCodeBuffer == 0)
            {
                EFI_INPUT_KEY key;
                EFI_SYSTEM_TABLE* tbl = StartupCodeHelpers.s_efiSystemTable;
                if (tbl->ConIn->ReadKeyStroke(tbl->ConIn, &key) == 0)
                {
                    s_keyBuffer = (char)key.UnicodeChar;
                    s_scanCodeBuffer = key.ScanCode;
                    return true;
                }
                return false;
            }
            return true;
        }

        public static bool KeyAvailable => FillBuffer();

        public static ConsoleKeyInfo ReadKey(bool intercept)
        {
            if (FillBuffer())
            {
                ConsoleKey key = s_scanCodeBuffer switch
                {
                    1 => ConsoleKey.UpArrow,
                    2 => ConsoleKey.DownArrow,
                    3 => ConsoleKey.RightArrow,
                    4 => ConsoleKey.LeftArrow,
                    _ => default(ConsoleKey),
                };
                s_scanCodeBuffer = 0;
                return new ConsoleKeyInfo(s_keyBuffer, key, false, false, false);
            }
            return default;
        }

        public static unsafe void SetWindowSize(int x, int y)
        {
        }

        public static void SetBufferSize(int x, int y)
        {
        }

        public static unsafe string Title
        {
            set
            {
                _ = value;
            }
        }

        public static unsafe bool CursorVisible
        {
            set
            {
                _ = value;
            }
        }
    }
}

#endif
