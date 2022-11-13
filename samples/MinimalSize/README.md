# Minimal size sample

This demonstrates how to build a minimal size executable with bflat.

```console
$ bflat build minimalsize.cs --no-reflection --no-stacktrace-data --no-globalization --no-exception-messages -Os --no-pie --separate-symbols
```

This will produce a minimalsize(.exe) file that is native compiled. You can launch it. Observe the difference in runtime behavior and size of the output when you omit some of the arguments from the `bflat build` command line above.
