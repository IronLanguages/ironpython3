The main IronPython3 git repository is at [http://github.com/IronLanguages/ironpython3](http://github.com/IronLanguages/ironpython3).

## Downloading the sources

You can [download a zipped copy](http://github.com/IronLanguages/ironpython3/zipball/main) of the latest IronPython3 sources as well.

### Installing GIT

The following links include resources for installing and using GIT:
 * [GitHub guides](http://help.github.com/)
 * [GIT documentation](http://www.kernel.org/pub/software/scm/git/docs/git.html) - If you have never used GIT, reading the [tutorial](http://www.kernel.org/pub/software/scm/git/docs/gittutorial.html) first will be a big help to finding your way around.
 * [Cheat sheet](http://cheat.errtheblog.com/s/git) - Quick reference for commonly used commands
 * [Collaborative Github Workflow](http://www.eqqon.com/index.php/Collaborative_Github_Workflow) - very good description of the workflow and tips and tricks when contributing to a project hosted on GitHub.
 * [Jimmy's Cheatsheet](http://tinyurl.com/jimmygitcheat)

### Creating a local GIT repository

You will first need to fork the IronPython3 project. [Creating a fork](https://help.github.com/fork-a-repo/) is recommended as it will allow you to contribute patches back easily. Click the "Fork" button on [https://github.com/IronLanguages/ironpython3/](https://github.com/IronLanguages/ironpython3/). This should create your personal fork, with a web URL like http://github.com/janedoe/ironpython3 (where janedoe is your github username).

You can now use the git command-line client with many Linux distributions, Mac OS, Cygwin, and Windows (msysgit) to get the sources onto your local computer using the following commands:

```
git config --global branch.autosetupmerge true
git config --global user.name "Jane Doe"
git config --global user.email janedoe@example.com

git clone git@github.com:janedoe/ironpython3.git
cd ironpython3
git remote add ironpython3 git://github.com/IronLanguages/ironpython3.git
git pull ironpython3 main
```

At a later date, to get the latest updates from the IronPython3 project, run the following command in the ironpython3 directory created above:

```
git pull ironpython3 main
```

If there is a merge conflict, edit the unmerged files to remove the conflict markers, and then run the following command:

```
git commit -a
```

To push your changes back to your fork and make them public, use `git push`.

### Working without a fork

You can skip creating a fork if you only want to browse the sources. In that case, you can clone the project directly as such:
```
git clone git://github.com/IronLanguages/ironpython3.git
git pull
```

### Initialize submodules

The DLR (Dynamic Language Runtime) is a submodule of the ironpython3 repository, you need to initialize after cloning the repository.
```
git submodule update --init
```

For more information there is an excellent tutorial on [getting started with git](http://kylecordes.com/2008/04/30/git-windows-go/)
