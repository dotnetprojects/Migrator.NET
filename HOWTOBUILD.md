# Build

To build the project simply run the build.bat file in this folder like so:

`c:\> build`

This will run the nant build in default configuration. You can pass a target to the build.bat to run a specific
target in the default.build file.

To zip the project into a zip file run build passing a zip argument like so:

`c:\> build zip`

To override any of the build properties copy local.properties-exmple to local.properties and override any of the
property values in the default.build.

If you have any questions please go to the migrator google group located at:

http://groups.google.com/group/migratordotnet-devel