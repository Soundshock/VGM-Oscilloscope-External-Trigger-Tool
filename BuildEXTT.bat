@echo off
echo %cd%
echo %cd%\out\EXTT.exe
REM pause

dotnet publish -r win-x64 -c Release /p:PublishSingleFile=true --self-contained false -o %cd%\out
ren %cd%\out\EXTT.exe vgm2externaltrigger-win64.exe 
dotnet publish -r win-x86 -c Release /p:PublishSingleFile=true --self-contained false -o %cd%\out
ren %cd%\out\EXTT.exe vgm2externaltrigger-win86.exe 
dotnet publish -r linux-x64 -c Release /p:PublishSingleFile=true --self-contained false -o %cd%\out
ren %cd%\out\EXTT vgm2externaltrigger-linux-x64
dotnet publish -r osx-x64 -c Release /p:PublishSingleFile=true --self-contained false -o %cd%\out
ren %cd%\out\EXTT vgm2externaltrigger-osx-x64

pause