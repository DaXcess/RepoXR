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
| v1.2.2 | v0.4.3           |
| v1.2.1 | v0.4.3           |
| v1.2.0 | v0.4.3           |
| v1.1.2 | v0.3.0 - v0.3.2  |
| v1.1.1 | v0.3.0 - v0.3.1  |
| v1.1.0 | v0.3.0 - v0.3.1  |
| v1.0.3 | v0.2.1           |
| v1.0.2 | v0.2.1           |
| v1.0.1 | v0.2.1           |
| v1.0.0 | v0.2.0           |

> RepoXR dynamically measures compatibility with the game during startup, meaning newer R.E.P.O. versions might be supported even though they aren't listed here.

# Install from source

> The easiest way to install the mod is by downloading it from Thunderstore. You only need to follow these steps if you are planning on installing the mod by building the source code and without a mod manager.

To install the mod from the source code, you will first have to compile the mod. Instructions for this are available in [COMPILING.md](COMPILING.md).

Next up you'll need to grab a copy of a previous release of RepoXR either from [Releases](https://github.com/DaXcess/RepoXR/releases) or from [Thunderstore](https://thunderstore.io/c/repo/p/DaXcess/RepoXR) (manual download). Extract this zip file and replace the `LCVR.dll` and/or the `LCVR.Preload.dll` files with the new files that you obtained after following the guide in [COMPILING.md](COMPILING.md).

You can *also* manually build the required plugins, asset bundles and addressables by cloning the [RepoXR-Unity](https://github.com/DaXcess/RepoXR-Unity) and performing the necessary steps (which I am not planning on documenting).