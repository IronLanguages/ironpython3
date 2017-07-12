.PHONY: debug release test stage package clean test-smoke test-smoke-debug test-ironpython test-ironpython-debug test-cpython test-cpython-debug test-all test-all-debug

release:
	@msbuild Build.proj /t:Build /p:Mono=true /p:BuildFlavour=Release /p:Platform="Any CPU" /verbosity:minimal /nologo
	ls -la Src/DLR/bin
	cp Src/DLR/bin/Release/rowantest.*.dll bin/Release/

debug:
	@msbuild Build.proj /t:Build /p:Mono=true /p:BuildFlavour=Debug /p:Platform="Any CPU" /verbosity:minimal /nologo
	cp Src/DLR/bin/Debug/rowantest.*.dll bin/Debug/

stage:
	@msbuild Build.proj /t:Stage /p:Mono=true /p:BuildFlavour=Release /verbosity:minimal /nologo

package:
	@msbuild Build.proj /t:Package /p:Mono=true /p:BuildFlavour=Release /verbosity:minimal /nologo

clean:
	@msbuild Build.proj /t:Clean /p:Mono=true /verbosity:minimal /nologo

test-ironpython:
	(cd bin/Release && mono ./IronPythonTest.exe --labels=All --where:Category==IronPython --result:ironpython-net45-release-result.xml) || true

test-ironpython-debug:
	(cd bin/Debug && mono ./IronPythonTest.exe --labels=All --where:Category==IronPython --result:ironpython-net45-debug-result.xml) || true

test-cpython:
	(cd bin/Release && mono ./IronPythonTest.exe --labels=All --where:"Category==StandardCPython || Category==AllCPython" --result:cpython-net45-release-result.xml) || true

test-cpython-debug:
	(cd bin/Debug && mono ./IronPythonTest.exe --labels=All --where:"Category==StandardCPython || Category==AllCPython" --result:cpython-net45-debug-result.xml) || true


test-smoke:
	(cd bin/Release && mono ./IronPythonTest.exe --labels=All --where:Category==StandardCPython --result=smoke-net45-release-result.xml) || true

test-smoke-debug:
	(cd bin/Debug && mono ./IronPythonTest.exe --labels=All --where:Category==StandardCPython --result=smoke-net45-debug-result.xml) || true

test-all:
	(cd bin/Release && mono ./IronPythonTest.exe --labels=All --result=all-net45-release-result.xml) || true

test-all-debug:
	(cd bin/Debug && mono ./IronPythonTest.exe --labels=All --result=all-result-debug-net45.xml) || true
