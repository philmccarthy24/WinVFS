# Windows Virtual File System

A set of projects originally for testing the [Dokan](https://dokan-dev.github.io/) Windows user mode file system driver library for suitability for the Iress myMSO hackathon 2020 project. Wasn't integrated with the product properly (ran out of time to get the web api integrations working), but as a standalone demo working against the StubWebApi, it's a great demonstration that:

- Dokan is an awesome piece of OSS software technology solving a specific problem well (providing a file system view of a data model)!
- User mode file system drivers on Windows are viable and stable
- Different user mode file system handlers can be layered on top of each other with little impact on performance, to give a hybrid data source for a file tree view



## Installation

Download and install the [Dokan](https://dokan-dev.github.io/) redistributable, build the solution, run the web api, configure the test harness or web service to point at the web api base uri, forwarded directory and mount point (modify respective App.config files), and run.



## Projects in solution

### VirtualFileSystem

A [Dokan](https://dokan-dev.github.io/) wrapper library that makes it easy to add and stack user mode (.NET) read only file system handlers. **Read/write file systems are not supported at this time.** The library currently contains:

- **Directory Proxy File System Provider** that forwards file operations to another specified directory on the local file system
- **Web Service File System Provider** that gives a file system view of a remote web service, and allows fake local files to be retrieved via HTTP GET requests (the actual source of the file could be a database on a remote server). It doesn't support authentication of any kind at this time.



### MSO_VFS_Shim

#### Overview

A windows local service that uses the *VirtualFileSystem* library to mount a virtual file system at a particular place on the local computer. The virtual file system is a hard-coded stack of a Directory Proxy FS provider and a Web Service FS provider.

Browsing the virtual file system over a network connection from a remote PC is not supported at this time (but is possible with additional configuration / software). This should not be necessary for the hackathon project.

#### Configuration

Configuration is done through `MSO_VFS_Shim.exe.config` (App.config) file, via the following appSettings:

```xml
<add key="mountpoint" value="C:\Templates"/>
<add key="dirtoproxy" value="C:\LocalTesting\Scripts"/>
<add key="httpclienturi" value="http://websqlconfigtool.boi02.mso.devel.iress.com.au/"/>
```

- `mountpoint` is where to mount the virtual file system. *Must either be an empty directory, or not exist*
- `dirtoproxy` is the directory to forward file requests to
- `httpclienturi` is the web service base uri to retrieve files from

#### Installation

To install the service, in a command prompt use the `installutil.exe` utility located under the `C:\Windows\Microsoft.NET\Framework64\v4.0.30319` directory (or similar):

```cmd
installutil.exe MSO_VFS_Shim.exe
sc start MSOVFSShim
```

By default the service runs with Automatic start, as the *LocalSystem* user. This may pose a security risk. No investigation has been done on how to configure the service to run least privilege (noting that mounting file systems will probably require administration rights).

#### Logging

Logs are output to the `MSO_VFS_Shim.log` file, in the same directory as the `MSO_VFS_Shim.exe` binary. Logging can be configured by updating the ```<log4net>...</log4net>``` section of the `MSO_VFS_Shim.exe.config` file (see [this page](https://logging.apache.org/log4net/release/config-examples.html) for configuration examples).



### DokanTest

A Windows console application as a test harness for the *VirtualFileSystem* library. Otherwise identical functionality to MSO_VFS_Shim.



### StubWebApi

A stub web api written in Asp.Net Core 3 that can service the web service file system provider with a couple of dummy files in a pdf directory.