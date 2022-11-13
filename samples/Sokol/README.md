# Sokol graphics/audio samples

Bflat includes the [Sokol](https://github.com/floooh/sokol) crossplatform 3D and audio APIs.

The C# bindings in use are these: https://github.com/MichalStrehovsky/sokol-csharp. The bindings are also available from NuGet so that they can also be used with official .NET tools outside bflat. The NuGet package is here: https://www.nuget.org/packages/sokol_csharp.unofficial/.

To build the triangle sample with bflat:

```console
$ bflat build triangle.cs --target:winexe
```

You can also build it as:

```console
$ bflat build triangle.cs --target:winexe --no-reflection --no-stacktrace-data --no-globalization --no-exception-messages -Os --no-pie --separate-symbols
```

On Windows, this will generate a small, ~790 kB executable.

To build the audio sample:

```console
$ bflat build saudio.cs --target:winexe
```

More samples are avilable at https://github.com/MichalStrehovsky/sokol-csharp. They're all C# translations of https://github.com/floooh/sokol-samples.
