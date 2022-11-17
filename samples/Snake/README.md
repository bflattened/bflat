# ZeroLib sample

Basides the .NET standard library, bflat also comes with a minimal standard library that is an very minimal subset of what's in .NET.

There is no garbage collector. No exception handling. No useful collection types. Just a couple things to run at least _some_ code.

Think of it more as an art project than something useful. Building something that fits the supported envelope is more an artistic expression than anything useful.

This directory contains a sample app (a snake game) that can be compiled both in the standard way (with a useful standard library) or with zerolib.

To build the sample with zerolib, run:

```console
$ bflat build --stdlib:zero
```

Most other `build` arguments still apply, so you can make things smaller with e.g. `--separate-symbols --no-pie`, or you can crosscompile with `--os:windows` and `--os:linux`.

You should see a fully selfcontained executable that is 10-50 kB in size, depending on the platform. That's the "art" part.
