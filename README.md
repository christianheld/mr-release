# Mr. Release
Mr. Release helps you to keep track of your releases.

This is a more or less toy project to simplify deployments and track deployed
services at my day job.

Also I wanted to play with [`Spectre.Console`](https://spectreconsole.net/) and
`Spectre.Console.Cli`.

## Usage
Currently `MrRelase` supports following commands:

### Initialize configuration
 `init` - Inititalize Mr.Release and write settings to `~/.mrrelease/settings.json`

### Show current releases
`show` - Show the currently active releases inside a folder for a certain stage.

Example:
```ps1
MrRelease show my-project Staging
```

This will show all releases in the Release folder `my-project` that are currently
deployed to the `Staging` environment.

The `show` command supports subfolders as well:

Example:
```ps1
MrRelease show my-project/subfolder Staging
```

or
```ps1
MrRelease show my-project\subfolder Staging
```

## Build instructions
Use `build.ps1` or `build.sh` to build.

You will find the binaries in `./artifacts/{runtime}`, e.g. `./artifacts/win-x64`
