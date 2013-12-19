.PHONY: debug release test stage package clean

debug:
	xbuild Build.proj /t:Build /p:Mono=true;BuildFlavour=Debug

release:
	xbuild Build.proj /t:Build /p:Mono=true;BuildFlavour=Release

test:
	xbuild Build.proj /t:Test /p:Mono=true;BuildFlavour=Release

stage:
	xbuild Build.proj /t:Stage /p:Mono=true;BuildFlavour=Release

package:
	xbuild Build.proj /t:Package /p:Mono=true;BuildFlavour=Release

clean:
	xbuild Build.proj /t:Clean /p:Mono=true

