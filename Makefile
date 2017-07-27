MSBUILD := msbuild

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
	cd bin/Release && mono ./net45/IronPythonTest.exe --labels=All --where:Category==IronPython --result:ironpython-net45-release-result.xml

test-ironpython-debug:
	cd bin/Debug && mono ./net45/IronPythonTest.exe --labels=All --where:Category==IronPython --result:ironpython-net45-debug-result.xml

test-cpython:
	cd bin/Release && mono ./net45/IronPythonTest.exe --labels=All --where:"Category==StandardCPython || Category==AllCPython" --result:cpython-net45-release-result.xml

test-cpython-debug:
	cd bin/Debug && mono ./net45/IronPythonTest.exe --labels=All --where:"Category==StandardCPython || Category==AllCPython" --result:cpython-net45-debug-result.xml

test-smoke:
	cd bin/Release && mono ./net45/IronPythonTest.exe --labels=All --where:Category==StandardCPython --result=smoke-net45-release-result.xml

test-smoke-debug:
	cd bin/Debug && mono ./net45/IronPythonTest.exe --labels=All --where:Category==StandardCPython --result=smoke-net45-debug-result.xml

test-all:
	cd bin/Release && mono ./net45/IronPythonTest.exe --labels=All --result=all-net45-release-result.xml

test-all-debug:
	cd bin/Debug && mono ./net45/IronPythonTest.exe --labels=All --result=all-result-debug-net45.xml
