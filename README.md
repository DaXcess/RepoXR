# R.E.P.O. VR Mod

[![Thunderstore Version](https://img.shields.io/thunderstore/v/DaXcess/RepoXR?style=for-the-badge&logo=thunderstore&logoColor=white)](https://thunderstore.io/c/repo/p/DaXcess/RepoXR)
[![GitHub Version](https://img.shields.io/github/v/release/DaXcess/RepoXR?style=for-the-badge&logo=github)](https://github.com/DaXcess/RepoXR/releases/latest)
[![Thunderstore Downloads](https://img.shields.io/thunderstore/dt/DaXcess/RepoXR?style=for-the-badge&logo=thunderstore&logoColor=white)](https://thunderstore.io/c/repo/p/DaXcess/RepoXR)
[![GitHub Downloads](https://img.shields.io/github/downloads/DaXcess/RepoXR/total?style=for-the-badge&logo=github)](https://github.com/DaXcess/RepoXR/releases/latest)
<br />
[![Release Build](https://img.shields.io/github/actions/workflow/status/DaXcess/RepoXR/build-release.yaml?branch=main&style=for-the-badge&label=RELEASE)](https://github.com/DaXcess/RepoXR/actions/workflows/build-release.yaml)
[![Debug Build](https://img.shields.io/github/actions/workflow/status/DaXcess/RepoXR/build-debug.yaml?branch=dev&style=for-the-badge&label=DEBUG)](https://github.com/DaXcess/RepoXR/actions/workflows/build-debug.yaml)

> I couldn't wait until R.E.P.O. _might_ get a native VR mode so here is my contribution

<details>
  <summary>Jumpscare</summary>

  <img src="https://github.com/user-attachments/assets/0ba07173-e88d-4038-b46d-0c23f6f4ab43" alt="Floating kitty" />
</details>
<br/>

Ready to satisfy your ~~heartless~~ **GENEROUS** creator with your hard-earned ~~cash~~ **SURPLUS**... in VR!? Well wait no longer!

**RepoXR** is a mod that adds full 6-DoF VR support to R.E.P.O., including hand movement and motion-based controls.

The mod is powered by Unity's OpenXR plugin and is thereby compatible with a wide range of headsets, controllers and runtimes, like Oculus, Virtual Desktop, SteamVR and many more!

RepoXR is compatible with multiplayer and works in a lobby comprised of both flatscreen **and** VR players. Running this mod without having a VR headset will allow you to see the arm movements of any VR Semibots in the same lobby, all while still being compatible with vanilla clients (even if the host is using no mods, though there is a small catch with that).

### Installing and using the mod

It is recommended to use a mod launcher like Gale to easily download and install the mod. You can download Gale [here](https://kesomannen.com/gale). In gale, just look for the mod named "RepoXR", and install it, which should automatically install all dependencies needed.

Running the mod using Gale can be done simply by clicking "Launch game", which will automagically launch the game with the installed mods.

### Open Source

The source code for this mod is available on GitHub! Check it out: [DaXcess/RepoXR](https://github.com/DaXcess/RepoXR).

### License

This mod is licensed under the GNU General Public License version 3 (GPL-3.0). For more info check [LICENSE](https://github.com/DaXcess/RepoXR/blob/main/LICENSE).

### Verifying mod signature

> If you don't care about this, skip this part.

RepoXR comes pre-packaged with a digital signature. You can use tools like GPG to verify the `RepoXR.dll.sig` and `RepoXR.Preload.dll.sig` signatures.

The public key which can be used to verify the file is [9422426F6125277B82CC477DCF78CC72F0FD5EAD (OpenPGP Key Server)](https://keys.openpgp.org/vks/v1/by-fingerprint/9422426F6125277B82CC477DCF78CC72F0FD5EAD).

### Bypassing integrity checks

To prevent completely destroying the game, this mod scans the game assembly and tries to detect whether it's using a supported version or not. If this check fails, the mod will assume that either the game was updated, or the game files have been corrupted, and will refuse to start the mod. You can disable this behaviour by passing `--repoxr-skip-checksum=<version>` to the game's launch options in Steam, where `<version>` is the RepoXR version being used.

### Discord Server

Facing issues, have some mod (in)compatibility to report or just want to hang out?

You can join the [Discord Server](https://discord.gg/2DxNgpPZUF)!

# Versions

Here is a list of RepoXR versions and which version(s) of R.E.P.O. it supports

| RepoXR | R.E.P.O. Version |
| ------ | ---------------- |
| v1.0.3 | v0.2.1           |
| v1.0.2 | v0.2.1           |
| v1.0.1 | v0.2.1           |
| v1.0.0 | v0.2.0           |

> RepoXR is also able to check hashes remotely, meaning newer R.E.P.O. versions might be supported even though they aren't listed here.

# Multiplayer Support

Like my other VR mods, RepoXR is fully compatible with both VR players and non-VR players, whether they have the mod installed or not. Having the mod installed as a non-VR player is still beneficial, since it allows you to see the arm movements of VR players (and it includes some other features).

There's one small catch though: If the **host** does **not** have the VR mod installed, physics calculations will be different for VR players, which can be somewhat disruptive for the VR players. It is therefor recommended for the host to have the VR mod installed (even if the host does not use VR themselves), to make physics calculations behave normal for VR players.

# Compatibility

Most mods should work fine with RepoXR, like new levels, new items, etc. However, there are also mods that may impact some core functionality of the game, which might not support RepoXR. You will have to manually verify if certain mods work with RepoXR or not.

# Configuring the mod

You can change the mod configuration from within the game itself (even when playing without VR enabled). Just launch the game with the mod installed, get to the main menu, go to the settings, and press `VR Settings`. This will open a settings menu that looks like the one from the base game, however it only contains settings dedicated to the VR mod.

> _When creating a modpack or profile code, it is recommended to **NOT** ship your config file, so that other people can configure it on their own using the default settings. To quickly reset the settings, delete the config file named `io.daxcess.repoxr.cfg` in the `BepInEx/config` directory._

# Controls

RepoXR attempts to automatically detect which type of controller you are using, and will automatically apply the correct controller profile once they have been detected.

The current list of built-in controller profiles are:

- Oculus (Rift S, Quest 2, Quest 3 through VD) - Default Fallback
- Meta Quest (Quest 3 through Steam link or Native link)
- Valve Index
- HTC Vive
- HP Reverb G2
- Windows Mixed Reality (deprecated)

# How to change controls

You can change controller bindings the same way you would when playing R.E.P.O. normally.

Go to the settings menu, and click `Controls`. All the controls have been replaced with VR controls, so you won't have to scroll to a special VR section.

> You must be in VR to change VR controller bindings. <br/>
> Resetting the bindings will only reset the VR bindings, and will not touch keyboard bindings.

# Grabbing stuff

Instead of grabbing items with your "face", you can actually grab items with your hand. You have full control over the items while you're holding them, you can even rotate the items by rotating your hand (and of course using the rotate feature in R.E.P.O. itself). To pull and push items, use right stick up/down, which will adjust the distance the item is held from.

If for any reason your hand becomes obscured by solid geometry (e.g. you put your hand in a wall), item grabbing will be disabled, preventing you from picking up items through walls.

# The map

RepoXR changes the way how you open up your map. Instead of having a keybind to open the map, you have a sort of toolbelt in front of you. On the right side of the toolbelt is the map. You can grab the map by hovering over it with _either_ hand (yes, you can use both hands!) and grabbing it using the grip button. To put the map back, release the grip button. If your player has any upgrades, they will show up when holding the map on the left or right side (depending on which hand is used to pick up the map). If you're holding an item in your hand, the map scoots to the left side of the toolbelt, giving easier access to the map for your left hand.

# The inventory

Just like the map, the inventory has been placed on your toolbelt. When picking up an item that can be stored, three yellow rectangles will show up. To put an item in one of the slots, point your hand towards any of the three slots, and the item will be put inside the corresponding slot. If the slot was already occupied, the held item will be dropped, and you will pick up the item that was previously in the slot (same behavior as the base game).

To grab an item out of your inventory, hover over it with your right hand, and press the Grab button (trigger by default). Which will immediately pick up the item for it to be used.

# Headlamp

In R.E.P.O. VR, by default you will hold your flashlight in your left hand (not unlike the flashlight in lethal or content warning). To make life easier though, you can hold your hand to the side of your head, press the grip key, and the flashlight will be attached to your head, making the flashlight point to wherever you are looking. You can perform the same action to put the flashlight back onto your hand.

# The chat

Contrary to my Lethal Company VR mod, the R.E.P.O. VR mod actually does allow you to access the chat. It has a default binding of `Left Joystick Pressed`, and it will pop up the chat window with a virtual keyboard underneath it. You can use this keyboard like you would with a normal keyboard (though it has a limited character set). Using the chat in VR is a lot more clunky than it is on PC, so it's more of a gimmick, but at least you can use it if you would like to.

# Expressions

R.E.P.O. allows you to make your Semibot display a certain expression (or a combination of multiple expressions). In VR, you can access expressions by holding down the chat button, which will pop up a radial menu on the hand that was used to trigger the menu. With the wheel opened, you can position your hand on one of the expressions, and use the trigger button to toggle that expression. Even if the menu is closed, your expressions will stay active. When loading a new scene (e.g. after completing a level), the expressions will be remembered, so you do not have to re-enable expressions (not until you start a new game that is).

# Left handed support

RepoXR allows you to change your dominant hand, so you can swap your primary (grabbing) hand using a simple configuration setting. You can change this option at any time, whether you're in the main menu, or being chased by a Headman (hypothetically).

> [!WARNING]
> Changing your dominant hand **will not** update your bindings. You will have to manually rebind the controls in the settings menu to your preferred controls if you decide to change your dominant hand.

# Eye Tracking

If your headset supports it, you can make use of eye tracking when using the VR mod. Eye tracking alters mechanics in both singleplayer and multiplayer, so nobody is left out!

When eye tracking is enabled, certain features within the game that require you to look at (or away) from certain points in your vision will now take into account your real focus, instead of only relying on the rotation of your head. For example: looking away from the Shadow Child with your head, but still looking at it with your eyes, will cause the shadow child to snatch you up and throw you around.

Other people can also enjoy the use of eye tracking! When used in multiplayer, the eyes of the semibot will look wherever you are looking in real life, so you can roll your eyes, or stare into the soul of that one friend that keeps destroying all the fragile valuables.

# Other misc features

- While inside of a lobby, you can press "CTRL + C" on your keyboard to copy a Steam join link (steam://joinlobby/...) to allow you to invite people outside your friends list (make sure your game is focussed while you do this)

- While inside of the main menu, or a multiplayer **menu** lobby, you can press F8 to toggle between VR and flatscreen mode.
