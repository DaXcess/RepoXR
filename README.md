# R.E.P.O. VR Mod

[![Thunderstore Version](https://img.shields.io/thunderstore/v/DaXcess/RepoXR?style=for-the-badge&logo=thunderstore&logoColor=white)](https://thunderstore.io/c/repo/p/DaXcess/RepoXR)
[![GitHub Version](https://img.shields.io/github/v/release/DaXcess/RepoXR?style=for-the-badge&logo=github)](https://github.com/DaXcess/RepoXR/releases/latest)
[![Thunderstore Downloads](https://img.shields.io/thunderstore/dt/DaXcess/RepoXR?style=for-the-badge&logo=thunderstore&logoColor=white)](https://thunderstore.io/c/repo/p/DaXcess/RepoXR)
[![GitHub Downloads](https://img.shields.io/github/downloads/DaXcess/RepoXR/total?style=for-the-badge&logo=github)](https://github.com/DaXcess/RepoXR/releases/latest)
<br />
[![Release Build](https://img.shields.io/github/actions/workflow/status/DaXcess/RepoXR/build-release.yaml?branch=main&style=for-the-badge&label=RELEASE)](https://github.com/DaXcess/RepoXR/actions/workflows/build-release.yaml)
[![Debug Build](https://img.shields.io/github/actions/workflow/status/DaXcess/RepoXR/build-debug.yaml?branch=dev&style=for-the-badge&label=DEBUG)](https://github.com/DaXcess/RepoXR/actions/workflows/build-debug.yaml)

**RepoXR** is a fully fledged R.E.P.O. VR mod that adds full 6-DoF motion controlled VR support to R.E.P.O.

The mod is powered by Unity's OpenXR plugin and is thereby compatible with a wide range of headsets, controllers and runtimes, like Oculus, Virtual Desktop, SteamVR and many more!

RepoXR is compatible with multiplayer and works in a lobby comprised of both flatscreen **and** VR players. Running this mod without having a VR headset will allow you to see the arm movements of any VR semibots in the same lobby, all while still being compatible with vanilla clients (even if the host is using no mods, though there is a small catch with that).

### Discord Server

Facing issues, have some mod (in)compatibility to report or just want to hang out?

You can join the [Discord Server](https://discord.gg/2DxNgpPZUF)!

# Compatibility

At the time of writing, no explicit compatibility exists yet for RepoXR, so you will have to manually figure out what mod works in VR and which ones don't.

# Installing and using the mod

It is recommended to use a mod launcher like Gale to easily download and install the mod. You can download Gale [here](https://kesomannen.com/gale). This mod can be found on thunderstore under the name [RepoXR](https://thunderstore.io/c/repo/p/DaXcess/RepoXR). You can also install the mod by manually downloading it in combination with BepInEx.

Running the mod using Gale can be done simply by clicking "Launch game", which will automagically launch the game with the installed mods.

For more information on using the mod, check out the [RepoXR Thunderstore page](https://thunderstore.io/c/repo/p/DaXcess/RepoXR).

# Versions

Here is a list of RepoXR versions and which version(s) of R.E.P.O. it supports

| RepoXR | R.E.P.O. Version |
|--------|------------------|
| v1.1.2 | v0.3.0 - v0.3.2  |
| v1.1.1 | v0.3.0 - v0.3.1  |
| v1.1.0 | v0.3.0           |
| v1.0.3 | v0.2.1           |
| v1.0.2 | v0.2.1           |
| v1.0.1 | v0.2.1           |
| v1.0.0 | v0.2.0           |

> RepoXR is also able to check hashes remotely, meaning newer R.E.P.O. versions might be supported even though they aren't listed here.

# Install from source

> The easiest way to install the mod is by downloading it from Thunderstore. You only need to follow these steps if you are planning on installing the mod by building the source code and without a mod manager.

To install the mod from the source code, you will first have to compile the mod. Instructions for this are available in [COMPILING.md](COMPILING.md).

Next up you'll need to grab a copy of some **Runtime Dependencies** and the [**Asset Bundle**](https://github.com/DaXcess/RepoXR/blob/thunderstore/repoxrassets). You can grab both of these from [the thunderstore branch](https://github.com/DaXcess/RepoXR/tree/thunderstore).
You can also manually retrieve the **Runtime Dependencies** from a manually compiled Unity project.

You can *also* manually build an asset bundle by cloning the [REPOVR-Unity](https://github.com/DaXcess/REPOVR-Unity) project and building the assets using the Asset Bundle Browser.

## Retrieving Runtime Dependencies and building the Asset Bundle from a Unity Project

> You can skip this part if you have taken the runtime dependencies from the releases page.

First of all, start by installing Unity 2022.3.21f1, which is the Unity version that R.E.P.O. uses. Once you have installed the editor, clone the [REPOVR-Unity](https://github.com/DaXcess/REPOVR-Unity) repository and open it up in the Unity editor.

Once you have opened up the Unity editor, you can build the game to retrieve the runtime dependencies. These files will be located in the `Build/<Project Name>_Data/Managed` directory. There you will need to extract the following files:

- UnityEngine.SpatialTracking.dll
- Unity.Mathematics.dll
- Unity.XR.CoreUtils.dll
- Unity.XR.Interaction.Toolkit.dll
- Unity.XR.Management.dll
- Unity.XR.OpenXR.dll

And from the `<Project Name>_Data/Plugins/x86_64` directory:

- openxr_loader.dll
- UnityOpenXR.dll

To retrieve the asset bundle, open up the Asset Bundle Browser, and build the `repoxrassets` bundle, which after built will be located in the `AssetBundles/StandaloneWindows` directory (look for the `repoxrassets` file, not the `repoxrassets.manifest` file).

## Install BepInEx

BepInEx is the modloader that RepoXR uses to mod the game. You can download BepInEx from their [GitHub Releases](https://github.com/BepInEx/BepInEx/releases) (RepoXR currently targets BepInEx 5.4.21).

To install BepInEx, you can follow their [Installation Guide](https://docs.bepinex.dev/articles/user_guide/installation/index.html#installing-bepinex-1).

## Installing the mod

Once BepInEx has been installed and run at least once, you can start installing the mod.

First of all, in the `BepInEx/plugins` folder, create a new folder called `RepoXR` (doesn't have to be named that specifically, but makes identification easier). Inside this folder, place the `RepoXR.dll` file that was generated during the [COMPILING.md](COMPILING.md) steps.

After this has been completed, create a new directory called `RuntimeDeps` (has to be named exactly that) inside the `RepoXR` folder. Inside this folder you will need to put the following DLLs:

- UnityEngine.SpatialTracking.dll
- Unity.Mathematics.dll
- Unity.XR.CoreUtils.dll
- Unity.XR.Interaction.Toolkit.dll
- Unity.XR.Management.dll
- Unity.XR.OpenXR.dll

These files should have been retrieved either during the [Retrieving Runtime Dependencies](#retrieving-runtime-dependencies-and-building-the-asset-bundle-from-a-unity-project) step, or from grabbing them from the latest release.

Next up, grab the **Asset Bundle** from one of the releases (or from the [Retrieving Runtime Dependencies](#retrieving-runtime-dependencies-and-building-the-asset-bundle-from-a-unity-project)) step, and place them into the same folder as the `RepoXR.dll` file. This asset bundle file needs to be called `repoxrassets`.

Finally, in the `BepInEx/patchers` folder, also create a new folder called `RepoXR` (again, doesn't have to be exactly named that). Inside this folder, place the `RepoXR.Preload.dll` file that was also generated during the [COMPILING.md](COMPILING.md) steps.

In this folder, also create a new directory called `RuntimeDeps` (again, has to be exactly named that), and place the following DLLs inside:

- openxr_loader.dll
- UnityOpenXR.dll

You can now run the game with RepoXR installed.
