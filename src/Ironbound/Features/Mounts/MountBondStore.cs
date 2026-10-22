using System;
using System.IO;
using System.Text;

namespace Ironbound.Features.Mounts;

// Commit a complete candidate before replacing live policy. A failed write must
// never clear somebody's previous bond or leave a half-written authoritative file.
internal sealed class MountBondStore
{
    private readonly string path;
    internal MountBondLedger Current { get; private set; }

    internal MountBondStore(string path, long world)
    {
        this.path = Path.GetFullPath(path);
        Current = File.Exists(path) ? MountBondLedger.Parse(world, File.ReadAllText(path)) : new MountBondLedger(world);
    }

    internal void Commit(MountBondLedger candidate)
    {
        if (candidate.World != Current.World) throw new InvalidOperationException("Cross-world mount bond commit refused.");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temporary = path + ".pending-" + Guid.NewGuid().ToString("N");
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                byte[] bytes = Encoding.UTF8.GetBytes(candidate.Serialize());
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }
            if (File.Exists(path)) File.Replace(temporary, path, path + ".bak");
            else File.Move(temporary, path);
            Current = candidate.Copy();
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
