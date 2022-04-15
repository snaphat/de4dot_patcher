# de4dot_patcher
Patcher for d4dot that merges all assemblies into the base executable, publicizes classes/fields/methods, unseals classes, and virtualizes methods.

# Dependencies
- `csc.exe` must be in the path. 
- `ILRepack.exe` must be in the path.
- `de4dot`

# Setup
- Download a compiled version of [de4dot](https://github.com/mobile46/de4dot). You should be able to get a copy from the build bot as an [artifact](https://github.com/mobile46/de4dot/actions).
- Install [Build Tools for Visual Studio](https://visualstudio.microsoft.com/downloads/#build-tools-for-visual-studio-2022) and add `csc.exe` directory to your path.
- Install [ILRepack](https://github.com/gluck/il-repack) and add the `ILRepack.exe` directory to your path. I just extract the executable from the [nupkg](http://nuget.org/api/v2/package/ILRepack) directly.

# Usage

- Run `build.bat` to compile.
- Drop `auto_patcher.exe` the same directory as de4dot.exe and run the executable.
- A patched version of `de4dot.exe` called `de4dotp.exe` will be in the same directory.
- `de4dotp.exe` is usable without any external dependencies and can be utilized directly as an assembly with other .net code.
