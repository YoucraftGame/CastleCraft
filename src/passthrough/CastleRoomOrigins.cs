using System;
using System.IO;
using System.Globalization;
using System.Text;
namespace PeakCraft;
// Reserve before use: restarting either game or rolling back a DLL cannot reuse old room data.
internal static class CastleRoomOrigins {
 public const int First=65536,Stride=1024,Limit=1048576;
 public static double Reserve(string path){
  Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
  using var file=new FileStream(path,FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
  byte[] data=new byte[file.Length];file.Read(data,0,data.Length);
  int next=First;
  if(data.Length>0&&!int.TryParse(Encoding.UTF8.GetString(data),NumberStyles.Integer,CultureInfo.InvariantCulture,out next))throw new IOException("Room origin ledger damaged; refusing to reuse saved coordinates.");
  if(next<First||next%Stride!=0||next>=Limit)throw new IOException("Room origin ledger outside precision-safe range; migration required, no saved room reused.");
  byte[] saved=Encoding.UTF8.GetBytes((next+Stride).ToString(CultureInfo.InvariantCulture));
  file.Position=0;file.Write(saved,0,saved.Length);file.SetLength(saved.Length);file.Flush(true);
  return next;
 }
}
