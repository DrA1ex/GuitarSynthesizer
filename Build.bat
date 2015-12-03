@echo off

set MSBUILD="C:\Program Files (x86)\MSBuild\14.0\Bin\MSBuild.exe"

set missingPackages=
if not exist packages\ (
    set missingPackages=YES
)

if not defined MSBUILD echo error: can't find MSBuild.exe & goto :eof
if not exist %MSBUILD% echo error: "%MSBUILD%": not found & goto :eof

echo Cleaning
%MSBUILD% GuitarSynthesizer.sln /target:Clean /p:Configuration=Release /nologo /m

if defined missingPackages (
	echo Restoring Nuget packages
) else (
	echo Building
)
%MSBUILD% GuitarSynthesizer.sln /p:Configuration=Release /nologo /m

if defined missingPackages (
	echo Building
	%MSBUILD% GuitarSynthesizer.sln /p:Configuration=Release /nologo /m
)

echo Collecting binaries

rmdir bin /s/q >nul 2>&1

xcopy GuitarSynthesizer\bin\Release\GuitarSynthesizer.exe bin\*.* > nul
xcopy GuitarSynthesizer\bin\Release\*.dll bin\*.* > nul
xcopy GuitarSynthesizer\bin\Release\media bin\media\ /s /e > nul

echo Done

:eof