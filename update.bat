@echo off

set BASE=%~dp0
set BASE2=%~dp0
set TARGET=Debug

rmdir /s /q "tools/Carbon"
git clone "https://github.com/CarbonCommunity/Carbon.Core.git" "tools\Carbon"

cd "tools\Carbon"
call "%BASE%tools\Carbon\Tools\Build\win\bootstrap.bat"
call "%BASE%build_release.bat"

"%BASE2%tools\Carbon\Tools\DepotDownloader\DepotDownloader\bin\Release\net6.0\DepotDownloader.exe" ^
	-validate -app 258550 -branch public -filelist ^
	"%BASE2%\tools\managed.txt" -dir "%BASE2%tools\Carbon\Rust\windows"

cd %BASE2%Carbon.HookValidator
dotnet restore "%cd%" --nologo
dotnet   clean "%cd%" --configuration Release --nologo
dotnet   build "%cd%" --configuration Release --no-restore --no-incremental

"%BASE2%Carbon.HookValidator\bin\Release\Carbon.HookValidator.exe"
cd ..