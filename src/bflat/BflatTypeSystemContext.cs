// bflat C# compiler
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

using System.IO;
using System.IO.MemoryMappedFiles;
using System.Reflection.PortableExecutable;

using Internal.IL;
using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;

using ILCompiler;

class BflatTypeSystemContext : CompilerTypeSystemContext
{
    public unsafe BflatTypeSystemContext(TargetDetails details, SharedGenericsMode genericsMode, DelegateFeature delegateFeatures, MemoryStream compiledModule, string compiledModuleName)
        : base(details, genericsMode, delegateFeatures)
    {
        var mappedFile = MemoryMappedFile.CreateNew(mapName: null, compiledModule.Length);
        var vs = mappedFile.CreateViewStream();
        compiledModule.CopyTo(vs);
        compiledModule.Dispose();
        vs.Dispose();

        var accessor = mappedFile.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);

        var safeBuffer = accessor.SafeMemoryMappedViewHandle;
        var peReader = new PEReader((byte*)safeBuffer.DangerousGetHandle(), (int)safeBuffer.ByteLength);

        var pdbReader = PortablePdbSymbolReader.TryOpenEmbedded(peReader, GetMetadataStringDecoder());

        CacheOpenModule(compiledModuleName, compiledModuleName, EcmaModule.Create(this, peReader, null, pdbReader), accessor);
    }
}
