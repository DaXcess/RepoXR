# Compiling

## Initial setup

We recommend using Visual Studio with the .NET development features to compile this project. You can [download Visual Studio here](https://visualstudio.microsoft.com/downloads/). You should use the Community edition as that one is free.

Alternatively you may also manually install the .NET development tools and compile the project from command line. You will need to [download the .NET SDK 8.0](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) or higher, since this project is using C# 12.

You can download it from the link above, or alternatively if you are using Arch Linux you can install dotnet using the following command:

```sh
$ pacman -S dotnet-sdk
```

## Building the assembly

Now that the project is set up, you may compile it by either building it using Visual Studio, or by running the following command in the project root:

```sh
$ dotnet build
```

Alternatively if you're building a finished product run this command:

```sh
$ dotnet build --configuration Release
```

The built plugin assemblies can now be found inside the `bin` folder.

## For R.E.P.O. Testers

The main RepoXR codebase is designed to be compatible with all official R.E.P.O. releases (including some tester builds).  
However, if you want to develop against **private tester builds**, there are a few extra steps to follow.

Since tester builds are private, there are **no public NuGet packages available** for these versions. To let your local build reference the tester assemblies:

Add the following content, updating the `TesterGamePath` if your game is installed elsewhere:

```xml
<Project>
    <PropertyGroup>
        <USE_TESTER>true</USE_TESTER>
        <TesterGamePath>C:\Program Files (x86)\Steam\steamapps\common\REPO</TesterGamePath>
    </PropertyGroup>
</Project>
```

> **Warning**: Do not share game code or mod code that targets private tester builds until those builds are officially public. Use private repositories or branches, and only merge changes into the main repository once the builds are public.