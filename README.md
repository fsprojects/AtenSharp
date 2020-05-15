
AtenSharp
==========

AtenSharp is a .NET library that provides access to the underlying tensor library that powers
PyTorch.   This was originally part of TorchSharp but has been factored out and is no longer maintained.

[![Project Status: Suspended – Initial development has started, but there has not yet been a stable, usable release; work has been stopped for the time being but the author(s) intend on resuming work.](https://www.repostatus.org/badges/latest/suspended.svg)](https://www.repostatus.org/#suspended)

If you would like to be a maintainer on this project please open an issue or context @dsyme or another maintainer.

Things that you can try:

```csharp
using AtenSharp;

var x = new FloatTensor (100);   // 1D-tensor with 100 elements
FloatTensor result = new FloatTensor (100);

FloatTensor.Add (x, 23, result);

Console.WriteLine (x [12]);
```

Building
============


Windows
-----------------------------

Requirements:
- Visual Studio
- git
- cmake (tested with 3.14)

Commands:
- Building: `build.cmd`
- Building from Visual Studio: first build using the command line
- See all configurations: `build.cmd -?`
- Run tests from command line: `build.cmd -runtests`
- Build packages: `build.cmd -buildpackages`


Linux/Mac
-----------------------------
Requirements:
- requirements to run .NET Core 2.0
- git
- cmake (tested with 3.14)
- clang 3.9

Example to fulfill the requirements in Ubuntu 16:
```
sudo apt-get update
sudo apt-get install git clang cmake libunwind8 curl
sudo apt-get install libssl1.0.0
sudo apt-get install libomp-dev
```

Commands:
- Building: `./build.sh`
- Building from Visual Studio: first build using the command line
- See all configurations: `./build.sh -?`
- Run tests from command line: `./build.sh -runtests`
- Build packages: `./build.sh -buildpackages`

Updating package version for new release
-----------------------------
To change the package version update this [file](https://github.com/xamarin/AtenSharp/blob/master/build/BranchInfo.props).
Everything is currently considered in preview.

Use the following two MSBuild arguments in order to control the -preview and the build numbers in the name of the nuget packages produced (use one of the two generally):

|Name | Value| Example Version Output|
|---|---|---|
|StabilizePackageVersion |  true  | 1.0.0|
|IncludeBuildNumberInPackageVersion | false | 1.0.0-preview|

Sample command: `./build.cmd -release -buildpackages -- /p:StabilizePackageVersion=true`

GPU support
============
For GPU support it is required to install CUDA 9.0 and make it available to the dynamic linker.

Examples
===========
Porting of the more famous network architectures to AtenSharp is in progress. For the moment we only support [MNIST](https://github.com/xamarin/AtenSharp/blob/master/src/Examples/MNIST.cs) and [AlexNet](https://github.com/xamarin/AtenSharp/blob/master/src/Examples/AlexNet.cs)
