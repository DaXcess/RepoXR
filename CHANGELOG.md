# 1.1.1

**Additions**:
- Added a \[VR\] prefix to VR players in the lobby menu
- Added a warning popup if the host does not have the VR mod

**Fixes**:
- Fixed hotswapping breaking the menu lobby in some circumstances

# 1.1.0

## Detached Arms

You can now detach your arms from your body, giving you more control over your aim, not being constrained by the maximum length of the Semibot's arms anymore. You can change this setting at any time, no restarts or rejoins needed.

## Eye Tracking

RepoXR v1.1.0 adds support for eye tracking (for the three people that have it). 

#### Singleplayer

Players that use eye tracking will have enhanced immersion when it comes to "looking at" things. A few of the enemies in R.E.P.O. behave differently depending on if you're looking at them or not. This detection now factors in where you are looking with your eyes!

Discovering valuables, or your lost friends' heads will also make use of eye tracking. Just look at the items and the game will discover them without you having to move your head.

#### Multiplayer

When playing with other people, other people will see your pupils move based on your real eye movement. Looking down? People will see you look down. Looking straight through your friend's soul because they broke something? Yup, they'll see that!

#### Configuration

Eye tracking can be enabled and disabled mid-game, there's no need to restart. If your headset does not support eye tracking, it will be considered disabled (even when eye tracking is enabled in the config), so no need to change the settings if you don't have eye tracking.

## Hotswap

You can now swap between VR mode and flatscreen mode by pressing the F8 button on your keyboard while you are in the main menu or in the lobby menu. This works even when you are the host (though the lobby will reload for everybody).

**Additions**:
- Added eye tracking support
- Added an option to detach arms from body
- Added support for the new climbing mechanic
- Added support for spectating your death head
- Added support for the monster update
- Added hotswapping in the main menu (F8)

**Changes**:
- The ceiling eye now darkens the world except for where the eye is (no more cheating hihi)
- Removed the performance tab and replaced it with UI in the settings
- Replaced the valuable discover overlay with a new 3D graphic (supports custom colors)
- You now look at the enemy/object that killed you while the death animation plays (if possible)
- Slightly optimized the custom camera by adding a frame rate limiter (disabled by default)
- Optimized framerate by forcibly disabling ambient occlusion (20%-40% less render time)
- Renamed "Dynamic Smooth Speed" to "Analog Smooth Turn"
- Changed the minimum possible HUD height value to account for detached hands
- The map tool can now also be grabbed from behind your head (near the shoulders)
- You can now unbind controls at your leisure

**Removals**:
- Removed support for REPO v0.2.x

# 1.0.3

**Additions**:
- Added a keyboard shortcut (CTRL+C) to the lobby menu for easy copying of a steam lobby link
- Added support for late joining

**Removals**:
- Removed redundant checks during startup

# 1.0.2

**Additions**:
- Added keybind options for turning
- Added a new option for configuring the distance of the camera canvas in the gameplay settings

# 1.0.1

**Additions**:
- Added support for v0.2.1

**Bug fixes**:
- Fixed the small moons in the moon UI rotating weirdly
- Fixed a bug in HarmonyX that would generate invalid code during transpiling

**Removals**:
- Removed support for v0.2.0

# 1.0.0

**The VR mod has been released!!**

It took a bit, but after 3 months <sub><sup>_(started March 21st 2025)_</sup></sub> the R.E.P.O. VR mod has finally released!

No changelogs are necessary for this version, as it is the first version. Subsequent versions will contain a list of changes and new contributors.

### Verifying mod signature

RepoXR comes pre-packaged with a digital signature. You can use tools like GPG to verify the `RepoXR.dll.sig` and `RepoXR.Preload.dll.sig` signatures.

The public key which can be used to verify the file is [9422426F6125277B82CC477DCF78CC72F0FD5EAD (OpenPGP Key Server)](https://keys.openpgp.org/vks/v1/by-fingerprint/9422426F6125277B82CC477DCF78CC72F0FD5EAD).