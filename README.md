# bflat
C# as you know it but with Go-inspired tooling that produces small, selfcontained, and native executables out of the box.

```console
$ echo 'System.Console.WriteLine("Hello World");' > hello.cs
$ bflat build hello.cs
$ ./hello
Hello World
$ bflat build hello.cs --os:windows
$ file ./hello.exe
hello.exe: PE32+ executable (console) x86-64, for MS Windows
```

## 🎻 What exactly is bflat

bflat is a concoction of Roslyn - the "official" C# compiler that produces .NET executables - and NativeAOT (née CoreRT) - the experimental ahead of time compiler for .NET based on CoreCLR's crossgen2. Thanks to this, you get access to the latest C# features using the high performance CoreCLR GC and native code generator (RyuJIT).

bflat merges the two components together into a single ahead of time crosscompiler and runtime for C#.

bflat can currently target:

* x64 Linux
* x64 and ARM64 Windows

It can either produce native executables, or native shared libraries that can be called from other languages through FFI.

## 🥁 Where to get bflat

Look at the Releases tab of this repo and download a compiler that matches your host system. These are all crosscompilers and can target any of the supported OSes/architectures.

Unzip the archive to a convenient location and add the root to your PATH. You're all set. See the samples directory for a couple samples.

## 🎷 I don't see dotnet, MSBuild, or NuGet

That's the point. bflat is to dotnet as VS Code is to VS.

## 🎙 Where is the source code

The source code is in the respective Roslyn/NativeAOT repositories. I'm not ready for people to see (or to accept pull requests) things that are specific to bflat. If you think bflat is useful, you can leave me a tip in my [tip jar](https://paypal.me/MichalStrehovsky) and include your GitHub user name in a note so that I can give you access to a private repo when I'm ready.

## 📻 How to stay up-to-date on bflat?

Follow me on [Twitter](https://twitter.com/MStrehovsky).

## 🎺 Optimizing output for size

By default, bflat produces executables that are between 2 MB and 3 MB in size, even for the simplest apps. There are multiple reasons for this:

* bflat includes stack trace data about all compiled methods so that it can print pretty exception stack traces
* even the simplest apps might end up calling into reflection (to e.g. get the name of the `OutOfMemoryException` class), globalization, etc.
* method bodies are aligned at 16-byte boundaries to optimize CPU cache line utilization
* (Doesn't apply to Windows) DWARF debug information is included in the executable

The "bigger" defaults are chosen for friendliness and convenience. To get an experience that more closely matches low level programming languages, specify `--no-reflection`, `--no-stacktrace-data`, `--no-globalization`, and `--no-exception-messages` arguments to `bflat build`.

Best to show an example. Following program:

```csharp
using System.Diagnostics;
using static System.Console;

WriteLine($"NullReferenceException message is: {new NullReferenceException().Message}");
WriteLine($"The runtime type of int is named: {typeof(int)}");
WriteLine($"Type of boxed integer is{(123.GetType() == typeof(int) ? "" : " not")} equal to typeof(int)");
WriteLine($"Type of boxed integer is{(123.GetType() == typeof(byte) ? "" : " not")} equal to typeof(byte)");
WriteLine($"Upper case of 'Вторник' is '{"Вторник".ToUpper()}'");
WriteLine($"Current stack frame is {new StackTrace().GetFrame(0)}");
```

will print this by default:

```
NullReferenceException message is: Object reference not set to an instance of an object.
The runtime type of int is named: System.Int32
Type of boxed integer is equal to typeof(int)
Type of boxed integer is not equal to typeof(byte)
Upper case of 'Вторник' is 'ВТОРНИК'
Current stack frame is <Program>$.<Main>$(String[]) + 0x154 at offset 340 in file:line:column <filename unknown>:0:0
```

But it will print this with all above arguments specified:

```
NullReferenceException message is: Arg_NullReferenceException
The runtime type of int is named: EETypeRva:0x00048BD0
Type of boxed integer is equal to typeof(int)
Type of boxed integer is not equal to typeof(byte)
Upper case of 'Вторник' is 'Вторник'
Current stack frame is ms!<BaseAddress>+0xb82d4 at offset 340 in file:line:column <filename unknown>:0:0
```

With all options turned on, one can comfortably fit useful programs under 1 MB. The above program is 735 kB on Windows at the time of writing this. The output executables are executables like any other. You can use a tool like UPX to compress them further (to ~300 kB range).

If you're on a Unix-like system, you might want to run `strip` tool to remove debug information from the executable. Windows places the debug information in a separate PDB file and `strip` is not needed.

## 🎸 Preprocessor definitions

Besides the preprocessor definitions provided at the command line, bflat defines several other symbols: `BFLAT` (defined always), `DEBUG` (defined when not optimizing), `WINDOWS`/`LINUX`/`MACOS` (when the corresponding operating system is the target), `X86`/`X64`/`ARM`/`ARM64` (when the corresponding architecture is targeted).

## 🎹 Debugging bflat apps

Apps compiled with bflat debug same as any other native code. Launch the produced executable under your favorite debugger (gdb or lldb on Linux, or Visual Studio or WinDbg on Windows) and you'll be able to set breakpoints, step, and see local variables.

## 😢 Current known issues

* #3 - Things that depend on libssl don't work on Linux - this includes a lot of crypto. We need to link to libssl statically but the framework code is structured very much against that. Needs a bit work.
* #3 - Globalization is uncondionally disabled on Linux - we need to link with ICU. Needs a bit work.
* #4 - No Linux host of the bflat compiler. This is unforunate fallout from libssl. Apparently the C# compiler insists on calculating SHA hashes of files and we need a working libssl for that.
* #5 - Can't build shared libraries for Linux.
