using Mono.Cecil;
using Mono.Cecil.Cil;

// Development-copy-only guard. Steam's cloud storage is shared even with -savedir.
// Read/write the copied assembly offline; no hooks or objects survive plugin unload.
try
{
if (args.Length != 2 || (args[0] != "--apply" && args[0] != "--verify"))
    throw new ArgumentException("Usage: LocalSaves --apply|--verify assembly_utils.dll");

string path = Path.GetFullPath(args[1]);
using var resolver = new DefaultAssemblyResolver();
resolver.AddSearchDirectory(Path.GetDirectoryName(path));
using var assembly = AssemblyDefinition.ReadAssembly(path, new ReaderParameters { AssemblyResolver = resolver });
var method = assembly.MainModule.Types.Single(t => t.FullName == "FileHelpers")
    .Methods.Single(m => m.Name == "get_CloudStorageSupported" && m.IsStatic &&
                         m.Parameters.Count == 0 && m.ReturnType.FullName == "System.Boolean");
bool guarded = method.HasBody && method.Body.Instructions.Count == 2 &&
               method.Body.Instructions[0].OpCode == OpCodes.Ldc_I4_0 &&
               method.Body.Instructions[1].OpCode == OpCodes.Ret;
if (args[0] == "--verify" && !guarded)
    throw new InvalidOperationException("Local-save guard missing. Run scripts/setup.sh before launching.");
if (!guarded)
{
    method.Body = new MethodBody(method);
    method.Body.Instructions.Add(Instruction.Create(OpCodes.Ldc_I4_0));
    method.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
    string temporary = path + ".local-saves.tmp";
    try
    {
        assembly.Write(temporary);
        File.Move(temporary, path, overwrite: true);
    }
    finally { if (File.Exists(temporary)) File.Delete(temporary); }
}
Console.WriteLine("PASS: Development game cloud storage disabled; local saves only.");
return 0;
}
catch (Exception error)
{
    Console.Error.WriteLine(error.Message);
    return 1;
}
