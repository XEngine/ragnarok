#!/usr/bin/env python3
"""Linux development workflow. Uses only Python's standard library and CLI tools."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import signal
import subprocess
import sys
import tempfile
import time
import uuid
from xml.sax.saxutils import escape
import zipfile

ROOT = Path(__file__).resolve().parent.parent
PROJECT = ROOT / "src/Ragnarok/Ragnarok.csproj"
STEAM = Path.home() / ".var/app/com.valvesoftware.Steam/.local/share/Steam"
os.environ.update(DOTNET_CLI_TELEMETRY_OPTOUT="1", DOTNET_NOLOGO="1")


def run(args, **kwargs):
    subprocess.run([str(arg) for arg in args], check=True, cwd=ROOT, **kwargs)


def download(url, path, digest=None):
    if not path.exists():
        partial = path.with_suffix(path.suffix + ".partial")
        run(["curl", "-fL", "--retry", "2", url, "-o", partial])
        partial.replace(path)
    if digest and hashlib.sha256(path.read_bytes()).hexdigest() != digest:
        raise RuntimeError(f"Checksum mismatch: {path}. Remove it and retry.")
    return path


def extract(archive, destination):
    # ScriptEngine's archive uses Windows backslashes even on Linux.
    with zipfile.ZipFile(archive) as contents:
        for member in contents.infolist():
            target = (destination / member.filename.replace("\\", "/")).resolve()
            if not target.is_relative_to(destination.resolve()):
                raise RuntimeError(f"Unsafe archive path: {member.filename}")
            if member.is_dir():
                target.mkdir(parents=True, exist_ok=True)
            else:
                target.parent.mkdir(parents=True, exist_ok=True)
                target.write_bytes(contents.read(member))


def require_stopped():
    for proc in Path("/proc").glob("[0-9]*/comm"):
        try:
            if proc.read_text().strip() in ("valheim", "valheim.x86_64", "valheim.exe"):
                raise RuntimeError("Valheim is running. Close it first, or use deploy and F6.")
        except (FileNotFoundError, PermissionError, ProcessLookupError):
            pass


def setup(args):
    require_stopped()
    for tool in ("curl", "rsync", "bash", "file"):
        if not shutil.which(tool):
            raise RuntimeError(f"Required tool missing: {tool}")
    candidates = [STEAM / "steamapps/common/Valheim",
                  Path.home() / ".local/share/Steam/steamapps/common/Valheim",
                  Path.home() / ".steam/steam/steamapps/common/Valheim"]
    source = Path(args.source_game).expanduser().resolve() if args.source_game else next(
        (p.resolve() for p in candidates if (p / "valheim.x86_64").exists()), None)
    if source is None or not (source / "valheim.x86_64").is_file():
        raise RuntimeError("Native Linux Valheim not found. Install in Steam or pass --source-game PATH.")
    base = Path(args.dev_root).expanduser().resolve()
    game = base / "Valheim"
    if base.is_relative_to(source) or source.is_relative_to(base):
        raise RuntimeError("Development root and original game must be separate directories.")
    if base.is_relative_to(ROOT):
        raise RuntimeError("Keep the development game, SDK and saves outside the Git checkout.")
    downloads = base / "downloads"
    downloads.mkdir(parents=True, exist_ok=True)
    sdk_dir = base / "dotnet"
    version = json.loads((ROOT / "global.json").read_text())["sdk"]["version"]
    if not (sdk_dir / "sdk" / version).is_dir():
        installer = download("https://dot.net/v1/dotnet-install.sh", downloads / "dotnet-install.sh")
        run(["bash", installer, "--version", version, "--install-dir", sdk_dir, "--no-path"])
    archives = [
        ("https://thunderstore.io/package/download/denikson/BepInExPack_Valheim/5.4.2351/",
         "BepInExPack_Valheim-5.4.2351.zip",
         "bce631497976a93977ceb08e166712e6c31d15244956f89f17df092a9b62e29f"),
        ("https://github.com/BepInEx/BepInEx.Debug/releases/download/r11.2/ScriptEngine_r11.2.zip",
         "ScriptEngine_r11.2.zip",
         "6092e582b3f79c518a549503e5f13d65c5d74f0d6381366dd444e592cde13233"),
    ]
    verified = [download(url, downloads / name, digest) for url, name, digest in archives]
    # Copy rather than hardlink: development writes cannot affect the Steam copy.
    run(["rsync", "-a", "--exclude=BepInEx", "--exclude=winhttp.dll",
         "--exclude=doorstop_config.ini", "--exclude=.doorstop_version",
         "--exclude=doorstop_libs", "--exclude=start*_*bepinex.sh",
         "--exclude=output_log.txt", str(source) + "/", str(game) + "/"])
    config = game / "BepInEx/config/BepInEx.cfg"
    previous = config.read_bytes() if config.exists() else None
    with tempfile.TemporaryDirectory(dir=downloads) as folder:
        unpacked = Path(folder)
        extract(verified[0], unpacked)
        shutil.copytree(unpacked / "BepInExPack_Valheim", game, dirs_exist_ok=True)
        extract(verified[1], game)
        if not list((game / "BepInEx/plugins").rglob("Jotunn.dll")):
            archive = download(
                "https://thunderstore.io/package/download/ValheimModding/Jotunn/2.30.2/",
                downloads / "Jotunn-2.30.2.zip",
                "8aae92da2be0eb6820cd4cf57e2f6c1d6ad0d738d4915966e7c3d7a94e9a9b0f")
            jotunn = unpacked / "Jotunn-2.30.2"
            extract(archive, jotunn)
            shutil.copytree(jotunn / "plugins", game / "BepInEx/plugins/Jotunn", dirs_exist_ok=True)
    if previous is not None:
        config.write_bytes(previous)
    else:
        text = config.read_text()
        text = re.sub(r"(\[Logging.Console\].*?Enabled = )true", r"\g<1>false", text, flags=re.S)
        config.write_text(text)
    for name in ("start_game_bepinex.sh", "start_server_bepinex.sh", "valheim.x86_64"):
        (game / name).chmod((game / name).stat().st_mode | 0o100)
    saves = base / "saves"
    saves.mkdir(exist_ok=True)
    (game / "BepInEx/scripts").mkdir(exist_ok=True)
    engine_config = game / "BepInEx/config/com.bepis.bepinex.scriptengine.cfg"
    if not engine_config.exists():
        shutil.copy2(ROOT / "config" / engine_config.name, engine_config)
    local = dict(gamePath=str(game), savePath=str(saves), dotnetPath=str(sdk_dir / "dotnet"))
    local["steamFlatpak"] = source.is_relative_to(STEAM.resolve())
    local_file = ROOT / "dev.local.json"
    if local_file.exists():
        prior = json.loads(local_file.read_text(encoding="utf-8-sig"))
        if prior.get("gamePath") == str(game) and "gameArgs" in prior:
            local["gameArgs"] = prior["gameArgs"]
    (ROOT / "dev.local.json").write_text(json.dumps(local, indent=2) + "\n")
    (ROOT / "Environment.props").write_text(
        f"<Project><PropertyGroup><GamePath>{escape(str(game))}</GamePath></PropertyGroup></Project>\n")
    run([local["dotnetPath"], "build", ROOT / "tools/LocalSaves/LocalSaves.csproj",
         "--configuration", "Release", f"-p:GamePath={game}"])
    run([local["dotnetPath"], ROOT / "tools/LocalSaves/bin/Release/net10.0/LocalSaves.dll",
         "--apply", game / "valheim_Data/Managed/assembly_utils.dll"])
    doctor(local)
    build(local, "Debug", deploy=True)


def load():
    config = ROOT / "dev.local.json"
    if not config.exists():
        raise RuntimeError("Run ./scripts/setup.sh first.")
    return json.loads(config.read_text(encoding="utf-8-sig"))


def build(local, configuration, deploy=False):
    game = Path(local["gamePath"])
    if deploy and list((game / "BepInEx/plugins").rglob("Ragnarok.dll")):
        raise RuntimeError("Remove the release Ragnarok.dll from plugins before ScriptEngine deployment.")
    run([local["dotnetPath"], "build", PROJECT, "--configuration", configuration,
         f"-p:GamePath={game}", "--nologo"])
    if deploy:
        destination = game / "BepInEx/scripts"
        destination.mkdir(exist_ok=True)
        for extension in ("pdb", "dll"):
            shutil.copy2(ROOT / f"src/Ragnarok/bin/{configuration}/net472/Ragnarok.{extension}", destination)
        print("Deployed. Press F6 in Valheim to reload Ragnarök.", flush=True)


def doctor(local):
    game = Path(local["gamePath"])
    jotunn = list((game / "BepInEx/plugins").rglob("Jotunn.dll"))
    if len(jotunn) != 1:
        raise RuntimeError("Install exactly one Jotunn 2.30.2 or newer in the development BepInEx/plugins folder.")
    print(f"OK: Jotunn installed at {jotunn[0]} (runtime requires 2.30.2 or newer)", flush=True)
    for path in (Path(local["dotnetPath"]), game / "valheim.x86_64",
                 game / "valheim_Data/Managed/assembly_valheim.dll",
                 game / "BepInEx/core/BepInEx.dll", game / "BepInEx/plugins/ScriptEngine.dll",
                 game / "doorstop_libs/libdoorstop_x64.so", game / "start_game_bepinex.sh"):
        if not path.is_file():
            raise RuntimeError(f"Missing: {path}")
        print(f"OK: {path}", flush=True)
    run([local["dotnetPath"], "--version"])
    print(f"Separate test saves: {local['savePath']}", flush=True)


def launch(local, extra):
    game = Path(local["gamePath"])
    saves = Path(local["savePath"])
    saves.mkdir(parents=True, exist_ok=True)
    # Fail before starting the game if setup's cloud guard was removed by a refresh.
    run([local["dotnetPath"], ROOT / "tools/LocalSaves/bin/Release/net10.0/LocalSaves.dll",
         "--verify", game / "valheim_Data/Managed/assembly_utils.dll"])
    env = os.environ.copy()
    env.update(SteamAppId="892970", SteamGameId="892970")
    command = [str(game / "start_game_bepinex.sh"), str(game / "valheim.x86_64"),
               "-savedir", str(saves), *local.get("gameArgs", []), *extra]
    steam_running = False
    for comm in Path("/proc").glob("[0-9]*/comm"):
        try:
            steam_running |= comm.read_text().strip() == "steam"
        except (FileNotFoundError, PermissionError, ProcessLookupError):
            pass
    if not steam_running:
        raise RuntimeError("Open Steam and sign in before launching Valheim.")
    if local.get("steamFlatpak", False):
        # Run beside Flatpak Steam so its Steamworks library and IPC are available.
        # Permissions apply to this invocation, without changing Steam's overrides.
        command = ["flatpak", "run", f"--filesystem={game}", f"--filesystem={saves}",
                   "--env=SteamAppId=892970", "--env=SteamGameId=892970", "--command=sh",
                   "com.valvesoftware.Steam", "-c", 'cd "$1" && shift && exec "$@"',
                   "ragnarok", str(game), *command]
    return subprocess.Popen(command, cwd=game, env=env,
                            start_new_session=True)


def smoke(local):
    require_stopped()
    build(local, "Debug", deploy=True)
    game = Path(local["gamePath"])
    config = game / "BepInEx/config/com.bepis.bepinex.scriptengine.cfg"
    original = config.read_bytes()
    log = game / "BepInEx/LogOutput.log"
    smoke_log = game / f"startup-smoke-{uuid.uuid4().hex}.log"
    process = None
    try:
        config.write_text(original.decode().replace("EnableFileSystemWatcher = false",
                                                   "EnableFileSystemWatcher = true"))
        if log.exists():
            shutil.copy2(log, game.parent / "LogOutput.before-smoke.log")
            log.unlink()
        # Keep graphics initialized: Valheim's menu uploads assets and decodes video.
        process = launch(local, ["-batchmode", "-ragnarok-inventory-checks",
                                 "-logFile", str(smoke_log)])
        deadline = time.monotonic() + 60
        triggered = False
        while time.monotonic() < deadline:
            text = log.read_text(errors="replace") if log.exists() else ""
            loads = len(re.findall(r"Ragnarok [0-9.]+ loaded", text))
            if loads and not triggered:
                print("PASS: Plugin loaded inside Valheim. Triggering reload.", flush=True)
                os.utime(game / "BepInEx/scripts/Ragnarok.dll", None)
                triggered = True
            if (loads >= 2 and "Ragnarok unloaded." in text and "Steam initialized" in text
                    and "Starting music menu" in text):
                if "[Error" in text or any(text.count(marker) < 2 for marker in (
                        "Ragnarok inventory checks PASS", "Ragnarok station checks PASS",
                        "Ragnarok skill XP checks PASS", "Ragnarok UI lifecycle checks PASS")):
                    raise RuntimeError(f"Runtime checks failed. Inspect {log}")
                print("PASS: Steam initialization, initial load, inventory/station/XP checks and ScriptEngine unload/reload.", flush=True)
                return
            if process.poll() is not None:
                break
            time.sleep(1)
        raise RuntimeError(f"Smoke test failed or timed out. Inspect {log} and {game}/startup-smoke.log")
    finally:
        # Flatpak's wrapper can exit independently of its game process. Match this
        # run's unique logfile argument, never another game or the Steam client.
        owned_pids = []
        for cmdline in Path("/proc").glob("[0-9]*/cmdline"):
            try:
                argv = cmdline.read_bytes().split(b"\0")
                if os.fsencode(smoke_log) in argv and os.fsencode(game / "valheim.x86_64") in argv:
                    pid = int(cmdline.parent.name)
                    owned_pids.append(pid)
                    os.kill(pid, signal.SIGTERM)
            except (FileNotFoundError, PermissionError, ProcessLookupError):
                pass
        if process is not None and process.poll() is None:
            try:
                os.killpg(process.pid, signal.SIGTERM)
                process.wait(timeout=10)
            except ProcessLookupError:
                pass
            except subprocess.TimeoutExpired:
                os.killpg(process.pid, signal.SIGKILL)
                process.wait()
        stop_deadline = time.monotonic() + 5
        while any((Path("/proc") / str(pid) / "cmdline").exists() for pid in owned_pids):
            if time.monotonic() >= stop_deadline:
                for pid in owned_pids:
                    try:
                        cmdline = (Path("/proc") / str(pid) / "cmdline").read_bytes().split(b"\0")
                        if os.fsencode(smoke_log) in cmdline:
                            os.kill(pid, signal.SIGKILL)
                    except (FileNotFoundError, ProcessLookupError):
                        pass
                break
            time.sleep(0.1)
        config.write_bytes(original)
        if smoke_log.exists():
            smoke_log.replace(game / "startup-smoke.log")


def fingerprint():
    files = [p for p in (ROOT / "src").rglob("*") if p.is_file()
             and not {"bin", "obj"}.intersection(p.relative_to(ROOT).parts)]
    files += list(ROOT.glob("*.props")) + list(ROOT.glob("*.targets")) + [ROOT / "global.json"]
    return [(str(p), p.stat().st_mtime_ns, p.stat().st_size) for p in sorted(files)]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("action", choices=["setup", "build", "deploy", "play", "test", "doctor",
                                           "watch", "logs", "package", "smoke"], nargs="?", default="build")
    parser.add_argument("--configuration", choices=["Debug", "Release"], default="Debug")
    parser.add_argument("--source-game")
    parser.add_argument("--dev-root", default=str(Path.home() / "RagnarokDev"))
    args = parser.parse_args()
    if args.action == "setup":
        setup(args)
        return
    local = load()
    if args.action in ("build", "deploy"):
        build(local, args.configuration, deploy=args.action == "deploy")
    elif args.action == "doctor":
        doctor(local)
    elif args.action == "test":
        run([local["dotnetPath"], "run", "--project", ROOT / "tests/Ragnarok.Tests"])
    elif args.action == "play":
        require_stopped()
        build(local, args.configuration, deploy=True)
        launch(local, ["-console"]).wait()
    elif args.action == "smoke":
        smoke(local)
    elif args.action == "logs":
        run(["tail", "-n", "60", "-F", Path(local["gamePath"]) / "BepInEx/LogOutput.log"])
    elif args.action == "watch":
        previous = None
        print("Watching source/assets. Save to deploy; F6 reloads in game. Ctrl+C stops.", flush=True)
        while True:
            current = fingerprint()
            if previous != current:
                time.sleep(0.5)
                previous = fingerprint()
                try:
                    build(local, args.configuration, deploy=True)
                except subprocess.CalledProcessError:
                    print("Build failed. Waiting for the next edit.", flush=True)
            time.sleep(1)
    elif args.action == "package":
        build(local, "Release")
        run([sys.executable, ROOT / "scripts/package-release.py"])


if __name__ == "__main__":
    try:
        main()
    except KeyboardInterrupt:
        sys.exit(130)
    except (RuntimeError, OSError, subprocess.CalledProcessError) as error:
        print(f"Error: {error}", file=sys.stderr)
        sys.exit(1)
