MSBUILD := msbuild
CONSOLERUNNER := ../../../packages/nunit.consolerunner/3.7.0/tools/nunit3-console.exe

.PHONY: debug release test stage package clean test-smoke test-smoke-debug test-ironpython test-ironpython-debug test-cpython test-cpython-debug test-all test-all-debug

release: update-submodules
	@$(MSBUILD) Build.proj /t:Build /p:Mono=true /p:BuildFlavour=Release /p:Platform="Any CPU" /verbosity:minimal /nologo
	cp Src/DLR/bin/Release/net45/rowantest.*.dll bin/Release/net45/

debug: update-submodules
	@$(MSBUILD) Build.proj /t:Build /p:Mono=true /p:BuildFlavour=Debug /p:Platform="Any CPU" /verbosity:minimal /nologo
	cp Src/DLR/bin/Debug/net45/rowantest.*.dll bin/Debug/net45/

stage: update-submodules
	@$(MSBUILD) Build.proj /t:Stage /p:Mono=true /p:BuildFlavour=Release /verbosity:minimal /nologo

package: update-submodules
	@$(MSBUILD) Build.proj /t:Package /p:Mono=true /p:BuildFlavour=Release /verbosity:minimal /nologo

clean:
	@$(MSBUILD) Build.proj /t:Clean /p:Mono=true /verbosity:minimal /nologo

update-submodules:
	@git submodule update --init

test-ironpython:
	cd bin/Release/net45 && mono $(CONSOLERUNNER) --params "FRAMEWORK=net45" --labels=All --where:Category==IronPython --result:ironpython-net45-release-result.xml IronPythonTest.dll

test-ironpython-debug:
	cd bin/Debug/net45 && mono $(CONSOLERUNNER) --params "FRAMEWORK=net45" --labels=All --where:Category==IronPython --result:ironpython-net45-debug-result.xml IronPythonTest.dll

test-cpython:
	cd bin/Release/net45 && mono $(CONSOLERUNNER) --params "FRAMEWORK=net45" --labels=All --where:"Category==StandardCPython || Category==AllCPython" --result:cpython-net45-release-result.xml IronPythonTest.dll

test-cpython-debug:
	cd bin/Debug/net45 && mono $(CONSOLERUNNER) --params "FRAMEWORK=net45" --labels=All --where:"Category==StandardCPython || Category==AllCPython" --result:cpython-net45-debug-result.xml IronPythonTest.dll

test-smoke:
	cd bin/Release/net45 && mono $(CONSOLERUNNER) --params "FRAMEWORK=net45" --labels=All --where:Category==StandardCPython --result=smoke-net45-release-result.xml IronPythonTest.dll

test-smoke-debug:
	cd bin/Debug/net45 && mono $(CONSOLERUNNER) --params "FRAMEWORK=net45" --labels=All --where:Category==StandardCPython --result=smoke-net45-debug-result.xml IronPythonTest.dll

test-all:
	cd bin/Release/net45 && mono $(CONSOLERUNNER) --params "FRAMEWORK=net45" --labels=All --result=all-net45-release-result.xml IronPythonTest.dll

test-all-debug:
	cd bin/Debug/net45 && mono $(CONSOLERUNNER) --params "FRAMEWORK=net45" --labels=All --result=all-net45-debug-result.xml IronPythonTest.dll
