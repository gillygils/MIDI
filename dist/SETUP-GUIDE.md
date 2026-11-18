# MIDI Link setup guide

MIDI Link sends MIDI cues from the **lyrics computer (ProPresenter)** to the
**lighting/streaming computer (ShowXpress)**. It replaces rtpMIDI.

```
 LYRICS PC                                         LIGHTING PC
 ProPresenter ──> virtual MIDI port ──> MIDI Link ═══ network ═══> MIDI Link ──> virtual MIDI port ──> ShowXpress
```

Why it's simpler than rtpMIDI:

- **No sessions and no IP lists.** Both computers announce themselves every second and find each other on their own. If either computer restarts, or its IP address changes, they reconnect within about a second.
- **Cues are confirmed.** Every MIDI message is resent until the other computer confirms it arrived, and duplicates are filtered out. If a cue can't be delivered in 3 seconds, the log shows a red **NOT DELIVERED** line, so a missed cue is never silent.
- **Clear status.** Green means connected and ready. Yellow means it's still looking for the other computer. Orange means connected, but a port setting needs attention. The status bar also shows whether the other computer has an output port selected.
- **Nothing to install.** It's a single `MidiLink.exe` that uses the .NET Framework already built into Windows 10 and 11. It uses UDP port **21928**.

---

## 1. Remove rtpMIDI from both computers

Uninstall it in **Settings → Apps → Installed apps**, then restart the computer. This keeps the old driver from getting in the way.

## 2. Create a virtual MIDI port on each computer

ProPresenter and ShowXpress need a MIDI port to connect to. A virtual "loopback" port is a cable that exists only in software. Pick one of these options:

**Option A: built into Windows 11 (no driver).** Install the *Windows MIDI Services* SDK/tools from Microsoft (aka.ms/midi). Then open **MIDI Settings** and create a loopback pair, for example *Lights A* / *Lights B*. Anything sent out on **A** comes in on **B**.
- Lyrics PC: in ProPresenter, send MIDI to **Lights A**. In MIDI Link, set *Send MIDI from* to **Lights B**.
- Lighting PC: in MIDI Link, set *Play MIDI into* to **Lights A**. In ShowXpress, listen on **Lights B**.

**Option B: loopMIDI** (free, from tobias-erichsen.de). Create one port named, for example, `Lights`, and use that same name on both ends. Leave loopMIDI running and set it to start with Windows.
If the port disappears on Windows 11 25H2, restart the *Windows MIDI Service* (in `services.msc`) or switch to Option A.

## 3. Install MIDI Link on both computers

1. Copy `MidiLink.exe` to `C:\MidiLink\` on each computer.
2. Run it. Click **Allow through firewall...** and approve the prompt. This adds a rule for all network types. A *Public* network classification is the most common reason computers can't see each other.
3. Tick **Start MIDI Link when this computer starts**.
4. Leave **Link name** as `Church` on both computers, and leave the IP blank.
5. Choose the ports:
   | Computer | Send MIDI from | Play MIDI into |
   |---|---|---|
   | Lyrics (ProPresenter) | the loopback port ProPresenter sends to | (none) |
   | Lighting (ShowXpress) | (none) | the loopback port ShowXpress listens to |
6. In ProPresenter, where you used to pick the rtpMIDI port, choose the loopback port instead. Do the same in ShowXpress for its MIDI input.

## 4. Test it

1. Both windows should turn **green** with "Connected to *other PC name*".
2. Click **Test connection**. The log should show the round-trip time, usually under 2 ms.
3. On the lyrics PC, click **Send test note**. The lighting PC's log should show `Received <- Note On ch 16 note 0`. To check the last step too, assign that note to a test cue in ShowXpress.
4. Fire a real ProPresenter slide cue. The lyrics PC logs `Sent ->` and the lighting PC logs `Received <-`.

Minimize the window to keep MIDI Link running in the tray (the blue **M** icon near the clock). Closing the window stops the cues, so it asks you to confirm first.

---

## 5. Optional: connect the two computers directly with a cable

**Yes, this helps, and it's cheap.** A dedicated cable removes the Wi-Fi, switch, router and changing IP addresses, which are the usual causes of drop-outs. You need a USB-to-Gigabit Ethernet adapter (about $15–25) on each computer that has no free Ethernet port, plus a normal Ethernet cable. A special crossover cable isn't needed.

Keep each computer's existing internet connection as it is. The new adapter is used only for the link.

On each computer, open **Settings → Network & internet → Ethernet → (the new adapter)**:

1. **IP assignment → Edit → Manual → IPv4 on**:
   | | Lyrics PC | Lighting PC |
   |---|---|---|
   | IP address | 192.168.77.1 | 192.168.77.2 |
   | Subnet mask | 255.255.255.0 | 255.255.255.0 |
   | Gateway / DNS | *leave blank* | *leave blank* |
   Leaving the gateway blank keeps Windows from trying to use this cable for internet. The 192.168.77.x addresses just need to be different from your church network's addresses.
2. **Network profile type → Private.**
3. In **Device Manager → Network adapters → (adapter) → Power Management**, untick **Allow the computer to turn off this device**.
4. Optional: in MIDI Link, enter the other computer's direct-cable IP (for example `192.168.77.2` on the lyrics PC) and click **Apply**. MIDI Link then talks only over the cable. Leaving it blank also works.

## 6. Troubleshooting

| What you see | What to do |
|---|---|
| Stays **yellow** ("Looking for...") | Make sure MIDI Link is open on both computers with the same Link name. Click *Allow through firewall* on both. Check that the network profile is Private. Try entering the other computer's IP. |
| Green on both, but lights don't react | Read the lighting PC's log. If you see `Received <-`, MIDI is arriving, so check ShowXpress's MIDI input port and the note/channel mapping. If it says `NOT played: no output port chosen`, pick the *Play MIDI into* port. |
| Orange, "port is not available" | The virtual port is missing. Start loopMIDI, or restart the Windows MIDI Service. MIDI Link reconnects to the port automatically once it's back. |
| "already in use by another program" | Another app has the port open exclusively. Close it, or use a separate loopback port for MIDI Link. |
| Red **NOT DELIVERED** lines | The other computer was unreachable for 3 seconds. Check the cable or network and confirm it's still green. |
| "Warning: ... is also using this link name" | A third computer is running MIDI Link with the same name. Give each pair its own Link name. |

Rebuilding from source: run `build.ps1`, which uses the C# compiler that ships with Windows.
