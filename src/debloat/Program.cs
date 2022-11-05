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

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

Merge(args[0]);

static void Merge(string directory)
{
    foreach (string subDirectory in Directory.EnumerateDirectories(directory))
        Merge(subDirectory);

    Dictionary<string, List<string>> candidates = new Dictionary<string, List<string>>();
    int expectedFiles = 0;
    foreach (string subDirectory in Directory.EnumerateDirectories(directory))
    {
        expectedFiles++;
        foreach (var f in Directory.EnumerateFiles(subDirectory, "*.dll"))
        {
            string key = Path.GetFileName(f);
            if (!candidates.TryGetValue(key, out List<string> list))
                candidates.Add(key, list = new List<string>());
            list.Add(f);
        }
    }

    foreach (var maybeSameFiles in candidates.Values)
    {
        if (maybeSameFiles.Count != expectedFiles)
            continue;

        bool allSame = true;
        ReadOnlySpan<byte> expected = SanitizedAssemblyBytes(maybeSameFiles[0]);
        for (int i = 1; i < maybeSameFiles.Count; i++)
        {
            ReadOnlySpan<byte> actual = SanitizedAssemblyBytes(maybeSameFiles[i]);
            if (!expected.SequenceEqual(actual))
            {
                allSame = false;
                break;
            }
        }

        if (allSame)
        {
            File.Move(maybeSameFiles[0], Path.Combine(directory, Path.GetFileName(maybeSameFiles[0])));
            string pdbFile = Path.ChangeExtension(maybeSameFiles[0], "pdb");
            if (File.Exists(pdbFile))
                File.Move(pdbFile, Path.Combine(directory, Path.GetFileName(pdbFile)));

            for (int i = 1; i < maybeSameFiles.Count; i++)
            {
                File.Delete(maybeSameFiles[i]);
                pdbFile = Path.ChangeExtension(maybeSameFiles[i], "pdb");
                if (File.Exists(pdbFile))
                    File.Delete(pdbFile);
            }
        }
    }
}

static byte[] SanitizedAssemblyBytes(string fileName)
{
    byte[] b = File.ReadAllBytes(fileName);
    Span<byte> span = b;
    PEReader perdr = new PEReader(ImmutableArray.Create(b));
    span.Slice(perdr.PEHeaders.CoffHeaderStartOffset + 4, 4).Clear();
    MetadataReader mdrdr = perdr.GetMetadataReader();
    Guid mvid = mdrdr.GetGuid(mdrdr.GetModuleDefinition().Mvid);
    span.Slice(span.IndexOf(mvid.ToByteArray()), 16).Clear();
    foreach (var ddentry in perdr.ReadDebugDirectory())
        span.Slice(ddentry.DataPointer, ddentry.DataSize).Clear();
    if (perdr.PEHeaders.TryGetDirectoryOffset(perdr.PEHeaders.PEHeader.DebugTableDirectory, out int debugDir))
        span.Slice(debugDir, perdr.PEHeaders.PEHeader.DebugTableDirectory.Size).Clear();
    return b;
}