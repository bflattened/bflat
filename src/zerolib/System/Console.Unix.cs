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

using System.Runtime.InteropServices;

namespace System
{
    public static unsafe partial class Console
    {
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

        public static unsafe void SetWindowSize(int x, int y)
        {
        }

        public static void SetBufferSize(int x, int y)
        {
        }

        public static ConsoleColor ForegroundColor
        {
            set
            {
                ConsoleColor adjusted = value;
                if (value >= ConsoleColor.DarkBlue && value <= ConsoleColor.Gray)
                    adjusted = value - ConsoleColor.DarkBlue + ConsoleColor.Blue;
                int colorCode = adjusted switch
                {
                    ConsoleColor.Black => 30,
                    ConsoleColor.Blue => 34,
                    ConsoleColor.Green => 32,
                    ConsoleColor.Cyan => 36,
                    ConsoleColor.Red => 31,
                    ConsoleColor.Magenta => 35,
                    ConsoleColor.Yellow => 33,
                    _ => 37,
                };
                byte* pBuf = stackalloc byte[16];
                pBuf[0] = 0x1B;
                pBuf[1] = (byte)'[';
                byte* pCur = Append(&pBuf[2], colorCode);
                *pCur++ = (byte)'m';
                SystemNative_Log(pBuf, (int)(pCur - pBuf));
            }
        }

        public static void SetCursorPosition(int x, int y)
        {
            byte* pBuf = stackalloc byte[32];
            pBuf[0] = 0x1B;
            pBuf[1] = (byte)'[';
            byte* cur = Append(&pBuf[2], y);
            *cur++ = (byte)';';
            cur = Append(cur, x);
            *cur++ = (byte)'H';
            SystemNative_Log(pBuf, (int)(cur - pBuf));
        }

        static byte* Append(byte* b, int x)
        {
            if (x >= 10)
                b = Append(b, x / 10);
            *b = (byte)((x % 10) + '0');
            return ++b;
        }

        [DllImport("libSystem.Native")]
        private static extern void SystemNative_Log(void* pBuffer, int length);

        public static void Write(char c)
        {
            if (c <= 0x7F)
            {
                SystemNative_Log(&c, 1);
            }
            else if (c <= 0x7FF)
            {
                ushort twoByte;
                byte* pOut = (byte*)&twoByte;
                *pOut++ = (byte)(0xC0 | (c >> 6));
                *pOut++ = (byte)(0x80 | (c & 0x3F));
                SystemNative_Log(&twoByte, 2);
            }
            else
            {
                int threeByte;
                byte* pOut = (byte*)&threeByte;
                *pOut++ = (byte)(0xE0 | (c >> 12));
                *pOut++ = (byte)(0x80 | ((c >> 6) & 0x3F));
                *pOut++ = (byte)(0x80 | (c & 0x3F));
                SystemNative_Log(&threeByte, 3);
            }
        }

        public static bool KeyAvailable => StdInReader.KeyAvailable();

        public static ConsoleKeyInfo ReadKey(bool intercept) => StdInReader.ReadKey();

        static class StdInReader
        {
            static StdInReader()
            {
                SystemNative_InitializeTerminalAndSignalHandling();

                [DllImport("libSystem.Native")]
                static extern int SystemNative_InitializeTerminalAndSignalHandling();
            }

            private static StdInBuffer s_buffer;
            
            struct StdInBuffer
            {
                public const int Size = 256;
                public int StartIndex;
                public int EndIndex;
                public bool Empty => StartIndex >= EndIndex;
                public fixed char Chars[Size];
            }

            public static bool KeyAvailable()
            {
                return SystemNative_StdinReady(1) != 0;

                [DllImport("libSystem.Native")]
                static extern int SystemNative_StdinReady(int distinguishNewLines);
            }

            public static ConsoleKeyInfo ReadKey()
            {
                SystemNative_InitializeConsoleBeforeRead(0, 1, 0);

                [DllImport("libSystem.Native")]
                static extern void SystemNative_InitializeConsoleBeforeRead(int distinguishNewLines, byte minChars, byte decisecondsTimeout);

                try
                {
                    if (s_buffer.Empty)
                    {
                        byte* bufPtr = stackalloc byte[StdInBuffer.Size];
                        int result = SystemNative_ReadStdin(bufPtr, StdInBuffer.Size);

                        [DllImport("libSystem.Native")]
                        static extern unsafe int SystemNative_ReadStdin(byte* buffer, int bufferSize);

                        if (result <= 0)
                        {
                            return default;
                        }
                        s_buffer.StartIndex = 0;
                        s_buffer.EndIndex = result;
                        for (int i = 0; i < result; i++)
                        {
                            char c = (char)bufPtr[i];
                            if (c > 0x7F)
                                Environment.FailFast(null);
                            s_buffer.Chars[i] = c;
                        }
                    }

                    if (s_buffer.EndIndex - s_buffer.StartIndex >= 3 &&
                        s_buffer.Chars[s_buffer.StartIndex] == 0x1B &&
                        s_buffer.Chars[s_buffer.StartIndex + 1] == '[')
                    {
                        char code = s_buffer.Chars[s_buffer.StartIndex + 2];
                        s_buffer.StartIndex += 3;
                        switch (code)
                        {
                            case 'A':
                                return new ConsoleKeyInfo(default, ConsoleKey.UpArrow, false, false, false);
                            case 'B':
                                return new ConsoleKeyInfo(default, ConsoleKey.DownArrow, false, false, false);
                            case 'C':
                                return new ConsoleKeyInfo(default, ConsoleKey.RightArrow, false, false, false);
                            case 'D':
                                return new ConsoleKeyInfo(default, ConsoleKey.LeftArrow, false, false, false);
                            default:
                                Environment.FailFast(null);
                                return default;
                        }
                    }

                    return default;
                    
                }
                finally
                {
                    SystemNative_UninitializeConsoleAfterRead();

                    [DllImport("libSystem.Native")]
                    static extern void SystemNative_UninitializeConsoleAfterRead();
                }
            }
        }
    }
}

#endif
