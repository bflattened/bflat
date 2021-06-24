# Shared library sample

To build a shared library run following command:

```console
$ bflat build library.cs
```

Bflat automatically detects you're trying to build a library because there's no Main. You can also specify `--target` to bflat.

This will produce a library.so file on Linux and library.dll on Windows.

The library can be consumed from any other programming language. Since we're using C#, let's consume it from C#. Because at this point library.so/.dll is a native library like any other, we need to p/invoke into it.

```console
$ bflat build libraryconsumer.cs
```

This will build a libraryconsumer(.exe) binary that will load the library and invoke functions in it.
