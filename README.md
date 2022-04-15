# de4dot_patcher
Patcher for d4dot that merges all assemblies into the base executable, publicizes classes/fields/methods, unseals classes, and virtualizes methods.

# Dependencies
- `csc.exe` must be in your path. 
- `ILRepack.exe` must be in your path.
- `de4dot.exe` and associated dlls are needed for patching.

# Setup
- Download a compiled version of [de4dot](https://github.com/mobile46/de4dot). You should be able to get a copy from the build bot [here](https://github.com/mobile46/de4dot/actions) as an artifact.
- Install [Build Tools for Visual Studio](https://visualstudio.microsoft.com/downloads/#build-tools-for-visual-studio-2022) and add the directory `csc.exe` is in to your path.
- Install [ILRepack](https://github.com/gluck/il-repack) and add the directory `ILRepack.exe` is in to your path. I just extract the executable from the [nupkg](http://nuget.org/api/v2/package/ILRepack) directly.

# Usage

- Run `build.bat` to compile.
- Drop `auto_patcher.exe` the same directory as de4dot.exe and run the executable.
- A patched version of `de4dot.exe` called `de4dotp.exe` will be in the same directory.
- `de4dotp.exe` is usable without any external dependencies and can be utilized directly as an assembly with other .net code.
