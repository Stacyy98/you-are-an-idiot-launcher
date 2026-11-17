# You are an Idiot! native launcher

A working rebuild of the old `youdontknowwhoiam.org` prank page on modern Windows.

The original was a Flash page that spawned a storm of bouncing browser windows. Browsers
can no longer do any of it, no Flash plugin, `window.moveTo()` is ignored, and popups on
unload are blocked — so this project reimplements only the *host*, in C#, driving the
**original, unmodified `youare.swf`** through the standalone Flash player.

## What it does

* a landing window (560×420) plays `youare.swf`, audio included
* close it with the X → **6** small windows open and start bouncing
* close any one of those → **6 more**, forever, up to a hard cap of 200
* the bounce maths are `you.js` ported 1:1: `xOff`/`yOff`, `Math.ceil(-6*rnd)*5-10` /
  `Math.ceil(7*rnd)*5-10`, edges at `screen.width-175` / `screen.height-100`

## Stopping it

Task Manager does **not** work the sane way: killing one `flashplayer_sa.exe` looks like
a closed window and summons 6 more. Kill the host first.

| how | effect |
|---|---|
| `Ctrl + Alt + Shift + X` | closes everything, instantly |
| `STOP - You are an Idiot!.lnk` | same, via a desktop shortcut (`/stop`) |
| tray → *Close everything* | same, with a mouse |
| tray → *Respawn on close* | stops the multiplication, leaves open windows |
| Task Manager | end `YouAreAnIdiot.exe` **first**, then the players |

## Running it

```
YouAreAnIdiot.exe            normal
YouAreAnIdiot.exe /stop      close everything
YouAreAnIdiot.exe /capture out.png 1380   render one frame off-screen (used by icons.ps1)
```

Requires the Adobe Flash standalone player at
`C:\Program Files (x86)\Flash Player\flashplayer_sa.exe`.

Popup size defaults to 320 px wide (311 tall, the original 180×175 proportions) because
180 px is unreadably tiny on a 1920 screen. Override it by putting a single number in
`size.txt` next to the exe.

## Building

```
launcher\build.cmd          rebuilds ..\YouAreAnIdiot.exe with csc.exe
launcher\icons.ps1          re-cuts both .ico files out of youare.swf
```

No SDK needed — `build.cmd` uses the .NET Framework compiler that ships with Windows.

## Files

| file | |
|---|---|
| `youare.swf` `you.js` `open.html` `idiot.html` | untouched originals |
| `launcher\Program.cs` | the host: spawning, bounce, tray, hotkey, `/stop`, `/capture` |
| `launcher\build.cmd` | build script |
| `launcher\icons.ps1` | captures a frame, finds the smiley, writes both icons |
| `YouAreAnIdiot.ico` | dark smiley — the app |
| `stop.ico` | light smiley — the STOP shortcut |

## Notes

The host keeps its windows topmost, sets them topmost exactly once (re-asserting it every
frame is what made the z-order flicker), uses `WS_EX_NOACTIVATE` so a click can't steal
focus from what you are doing, and steps every window from one shared clock with
time-based motion, so a busy machine cannot make them accelerate.

## Please don't

This is a period piece. Run it on your own machine, or on someone who has agreed to it.
Don't deploy it to shared or work computers, and don't wrap it into anything that starts
itself at logon.
