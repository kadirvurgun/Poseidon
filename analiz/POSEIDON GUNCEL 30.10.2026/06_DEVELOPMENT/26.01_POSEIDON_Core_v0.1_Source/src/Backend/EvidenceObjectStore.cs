using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
namespace Poseidon;

// Linux-only, anchored directory descriptors. No user supplied path component is used.
// Objects are retained on every failure: reservation accounting is never released automatically.
public sealed class EvidenceObjectStore : IDisposable
{
 readonly SafeFileHandle root;
 const int ReadOnly=0, WriteOnly=1, CreateFlag=64, Exclusive=128, Directory=65536, NoFollow=131072, CloseExec=524288;
 [DllImport("libc",SetLastError=true)] static extern int open(string path,int flags);
 [DllImport("libc",SetLastError=true)] static extern int openat(int fd,string path,int flags,uint mode=0);
 [DllImport("libc",SetLastError=true)] static extern int close(int fd);
 [DllImport("libc",SetLastError=true)] static extern int fsync(int fd);
 [DllImport("libc",SetLastError=true)] static extern uint geteuid();
 [DllImport("libc",SetLastError=true)] static extern int fstat(int fd,out LinuxStat stat);
 [StructLayout(LayoutKind.Sequential)] struct LinuxStat {
  public ulong Device,Inode,Links; public uint Mode,Uid,Gid; public int Pad;
  public ulong Rdev; public long Size,BlockSize,Blocks,AccessSeconds,AccessNanos,ModifySeconds,ModifyNanos,ChangeSeconds,ChangeNanos,Reserved1,Reserved2,Reserved3;
 }
 public EvidenceObjectStore(string path) {
  if(!OperatingSystem.IsLinux()||RuntimeInformation.ProcessArchitecture!=Architecture.X64)
   throw new InvalidOperationException("Evidence storage requires supported Linux x64 descriptor semantics.");
  if(!Path.IsPathFullyQualified(path)||path.Contains('\0'))throw new InvalidOperationException("Evidence root must be an existing absolute private directory.");
  var parts=path.Split('/',StringSplitOptions.RemoveEmptyEntries);
  if(parts.Length==0||parts.Any(p=>p is "." or ".."))throw new InvalidOperationException("Unsafe evidence root.");
  var fd=open("/",ReadOnly|Directory|NoFollow|CloseExec);
  if(fd<0)throw new IOException("Cannot anchor evidence root.");
  try {
   foreach(var part in parts) {
    var next=openat(fd,part,ReadOnly|Directory|NoFollow|CloseExec);if(next<0)throw new IOException("Evidence root contains an unavailable or unsafe directory.");
    close(fd);fd=next;
   }
   if(fstat(fd,out var s)!=0||(s.Mode&0xF000)!=0x4000||s.Uid!=geteuid()||(s.Mode&0x3F)!=0)
    throw new InvalidOperationException("Evidence root must be owned by this process user with no group or other permissions.");
   root=new SafeFileHandle((IntPtr)fd,true);fd=-1;
  } finally {if(fd>=0)close(fd);}
 }
 public FileStream Create(Guid objectId) {
  // O_EXCL prevents replacement, symlink traversal and retry overwrites.
  var added=false;try {
   root.DangerousAddRef(ref added);
   var fd=openat(root.DangerousGetHandle().ToInt32(),objectId.ToString("N")+".quarantine",WriteOnly|CreateFlag|Exclusive|NoFollow|CloseExec,0x180);
   if(fd<0)throw new IOException("Quarantine object could not be exclusively created; recover metadata before retrying.");
   return new FileStream(new SafeFileHandle((IntPtr)fd,true),FileAccess.Write,81920,false);
  }finally {if(added)root.DangerousRelease();}
 }
 public Dictionary<Guid,(long Length,string Hash)> Inspect() {
  var added=false;try {root.DangerousAddRef(ref added);var fd=root.DangerousGetHandle().ToInt32();var result=new Dictionary<Guid,(long,string)>();
   foreach(var path in System.IO.Directory.EnumerateFileSystemEntries("/proc/self/fd/"+fd)) {
    var name=Path.GetFileName(path);if(name.Length!=43||!name.EndsWith(".quarantine",StringComparison.Ordinal)||!Guid.TryParseExact(name[..32],"N",out var id))throw new InvalidOperationException("Evidence root contains unaccounted entries.");
    var filefd=openat(fd,name,ReadOnly|2048|NoFollow|CloseExec);if(filefd<0)throw new IOException("Cannot inspect retained evidence object.");
    using var handle=new SafeFileHandle((IntPtr)filefd,true);
    if(fstat(filefd,out var stat)!=0||(stat.Mode&0xF000)!=0x8000||stat.Uid!=geteuid()||(stat.Mode&0x3F)!=0||stat.Links!=1||stat.Size>EvidenceContracts.MaxBytes)throw new InvalidOperationException("Unsafe retained evidence object.");
    using var stream=new FileStream(handle,FileAccess.Read);result.Add(id,(stat.Size,Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(stream))));
   }return result;
  }finally {if(added)root.DangerousRelease();}
 }
 public void FlushDirectory() {var added=false;try{root.DangerousAddRef(ref added);if(fsync(root.DangerousGetHandle().ToInt32())!=0)throw new IOException("Quarantine directory flush failed.");}finally{if(added)root.DangerousRelease();}}
 public void Dispose()=>root.Dispose();
}
public interface IEvidenceFaults { void Checkpoint(string point); }
public sealed class NoEvidenceFaults : IEvidenceFaults { public void Checkpoint(string point){} }
