# Jenkins Windows builds

The repository's `Jenkinsfile` builds and checks the game, creates a self-contained
Native AOT `win-x64` publish, packages it as a ZIP, and exposes that ZIP on the
Jenkins build page under **Artifacts**.

Before packaging or uploading, Jenkins runs `UntitledGemGame.exe --tween-check`
from the published output. This checks float and Vector2 tween construction,
interpolation and completion in the Native AOT executable, without initializing
Steam, graphics or player saves. A failure stops deployment.
`TweenRuntime.Initialize` preserves both the tween constructors and the Vector2
arithmetic operators that the library discovers through reflection.

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

Steam Playtest deployment also requires the Steamworks SDK extracted to
`C:\steamworksSDK`. Run SteamCMD interactively once as the same Windows account
used by the Jenkins agent, then authorize the dedicated `teamjape_builder`
Steam account with Steam Guard. Jenkins reuses SteamCMD's saved login token and
does not store or pass the Steam password. Keep
`C:\steamworksSDK\tools\ContentBuilder\builder\config\config.vdf` private and
persistent; never commit or archive it.

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
By default, the same build is uploaded to Steam Playtest app `5426850`, depot `5426851`, and
automatically made live on its `jenkins` branch. The public/default branch is not
changed. Jenkins retains the latest 20 build records, as configured in the
pipeline.

## Pause and resume Steam uploads

After Jenkins has run this version of the pipeline once, the job page offers
**Build with Parameters** with an **UPLOAD_TO_STEAM** checkbox:

- **Pause:** uncheck **UPLOAD_TO_STEAM**, then click **Build**.
- **Resume:** check **UPLOAD_TO_STEAM**, then click **Build**.

The pipeline remembers the selection as the default for future builds, including
webhook and polling builds. Uploads start enabled when the parameter is first
introduced. Paused builds still compile, run checks, publish, and archive the ZIP;
only **Deploy Steam Playtest** is skipped. The version already live on Steam stays
available. Resuming builds and uploads the current `main` revision.

The selection applies to the build you start and subsequent builds using that
default. It does not cancel an upload already running or change parameters on
builds already queued; stop those builds separately if necessary. Saving the
parameter form without starting a build does not change the remembered selection.

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

## Steam deployment safety

The committed SteamPipe build definition contains only public app/depot IDs and
file mappings. Authentication remains in SteamCMD's machine-local login token.
If SteamCMD asks for a password during an automated run, stop the pipeline and
repeat the interactive authorization as the Windows account running the agent;
never add the password or a Steam Guard code to the Jenkinsfile.
