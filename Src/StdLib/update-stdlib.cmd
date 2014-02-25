@echo off
setlocal

if [%1]==[] goto usage

set _pyrepo=%1
if not exist "%_pyrepo%\Lib" (
    echo "%_pyrepo% does not look like a CPython repo (no Lib directory)."
    goto :fail
)

for /f %%I in ('hg -R "%_pyrepo%" id -i') do set _hgrev=%%I
set _stdlibdir=%~dp0
pushd "%_stdlibdir%"

git diff-index --quiet HEAD
if "%ERRORLEVEL%" NEQ "0" (
    echo "There are uncomitted changes. Commit or stash before proceeding."
    goto :fail
)

for /f %%I in ('git rev-parse --abbrev-ref HEAD') do set _curbranch=%%I
if "%_curbranch%" NEQ "python-stdlib" (
    echo "This should only be run on the python-stdlib branch (on %_curbranch%)."
    goto :fail
)

robocopy "%_pyrepo%\Lib" "%_stdlibdir%Lib" /MIR /R:1 /W:1 /XD plat-*

git update-index --refresh -q > NUL
git add -A "%_stdlibdir%/Lib"
git diff-index --quiet HEAD

if "%ERRORLEVEL%" EQU "0" (
    echo "No changes found."
) else (
    git commit -am "Import python stdlib @ %_hgrev%"
)

goto :exit

:usage
echo %~n0 ^<path^\to^\cpython^>
exit /B 1

:fail
popd
endlocal
exit /B 1

:exit
popd
endlocal