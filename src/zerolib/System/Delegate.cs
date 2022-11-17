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

namespace System
{
    public abstract class Delegate
    {
        internal object _firstParameter;
        internal object _helperObject;
        internal nint _extraFunctionPointerOrData;
        internal IntPtr _functionPointer;

        private void InitializeClosedStaticThunk(object firstParameter, IntPtr functionPointer, IntPtr functionPointerThunk)
        {
            _extraFunctionPointerOrData = functionPointer;
            _helperObject = firstParameter;
            _functionPointer = functionPointerThunk;
            _firstParameter = this;
        }

        private void InitializeOpenStaticThunk(object firstParameter, IntPtr functionPointer, IntPtr functionPointerThunk)
        {
            _firstParameter = this;
            _functionPointer = functionPointerThunk;
            _extraFunctionPointerOrData = functionPointer;
        }

        private void InitializeClosedInstance(object firstParameter, IntPtr functionPointer)
        {
            _functionPointer = functionPointer;
            _firstParameter = firstParameter;
        }
    }

    public abstract class MulticastDelegate : Delegate { }
}
