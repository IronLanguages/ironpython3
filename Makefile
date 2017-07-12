.PHONY: debug release test stage package clean test-smoke

debug:
	@xbuild Build.proj /t:Build /p:Mono=true /p:BuildFlavour=Debug /p:Platform="Any CPU" /verbosity:minimal /nologo

release:
	@xbuild Build.proj /t:Build /p:Mono=true /p:BuildFlavour=Release /p:Platform="Any CPU" /verbosity:minimal /nologo

stage:
	@xbuild Build.proj /t:Stage /p:Mono=true;BuildFlavour=Release /verbosity:minimal /nologo

package:
	@xbuild Build.proj /t:Package /p:Mono=true;BuildFlavour=Release /verbosity:minimal /nologo

clean:
	@xbuild Build.proj /t:Clean /p:Mono=true /verbosity:minimal /nologo

test-smoke:
	(cd bin/Debug && mono ./IronPythonTest.exe --labels=All --where:Category==StandardCPython --result=smoke-result-net45.xml) || true

test-smoke-release:
	(cd bin/Release && mono ./IronPythonTest.exe --labels=All --where:Category==StandardCPython --result=smoke-result-net45.xml) || true