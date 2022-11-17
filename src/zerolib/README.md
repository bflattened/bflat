# ZeroLib minimal runtime library

There are two guiding principles:

1. Public API surface that doesn't exist in .NET cannot be added (i.e. source code compilable against zerolib needs to be compilable against .NET).
2. APIs that do hidden allocations cannot be added. If an API returns a fresh object instance, it's all good. If an API allocates as part of it's operation and doesn't return that as result, it's not good. We don't have a GC. This is all memory leaks.

To work in this codebase, simply add a file with a `Main` to this directory and `bflat build --stdlib:none`. You'll get a tight inner dev loop. But you can do things differently; more power to you.
