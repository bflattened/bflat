# Building bflat from source

You'll need the .NET SDK to build bflat. The shipping binaries of bflat are built with bflat, but the .NET SDK is used for bootstrapping.

Before you can build bflat, you need to make sure you can restore the packages built out of the bflattened/runtime repo. For reasons that escape me, NuGet packages published to the Github registry require authentication. You need a github account and you need to create a PAT token to read packages. Follow the information [here](https://docs.github.com/en/packages/working-with-a-github-packages-registry/working-with-the-nuget-registry).

You should end up with a nuget.config file in src/bflat/ that looks roughly like this:

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
    <packageSources>
        <add key="github" value="https://nuget.pkg.github.com/bflattened/index.json" />
    </packageSources>
    <packageSourceCredentials>
        <github>
            <add key="Username" value="YOURUSERNAME" />
            <add key="ClearTextPassword" value="YOURPAT" />
        </github>
    </packageSourceCredentials>
</configuration>
```

In retrospect, going with Github packages was a mistake, but I don't have the capacity to redo things that work right now. NuGet.config is in .gitignore so that you don't accidentally check it in. But to be doubly sure, make sure your PAT can only read packages and nothing else. Leaking such PAT would likely cause no damage to most people.

With the package issue out of the way, you can run bflat by executing:

```bash
$ dotnet run --project src/bflat/bflat.csproj
```

from the repo root, or build binaries by running:

```bash
$ dotnet build src/bflat/bflat.csproj
```

This will build/run bflat on top of the official .NET 6 runtime.

To create bflat-compiled versions of bflat, run:

```bash
$ dotnet build src/bflat/bflat.csproj -t:BuildLayouts
```

This will create a `layouts` directory at the repo root and place Linux- and Windows-hosted versions of the bflat compiler built with bflat. These are the bits that are available as prebuilt binaries.
