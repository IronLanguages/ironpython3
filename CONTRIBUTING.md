# Contributing a patch

The steps to contribute a change are:

1. Fork the IronPython3 repository. For more information see [Getting the Sources](docs/getting-the-sources.md).
2. Build the repository. For more information see [Building](docs/building.md).
1. Make your changes on your machine, ensure ```make.ps1 test-all``` runs successfully, and commit your changes. For more information see [Modifying the Sources](docs/modifying-the-sources.md).
1. Push the commits to your fork. This way your name will be the author of the commit in the main IronPython3 tree (once the commits are pulled into the main tree).
1. Create a pull request on Github, this will initiate a code review and CLA signing request
1. The IronPython team will review, and possibly request changes, to your PR
1. Once all comments/questions/concerns have been addressed, your PR will be merged.

Also, [Collaborative Github Workflow](http://www.eqqon.com/index.php/Collaborative_Github_Workflow) has a very good description of the workflow and tips and tricks when contributing to a project hosted on GitHub.

# Ideas for contributions

For our first release, we are currently aiming for a Python 3.4 compatible version of IronPython. To that end we have created checklists in order to help keep track of what remains to be done:

* [What's New In Python 3.0](WhatsNewInPython30.md)
* [What's New In Python 3.1](WhatsNewInPython31.md)
* [What's New In Python 3.2](WhatsNewInPython32.md)
* [What's New In Python 3.3](WhatsNewInPython33.md)
* [What's New In Python 3.4](WhatsNewInPython34.md)
* [What's New In Python 3.5](WhatsNewInPython35.md)

Suggestions for first time contributors:

* Updating documentation (such as checking off completed features in the above lists).
* Trying out failing tests to identify and/or fix the cause of the failures.
* [List of good first issues](https://github.com/IronLanguages/ironpython3/issues?q=is%3Aissue+is%3Aopen+label%3A%22good+first+issue%22).
