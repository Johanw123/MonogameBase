# Jenkins Windows builds

The repository's `Jenkinsfile` builds and checks the game, creates a self-contained
Native AOT `win-x64` publish, packages it as a ZIP, and exposes that ZIP on the
Jenkins build page under **Artifacts**.

## Windows agent requirements

Configure a Windows Jenkins agent with the label `windows` and install:

- Git
- .NET 10 SDK
- Visual Studio 2022 Build Tools with **Desktop development with C++**

The C++ toolchain is required by .NET Native AOT. The agent account must be able
to run `git`, `dotnet`, and `powershell.exe` from `PATH`. Verify this in a terminal
running as the same Windows account used by the Jenkins agent:

```bat
git --version
dotnet --version
where powershell.exe
```

## Create the job

The job shown in the Jenkins UI is a Freestyle job. Create a new **Pipeline** job
(or replace it if it is disposable), then configure:

1. Under **Pipeline**, choose **Pipeline script from SCM**.
2. Set **SCM** to **Git**.
3. Set the repository URL to the full `MonogameBase` Git repository and add
   credentials if it is private.
4. Set the branch specifier to `*/main`.
5. Set **Script Path** to `Games/UntitledGemGame/Jenkinsfile`.
6. Save, then choose **Build Now**.

Do not configure a sparse checkout of only the game directory. The game has
project references to `ContentSourceGenerator`, `Generation`, and `JapeFramework`
elsewhere in the repository.

After a successful run, download the ZIP from the build's **Artifacts** section.
Jenkins retains the latest 20 build records, as configured in the pipeline.

## Build automatically on each push

The preferred setup is a webhook from the Git host to Jenkins. Enable
**GitHub hook trigger for GITScm polling** (for GitHub) and configure the repository
webhook to send push events to:

```text
http://YOUR-JENKINS-URL/github-webhook/
```

The Jenkins address shown in the UI (`192.168.x.x`) is private to the local network,
so GitHub cannot call it directly. Unless Jenkins is exposed through a secured HTTPS
reverse proxy or tunnel, enable **Poll SCM** and use a schedule such as
`H/5 * * * *`. This checks for changes about every five minutes and starts a build
only when the configured branch changes. Do not expose the current plain-HTTP
Jenkins endpoint directly to the internet.

## Publishing beyond Jenkins

Archiving makes every successful build downloadable from Jenkins. Uploading to
Steam or itch.io is a separate deployment step and should use credentials stored
in Jenkins Credentials, never committed files. Add that deployment only after the
store/channel and release policy (every push, tags only, or manual approval) are
chosen.
