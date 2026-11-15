@echo off
rem Rebuilds ..\YouAreAnIdiot.exe from Program.cs using the .NET Framework compiler.
setlocal
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe

rem the smiley, pulled out of youare.swf
set ICON=
if exist "%~dp0..\YouAreAnIdiot.ico" set ICON=/win32icon:"%~dp0..\YouAreAnIdiot.ico"

"%CSC%" /nologo /target:winexe /optimize+ /platform:anycpu %ICON% ^
  /out:"%~dp0..\YouAreAnIdiot.exe" ^
  /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll ^
  "%~dp0Program.cs"

if errorlevel 1 (
  echo.
  echo BUILD FAILED
  exit /b 1
)
echo.
echo Built: %~dp0..\YouAreAnIdiot.exe
endlocal
