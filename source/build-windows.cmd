@echo off
setlocal
cd /d "%~dp0"
set "FURZAP_CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%FURZAP_CSC%" (
    echo .NET Framework compiler not found. Install .NET Framework 4.8.
    pause
    exit /b 1
)
"%FURZAP_CSC%" /nologo /codepage:65001 /resource:..\assets\dragon.png,FurZap.Dragon.png /resource:..\assets\dragon-happy.png,FurZap.DragonHappy.png /resource:..\assets\dragon-sleep.png,FurZap.DragonSleep.png /target:winexe /platform:x64 /optimize+ /win32manifest:app.manifest /win32icon:..\furzap.ico /out:..\FurZap.exe /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.ServiceProcess.dll /reference:System.Core.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll *.cs
if errorlevel 1 (
    echo Build failed. Close FurZap before rebuilding.
    pause
    exit /b 1
)
echo Built: ..\FurZap.exe
pause

