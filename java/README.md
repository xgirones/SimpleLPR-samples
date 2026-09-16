# SimpleLPR Java samples

Sample applications for the SimpleLPR automatic number plate recognition SDK.
Requirements: a Java 11+ JDK. Maven is bootstrapped automatically by the included
wrapper scripts (`mvnw` / `mvnw.cmd`) — no installation needed.

| Sample | What it shows |
|---|---|
| `simplelpr-sample-platereader` | Read the plate in a single image: candidates, country matches, per-character details |
| `simplelpr-sample-batch` | Analyze a whole folder concurrently through a `ProcessorPool` |
| `simplelpr-sample-tracking` | Track plates across a video or RTSP stream, saving one thumbnail per plate |

## Building

```
mvnw.cmd package        # Windows
./mvnw package          # Linux
```

When these samples are run from inside the SimpleLPR SDK installation, the
wrapper library is taken from the SDK itself (the `lib/repo` folder) and the
native binaries are located automatically inside the SDK installation
(`bin` on Windows, `lib` on Linux) — no configuration at all.

From a standalone checkout there is equally nothing to configure: the wrapper
and the native binaries for your platform are fetched from Maven Central (the
checkout's pom declares the `natives-win-x64` / `natives-linux-x64` classifier
artifacts) and the natives are extracted to `~/.cache/simplelpr` on first run.
A 60-day evaluation license is included; for licensed use, register your
product key with `SimpleLPR.setProductKey(...)`. An installed SDK, when
present, still takes precedence — and `SIMPLELPR_NATIVE_PATH` (or
`-Dsimplelpr.native.path=...`) overrides everything.

## Running

Read one image (run from this folder):

```
mvnw.cmd -pl :simplelpr-sample-platereader exec:java -Dexec.args="--country Spain C:\path\to\car.jpg"
```

Analyze a folder:

```
mvnw.cmd -pl :simplelpr-sample-batch exec:java -Dexec.args="--country Spain C:\path\to\images"
```

Track plates in a video, saving thumbnails:

```
mvnw.cmd -pl :simplelpr-sample-tracking exec:java -Dexec.args="--country Spain --output thumbs C:\path\to\traffic.mp4"
```

Add `--product-key <file>` to any of them to leave evaluation mode, and
`--help` to see every option. In IntelliJ IDEA, open this folder's `pom.xml`
as a project and run any sample's main class directly.
