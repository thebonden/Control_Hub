# AndroidRemote in Control Hub 0.0.001

[Dansk](androidremote.da.md)

AndroidRemote gives the operator four phone controls. Next and Previous navigate the existing slideshow. Switch OBS scene uses the scene name entered by the operator. Status shows slide position and the current OBS scene or explains when an integration is unavailable.

## Install and pair

1. Install the matching Windows development package and `ControlHub-AndroidRemote-0.0.001.apk` on a phone running Android 8 or later. Connect Windows and the phone to the same local network.
2. Before starting the host, select **Allow AndroidRemote on local network** and the computer’s private IPv4 address on the network reachable by the phone. Start Control Hub. The phone server uses TCP port 5188. The desktop and Stream Deck host continues to use loopback port 5187.
3. Open **Manage phone access**. Name the phone and select **Read status**, **Control slides** and **Switch OBS scene** as needed. Click **Create invitation**. Windows lists it as *Pending* for up to two minutes.
4. Choose **Save invitation file** and transfer the file privately, for example over USB. On the phone choose **Open invitation file** and then **Pair phone**. You can also paste the copied invitation. It contains a secret and the server certificate fingerprint. Use a transfer where you control sender and recipient. Delete the file after use.
5. Start a slideshow manually on Windows and prepare an existing OBS scene. Controls follow the granted permissions. An unknown outcome requires checking status before another press.

**Remove selected access** in Windows revokes the phone. **Forget pairing** on the phone clears its local credentials. Restarting the host creates a new certificate and requires fresh pairings. If certificate verification fails after restarting, choose **Forget pairing** and use a fresh invitation.

## Connection and access

The phone server has a dedicated HTTPS listener on one private address. Before redemption the phone checks the server IP identity, certificate validity and the SHA-256 fingerprint from the invitation. The app connects directly and rejects redirects. Firewall rules remain under the owner’s control. Check private network reachability if the phone cannot reach Windows.

An invitation contains 256 random bits and is single-use. Five incorrect attempts cancel it. The host reserves up to eight pending invitations and 16 total device slots. Expiry uses monotonic time. The device credential lasts eight hours. The host retains only its digest in memory. The phone encrypts its session with AES-GCM using a key in Android Keystore.

Separate permissions cover status, slides, scenes, recording, streaming and OBS audio. The first app provides controls for the first three categories. Status permission covers the complete status object. The host checks access on command receipt and after queueing. Already admitted operations may finish after revocation. Command responses are associated with the client ID and the individual pairing. The app does not automatically replay commands after a disconnect.

The phone server limits a request to 16 KiB, eight concurrent requests and 32 connections. Aggregate limits per minute are 24 redemption attempts and 240 status/command requests. Owner administration stays on the separate local host. [Protocol](protocol.en.md)

## Build and test

App source and the APK build script are under `Plugins/AndroidRemote/Android/`. Run `build.py --help` with Python 3. Use JDK 17 or newer, Android SDK Platform 35 and Build Tools 36.0.0. Specify a development signing key outside the repository. The script compiles, packages and verifies the APK signature. Reuse the same development signing key for local updates. A package signed with a different key requires uninstalling and pairing again.

The Android 15 automated trial installs the APK and exercises its controls through the actual TLS server and PowerPoint/OBS adapters. Controlled test sessions count actions. It covers incorrect certificates, pairing, next/previous, scene switch, status, disconnect, reconnect and revocation. A physical phone trial with Windows, Office, OBS and the actual Wi-Fi network is recorded separately. [Testing](testing.en.md)
