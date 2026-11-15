You are an Idiot! (dot org)  --  Offiz rebuild
=============================================

RUN
  "You are an Idiot!" on the desktop, or YouAreAnIdiot.exe in this folder.

WHAT HAPPENS
  * the landing window plays youare.swf  (the real file, real Flash)
  * close it with the X  ->  6 small windows open and start bouncing
  * close any one of those  ->  6 more again, forever

STOPPING IT  (important: do NOT just kill one window in Task Manager --
             that counts as "closed" and it answers with 6 more)
  Ctrl + Alt + Shift + X                  kills every window, instantly
  desktop: "STOP - You are an Idiot!"     same thing, with a double click
  tray icon -> "Close everything"         same thing, with a mouse
  tray icon -> "Respawn on close"         stops the multiplication but
                                          leaves the open ones running

  the only safe manual route: Task Manager -> end YouAreAnIdiot.exe first
  (that kills the respawner), then end the flashplayer_sa.exe processes.

SIZE
  the original popup was 180x175 on a 1024x768 monitor, which reads as tiny
  on a 1920 screen.  default here is 320 px wide (311 tall, same proportions).
  to change it, create a file called  size.txt  next to the exe containing
  one number: the popup width in pixels.

FILES
  youare.swf  open.html  idiot.html  you.js   ... the untouched originals
  YouAreAnIdiot.exe                           the host
  YouAreAnIdiot.ico                           dark smiley  - the app
  stop.ico                                    light smiley - the STOP shortcut
  launcher\Program.cs                         the host source (you.js ported 1:1)
  launcher\build.cmd                          rebuilds the exe and re-embeds the icon
  launcher\icons.ps1                          re-cuts both icons out of youare.swf

  the .swf needs the Adobe Flash standalone player:
    C:\Program Files (x86)\Flash Player\flashplayer_sa.exe
