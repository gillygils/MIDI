# MIDI Link

MIDI Link sends MIDI between two Windows computers over a network or a direct Ethernet cable. It's a simpler replacement for rtpMIDI that works properly on Windows 11.

We use it to send lighting cues from **ProPresenter** (on the lyrics computer) to **ShowXpress** (on the lighting/streaming computer).

```
ProPresenter -> virtual MIDI port -> MIDI Link ==network==> MIDI Link -> virtual MIDI port -> ShowXpress
```

- **Connects on its own.** There are no sessions or IP lists. The two computers find each other automatically and reconnect within about a second after a restart or an IP change.
- **Confirms every cue.** Each message is resent until the other computer confirms it arrived, and duplicates are filtered out. If a cue can't be delivered, it's flagged in the log.
- **Shows clear status.** A green, yellow or orange bar shows the connection state. A built-in test button measures the round trip.
- **Needs nothing installed.** It's a single `.exe` that runs on the .NET Framework built into Windows 10 and 11. It uses UDP port 21928.

## Download and set up

Download `MidiLink.exe` from the [Releases](../../releases) page. Then follow [SETUP-GUIDE.md](dist/SETUP-GUIDE.md), which covers the virtual MIDI ports, the firewall, ProPresenter and ShowXpress settings, and the optional direct-cable setup.

## Build from source

Run the build script in PowerShell. It uses the C# compiler that ships with Windows, so no Visual Studio is needed:

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

The program is written to `dist\MidiLink.exe`.

To run the built-in network self-test, use this command. It writes PASS/FAIL results to the file you name:

```powershell
.\dist\MidiLink.exe /selftest:selftest.txt
```
