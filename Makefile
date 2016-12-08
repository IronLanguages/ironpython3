.PHONY: debug release test stage package clean

debug:
	xbuild Build.proj /t:Build /p:Mono=true /p:BuildFlavour=Debug /p:Platform="Any CPU"

release:
	xbuild Build.proj /t:Build /p:Mono=true /p:BuildFlavour=Release /p:Platform="Any CPU"

test:
	xbuild Build.proj /t:Test /p:Mono=true /p:BuildFlavour=Release /p:Platform="Any CPU"

stage:
	xbuild Build.proj /t:Stage /p:Mono=true;BuildFlavour=Release

package:
	xbuild Build.proj /t:Package /p:Mono=true;BuildFlavour=Release

clean:
	xbuild Build.proj /t:Clean /p:Mono=true

