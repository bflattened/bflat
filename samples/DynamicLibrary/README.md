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

Libraryconsumer is going to load the library through dlopen/dlsym (LoadLibrary/GetProcAddress on Windows). If you would like, you can also have it bind statically. Build the consumer program like this:

```console
$ bflat build libraryconsumer.cs -i:library
```

This will build the program with a hard reference to the external symbol. You should see a failure during linking if you run the above command. To fix this failure, we need to pass information where to find the symbol to linker.

If you're targeting Windows, run:

```console
$ bflat build libraryconsumer.cs -i:library --ldflags:library.lib
```

This will point the linker to the import library generated when building the library.
