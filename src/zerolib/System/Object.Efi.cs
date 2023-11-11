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
    public unsafe partial class Object
    {
        private static void* s_efiSystemTable;

        internal static EFI_SYSTEM_TABLE* EfiSystemTable => (EFI_SYSTEM_TABLE*)s_efiSystemTable;
        internal static void SetEfiSystemTable(EFI_SYSTEM_TABLE* t) => s_efiSystemTable = t;
    }
}

#endif
